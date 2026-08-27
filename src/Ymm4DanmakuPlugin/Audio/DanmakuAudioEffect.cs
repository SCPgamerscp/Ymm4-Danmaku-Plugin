using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;
using Ymm4DanmakuPlugin.Core.Audio;
using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Engine;
using Ymm4DanmakuPlugin.Settings;

namespace Ymm4DanmakuPlugin.Audio;

/// <summary>
/// 弾幕に合わせて効果音を重ねる音声エフェクト。
/// <para>
/// 音声アイテム (無音の音声でもよい) にこのエフェクトを追加し、
/// 映像側の弾幕アイテムと同じ「効果音チャンネル」を指定すると、
/// 発射・分裂・被弾・消滅のタイミングで効果音が鳴る。
/// </para>
/// <para>
/// <b>音ズレしない仕組み:</b> 映像側の計算結果を受け取るのではなく、
/// 同じ設定・同じシードで<b>自前にシミュレーションをやり直す</b>。
/// コアエンジンは決定論的なので必ず同じ効果音イベント列が得られる。
/// </para>
/// </summary>
[AudioEffect("弾幕効果音", ["弾幕"], ["danmaku", "弾幕", "効果音", "東方"], isAviUtlSupported: false)]
public class DanmakuAudioEffect : AudioEffectBase
{
    public override string Label => $"弾幕効果音 (ch{Channel})";

    [Display(GroupName = "弾幕効果音", Name = "チャンネル",
        Description = "映像側の弾幕アイテムで指定した「効果音チャンネル」と同じ番号にしてください (-1 で全チャンネル対応)。")]
    [TextBoxSlider("F0", "ch", -1, 255)]
    [DefaultValue(0)]
    [Range(-1, 255)]
    public int Channel { get => channel; set => Set(ref channel, value); }
    private int channel;

    [Display(GroupName = "弾幕効果音", Name = "音量")]
    [TextBoxSlider("F2", "倍", 0, 2)]
    [DefaultValue(1d)]
    [Range(0, 4)]
    public double Volume { get => volume; set => Set(ref volume, value); }
    private double volume = 1.0;

    [Display(GroupName = "弾幕効果音", Name = "時間オフセット",
        Description = "効果音を前後にずらします。負で早く鳴ります。")]
    [TextBoxSlider("F3", "秒", -1, 1)]
    [DefaultValue(0d)]
    [Range(-10, 10)]
    public double TimeOffset { get => timeOffset; set => Set(ref timeOffset, value); }
    private double timeOffset;

    [Display(GroupName = "弾幕効果音", Name = "同時発音数",
        Description = "同時に重ねられる効果音の上限。超えた分は古い音から打ち切られます。")]
    [TextBoxSlider("F0", "音", 4, 128)]
    [DefaultValue(32)]
    [Range(1, 512)]
    public int MaxVoices { get => maxVoices; set => Set(ref maxVoices, value); }
    private int maxVoices = 32;

    [Display(GroupName = "弾幕効果音", Name = "元の音を残す",
        Description = "オフにすると元の音声を消し、効果音だけを出力します。")]
    [ToggleSlider]
    public bool KeepInput { get => keepInput; set => Set(ref keepInput, value); }
    private bool keepInput = true;

    public override IAudioEffectProcessor CreateAudioEffect(TimeSpan duration) =>
        new DanmakuAudioEffectProcessor(this, duration);

    /// <summary>AviUtl (exo) には出力できない (独自シミュレーションのため)。</summary>
    public override IEnumerable<string> CreateExoAudioFilters(
        int keyFrameIndex,
        ExoOutputDescription exoOutputDescription) => [];

    /// <summary>参照する音声ファイルは YMM4 の設定画面側で管理するため、ここでは列挙しない。</summary>
    public override IEnumerable<TimelineResource> GetResources() => [];

    protected override IEnumerable<IAnimatable> GetAnimatables() => [];
}

/// <summary>
/// <see cref="DanmakuAudioEffect"/> の音声処理本体。
/// <para>
/// 効果音イベントを「発音予約リスト」に変換し、
/// 読み出し要求ごとに、その区間に重なる発音をミックスする。
/// </para>
/// </summary>
public sealed class DanmakuAudioEffectProcessor : AudioEffectProcessorBase
{
    /// <summary>1 回の発音。</summary>
    private readonly record struct Voice(DanmakuSoundBuffer Buffer, long StartPosition, double PitchRatio, double Volume);

    private readonly DanmakuAudioEffect effect;
    private readonly TimeSpan duration;
    private readonly List<Voice> voices = [];

    private readonly float[] mixBuffer;
    private bool isPrepared;

    public DanmakuAudioEffectProcessor(DanmakuAudioEffect effect, TimeSpan duration)
    {
        this.effect = effect;
        this.duration = duration;
        mixBuffer = new float[4096];
    }

    public override int Hz => Input?.Hz ?? 48000;

    public override long Duration => Input?.Duration ?? (long)(duration.TotalSeconds * Hz) * 2;

    /// <summary>効果音が 1 つも用意できなかった場合の理由 (デバッグ用)。</summary>
    public string? Diagnostics { get; private set; }

    protected override int read(float[] destBuffer, int offset, int count)
    {
        // まず入力をそのまま (または無音として) 書き込む
        var read = 0;
        if (Input is not null && effect.KeepInput)
        {
            read = Input.Read(destBuffer, offset, count);
            if (read < count) Array.Clear(destBuffer, offset + read, count - read);
        }
        else
        {
            Input?.Seek(Position + count);
            Array.Clear(destBuffer, offset, count);
        }

        Prepare();

        if (voices.Count == 0) return count;

        // この読み出し区間 (float 要素単位) に重なる発音を加算する
        var regionStart = Position;
        var regionEnd = Position + count;
        var gain = (float)Math.Clamp(effect.Volume, 0.0, 4.0);

        foreach (var voice in voices)
        {
            var voiceLength = (long)(voice.Buffer.FrameCount / Math.Max(0.01, voice.PitchRatio)) * 2;
            var voiceEnd = voice.StartPosition + voiceLength;

            if (voiceEnd <= regionStart || voice.StartPosition >= regionEnd) continue;

            MixVoice(destBuffer, offset, count, regionStart, voice, gain);
        }

        return count;
    }

    /// <summary>1 つの発音を出力バッファへ加算する。</summary>
    private static void MixVoice(
        float[] destBuffer,
        int offset,
        int count,
        long regionStart,
        in Voice voice,
        float gain)
    {
        var buffer = voice.Buffer;
        var volume = (float)voice.Volume * gain;
        if (volume <= 0f) return;

        // count は 2ch インターリーブの要素数。フレーム単位で回す。
        var frames = count / 2;
        for (var i = 0; i < frames; i++)
        {
            var elementPosition = regionStart + i * 2;
            var elapsedElements = elementPosition - voice.StartPosition;
            if (elapsedElements < 0) continue;

            // ピッチ比 = 再生速度。1.0 より大きいと速く (高く) 再生される。
            var sourceFrame = elapsedElements / 2.0 * voice.PitchRatio;
            if (sourceFrame >= buffer.FrameCount) break;

            var index = offset + i * 2;
            destBuffer[index] += buffer.SampleAt(sourceFrame, 0) * volume;
            destBuffer[index + 1] += buffer.SampleAt(sourceFrame, 1) * volume;
        }
    }

    /// <summary>
    /// 弾幕シミュレーションを実行し、効果音イベントを発音予約へ変換する。
    /// 1 度だけ行い、以降は結果を使い回す。
    /// </summary>
    private void Prepare()
    {
        if (isPrepared) return;

        var settings = DanmakuChannelBus.TryGetSettings(effect.Channel);
        if (settings is null)
        {
            Diagnostics = $"チャンネル {effect.Channel} の弾幕アイテムが見つかりません。";
            return;
        }

        isPrepared = true;

        // 音声側は描画しないので、必要な長さぶんだけシミュレーションする
        var simulator = new DanmakuSimulator(settings)
        {
            MaxSimulationSeconds = Math.Max(1.0, duration.TotalSeconds + 1.0),
        };
        simulator.SeekTo(duration.TotalSeconds);

        var log = simulator.SoundLog;
        if (log.Count == 0)
        {
            Diagnostics = "効果音イベントがありません。弾幕アイテム側で効果音を有効にし、設定画面で音声ファイルを指定してください。";
            return;
        }

        BuildVoices(log, settings);
    }

    private void BuildVoices(SoundEventLog log, DanmakuSettings settings)
    {
        var soundSettings = DanmakuSoundSettings.Default;
        var hz = Hz;

        // 種類ごとにバッファを 1 度だけ取得する
        var buffers = new Dictionary<DanmakuSoundKind, DanmakuSoundBuffer?>();
        DanmakuSoundBuffer? GetBuffer(DanmakuSoundKind kind)
        {
            if (buffers.TryGetValue(kind, out var cached)) return cached;
            var itemSound = settings.GetSound(kind);
            var filePath = !string.IsNullOrWhiteSpace(itemSound.FilePath)
                ? itemSound.FilePath
                : soundSettings.GetEntry(kind).FilePath;
            var buffer = DanmakuSoundBuffer.Load(filePath);
            buffers[kind] = buffer;
            return buffer;
        }

        var missing = new List<string>();

        foreach (var e in log.Events)
        {
            var buffer = GetBuffer(e.Kind);
            if (buffer is null)
            {
                var itemSound = settings.GetSound(e.Kind);
                var name = !string.IsNullOrWhiteSpace(itemSound.FilePath)
                    ? itemSound.FilePath
                    : soundSettings.GetEntry(e.Kind).FilePath;
                var label = string.IsNullOrWhiteSpace(name) ? $"{e.Kind}: 未設定" : $"{e.Kind}: {Path.GetFileName(name)}";
                if (!missing.Contains(label)) missing.Add(label);
                continue;
            }

            var time = settings.TimeScale < 0
                ? (duration.TotalSeconds - e.TimeSeconds / Math.Max(0.01, Math.Abs(settings.TimeScale))) + effect.TimeOffset
                : (settings.TimeScale > 0 ? e.TimeSeconds / settings.TimeScale : e.TimeSeconds) + effect.TimeOffset;
            if (time < 0) continue;
            if (time > duration.TotalSeconds) continue;

            // Position は「サンプル数 × 2ch」なので偶数に丸める (L/R の位相をずらさない)
            var startPosition = (long)(time * hz) * 2;

            voices.Add(new Voice(buffer, startPosition, e.PitchRatio, e.Volume));

            if (voices.Count >= effect.MaxVoices * 64) break;
        }

        if (voices.Count == 0 && missing.Count > 0)
            Diagnostics = "効果音ファイルを読み込めません: " + string.Join(", ", missing);

        // 同時発音数の制限: 開始位置でソートしておくと後段の打ち切りが自然になる
        voices.Sort(static (a, b) => a.StartPosition.CompareTo(b.StartPosition));
    }

    protected override void seek(long position) => Input?.Seek(position);
}
