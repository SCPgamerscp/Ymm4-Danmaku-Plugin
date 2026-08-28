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
using Ymm4DanmakuPlugin.Core.Mathematics;

namespace Ymm4DanmakuPlugin.Audio;

/// <summary>
/// 単機能弾幕効果音エフェクトの共通基底クラス。
/// <para>
/// 音声アイテムにこのエフェクトを追加すると、その音声アイテム自身の音源ファイルを使って、
/// 弾幕の各イベント（ショット・発射・被弾等）のタイミングに合わせて自動で効果音を鳴らします。
/// エフェクト側で音声ファイルを二重に選び直す必要がありません。
/// </para>
/// </summary>
public abstract class DanmakuSingleSoundAudioEffectBase : AudioEffectBase
{
    public abstract DanmakuSoundKind SoundKind { get; }

    [Display(GroupName = "基本設定", Name = "チャンネル",
        Description = "映像側の弾幕アイテムで指定した「効果音チャンネル」と同じ番号にしてください (-1 で全チャンネル対応)。")]
    [TextBoxSlider("F0", "ch", -1, 255)]
    [DefaultValue(0)]
    [Range(-1, 255)]
    public int Channel { get => channel; set => Set(ref channel, value); }
    private int channel;

    [Display(GroupName = "基本設定", Name = "音量")]
    [TextBoxSlider("F2", "倍", 0, 2)]
    [DefaultValue(1d)]
    [Range(0, 4)]
    public double Volume { get => volume; set => Set(ref volume, value); }
    private double volume = 1.0;

    [Display(GroupName = "基本設定", Name = "ピッチ変調",
        Description = "±この半音幅でランダムに変調して音の単調さを防ぎます (0 で音程を完全に固定)。")]
    [TextBoxSlider("F2", "半音", 0, 6)]
    [DefaultValue(0d)]
    [Range(0, 12)]
    public double PitchJitter { get => pitchJitter; set => Set(ref pitchJitter, value); }
    private double pitchJitter = 0.0;

    [Display(GroupName = "基本設定", Name = "時間オフセット",
        Description = "効果音を前後にずらします。負で早く鳴ります。")]
    [TextBoxSlider("F3", "秒", -1, 1)]
    [DefaultValue(0d)]
    [Range(-10, 10)]
    public double TimeOffset { get => timeOffset; set => Set(ref timeOffset, value); }
    private double timeOffset;

    [Display(GroupName = "基本設定", Name = "同時発音数",
        Description = "同時に重ねられる効果音の上限。超えた分は古い音から打ち切られます。")]
    [TextBoxSlider("F0", "音", 4, 256)]
    [DefaultValue(64)]
    [Range(1, 1024)]
    public int MaxVoices { get => maxVoices; set => Set(ref maxVoices, value); }
    private int maxVoices = 64;

    [Display(GroupName = "再生範囲", Name = "開始位置",
        Description = "音源の何秒目から再生するかを指定します (先頭の無音カット等に便利)。")]
    [TextBoxSlider("F3", "秒", 0, 5)]
    [DefaultValue(0d)]
    [Range(0, 60)]
    public double TrimStart { get => trimStart; set => Set(ref trimStart, value); }
    private double trimStart;

    [Display(GroupName = "再生範囲", Name = "再生時間",
        Description = "1音あたりの最大再生時間 (0 で音源の最後まで再生)。長すぎる音のカットに便利です。")]
    [TextBoxSlider("F3", "秒", 0, 5)]
    [DefaultValue(0d)]
    [Range(0, 60)]
    public double PlayDuration { get => playDuration; set => Set(ref playDuration, value); }
    private double playDuration;

    [Display(GroupName = "再生範囲", Name = "フェードアウト",
        Description = "再生時間の末尾で音が急に途切れてプチプチ鳴るのを防ぐフェード時間です。")]
    [TextBoxSlider("F3", "秒", 0, 0.5)]
    [DefaultValue(0.02d)]
    [Range(0, 5)]
    public double FadeOut { get => fadeOut; set => Set(ref fadeOut, value); }
    private double fadeOut = 0.02;

    public override IAudioEffectProcessor CreateAudioEffect(TimeSpan duration) =>
        new DanmakuSingleSoundProcessor(this, SoundKind, duration);

    public override IEnumerable<string> CreateExoAudioFilters(
        int keyFrameIndex,
        ExoOutputDescription exoOutputDescription) => [];

    public override IEnumerable<TimelineResource> GetResources() => [];

    protected override IEnumerable<IAnimatable> GetAnimatables() => [];
}

/// <summary>自機ショット専用の弾幕効果音エフェクト。</summary>
[AudioEffect("弾幕効果音 (自機ショット)", ["弾幕", "自機"], ["shot", "自機", "射撃", "効果音"], isAviUtlSupported: false)]
public sealed class DanmakuPlayerShotAudioEffect : DanmakuSingleSoundAudioEffectBase
{
    public override string Label => $"弾幕効果音:自機ショット (ch{Channel})";
    public override DanmakuSoundKind SoundKind => DanmakuSoundKind.PlayerShot;
}

/// <summary>敵弾発射専用の弾幕効果音エフェクト。</summary>
[AudioEffect("弾幕効果音 (敵弾発射)", ["弾幕", "発射"], ["fire", "発射", "敵弾", "効果音"], isAviUtlSupported: false)]
public sealed class DanmakuFireAudioEffect : DanmakuSingleSoundAudioEffectBase
{
    public override string Label => $"弾幕効果音:敵弾発射 (ch{Channel})";
    public override DanmakuSoundKind SoundKind => DanmakuSoundKind.Fire;
}

/// <summary>被弾音専用の弾幕効果音エフェクト。</summary>
[AudioEffect("弾幕効果音 (被弾音)", ["弾幕", "被弾"], ["hit", "被弾", "命中", "効果音"], isAviUtlSupported: false)]
public sealed class DanmakuHitAudioEffect : DanmakuSingleSoundAudioEffectBase
{
    public override string Label => $"弾幕効果音:被弾 (ch{Channel})";
    public override DanmakuSoundKind SoundKind => DanmakuSoundKind.Hit;
}

/// <summary>変化・分裂音専用の弾幕効果音エフェクト。</summary>
[AudioEffect("弾幕効果音 (変化・分裂)", ["弾幕", "変化"], ["change", "変化", "分裂", "効果音"], isAviUtlSupported: false)]
public sealed class DanmakuChangeAudioEffect : DanmakuSingleSoundAudioEffectBase
{
    public override string Label => $"弾幕効果音:変化 (ch{Channel})";
    public override DanmakuSoundKind SoundKind => DanmakuSoundKind.Change;
}

/// <summary>消滅音専用の弾幕効果音エフェクト。</summary>
[AudioEffect("弾幕効果音 (消滅音)", ["弾幕", "消滅"], ["vanish", "消滅", "効果音"], isAviUtlSupported: false)]
public sealed class DanmakuVanishAudioEffect : DanmakuSingleSoundAudioEffectBase
{
    public override string Label => $"弾幕効果音:消滅 (ch{Channel})";
    public override DanmakuSoundKind SoundKind => DanmakuSoundKind.Vanish;
}

/// <summary>
/// 単機能弾幕効果音の音声処理本体。
/// </summary>
public sealed class DanmakuSingleSoundProcessor : AudioEffectProcessorBase
{
    private readonly record struct Voice(
        DanmakuSoundBuffer Buffer,
        long StartPosition,
        double PitchRatio,
        double Volume,
        double StartFrame,
        double MaxFrames,
        double FadeOutFrames);

    private readonly DanmakuSingleSoundAudioEffectBase effect;
    private readonly DanmakuSoundKind soundKind;
    private readonly TimeSpan duration;
    private readonly List<Voice> voices = [];

    private bool isPrepared;

    public DanmakuSingleSoundProcessor(DanmakuSingleSoundAudioEffectBase effect, DanmakuSoundKind soundKind, TimeSpan duration)
    {
        this.effect = effect;
        this.soundKind = soundKind;
        this.duration = duration;
    }

    public override int Hz => Input?.Hz ?? 48000;

    public override long Duration => Input?.Duration ?? (long)(duration.TotalSeconds * Hz) * 2;

    public string? Diagnostics { get; private set; }

    protected override int read(float[] destBuffer, int offset, int count)
    {
        // 音声アイテム自体の音はそのまま鳴らさず、弾幕イベントに合わせて発音するためクリア
        Input?.Seek(Position + count);
        Array.Clear(destBuffer, offset, count);

        Prepare();

        if (voices.Count == 0) return count;

        var regionStart = Position;
        var regionEnd = Position + count;
        var gain = (float)Math.Clamp(effect.Volume, 0.0, 4.0);

        foreach (var voice in voices)
        {
            var voiceLength = (long)(voice.MaxFrames / Math.Max(0.01, voice.PitchRatio)) * 2;
            var voiceEnd = voice.StartPosition + voiceLength;

            if (voiceEnd <= regionStart || voice.StartPosition >= regionEnd) continue;

            MixVoice(destBuffer, offset, count, regionStart, voice, gain);
        }

        return count;
    }

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
        if (volume <= 0f || voice.MaxFrames <= 0) return;

        var frames = count / 2;
        var startFrame = voice.StartFrame;
        var maxFrames = voice.MaxFrames;
        var fadeOutFrames = voice.FadeOutFrames;

        for (var i = 0; i < frames; i++)
        {
            var elementPosition = regionStart + i * 2;
            var elapsedElements = elementPosition - voice.StartPosition;
            if (elapsedElements < 0) continue;

            var sourceFramesElapsed = elapsedElements / 2.0 * voice.PitchRatio;
            if (sourceFramesElapsed >= maxFrames) break;

            var sourceFrame = startFrame + sourceFramesElapsed;
            if (sourceFrame >= buffer.FrameCount) break;

            // フェードアウト計算 (末尾付近で 1.0 -> 0.0 へ線形減衰)
            var fadeMultiplier = 1.0f;
            if (fadeOutFrames > 0)
            {
                var remainingFrames = maxFrames - sourceFramesElapsed;
                if (remainingFrames < fadeOutFrames)
                {
                    fadeMultiplier = (float)Math.Clamp(remainingFrames / fadeOutFrames, 0.0, 1.0);
                }
            }

            var currentVol = volume * fadeMultiplier;
            var index = offset + i * 2;
            destBuffer[index] += buffer.SampleAt(sourceFrame, 0) * currentVol;
            destBuffer[index + 1] += buffer.SampleAt(sourceFrame, 1) * currentVol;
        }
    }

    private void Prepare()
    {
        if (isPrepared) return;

        var settings = DanmakuChannelBus.TryGetSettings(effect.Channel);
        if (settings is null)
        {
            Diagnostics = $"チャンネル {effect.Channel} の弾幕アイテムが見つかりません。";
            return;
        }

        // 音声バッファの取得: 音声アイテム (Input) から自動取得
        var buffer = Input is not null ? DanmakuSoundBuffer.FromAudioStream(Input) : null;
        if (buffer is null)
        {
            Diagnostics = "音声ソースを取得できませんでした。音声アイテムに音源をセットしてください。";
            return;
        }

        isPrepared = true;

        var simulator = new DanmakuSimulator(settings)
        {
            MaxSimulationSeconds = Math.Max(1.0, duration.TotalSeconds + 1.0),
        };
        simulator.SeekTo(duration.TotalSeconds);

        var log = simulator.SoundLog;
        if (log.Count == 0) return;

        var hz = Hz;
        var random = new DeterministicRandom(settings.Seed + (int)soundKind * 1000);

        var startFrame = Math.Max(0.0, effect.TrimStart * hz);
        var totalAvailableFrames = Math.Max(0.0, buffer.FrameCount - startFrame);
        var maxFrames = effect.PlayDuration > 0
            ? Math.Min(totalAvailableFrames, effect.PlayDuration * hz)
            : totalAvailableFrames;
        var fadeOutFrames = Math.Max(0.0, effect.FadeOut * hz);

        foreach (var e in log.Events)
        {
            if (e.Kind != soundKind) continue;

            var time = settings.TimeScale < 0
                ? (duration.TotalSeconds - e.TimeSeconds / Math.Max(0.01, Math.Abs(settings.TimeScale))) + effect.TimeOffset
                : (settings.TimeScale > 0 ? e.TimeSeconds / settings.TimeScale : e.TimeSeconds) + effect.TimeOffset;
            if (time < 0) continue;
            if (time > duration.TotalSeconds) continue;

            var startPosition = (long)(time * hz) * 2;
            var semitones = effect.PitchJitter > 0 ? random.NextSymmetric(effect.PitchJitter) : 0;
            var pitchRatio = e.PitchRatio * DanmakuMath.SemitoneToRatio(semitones);

            voices.Add(new Voice(buffer, startPosition, pitchRatio, e.Volume, startFrame, maxFrames, fadeOutFrames));

            if (voices.Count >= effect.MaxVoices * 256) break;
        }

        voices.Sort(static (a, b) => a.StartPosition.CompareTo(b.StartPosition));
    }

    protected override void seek(long position) => Input?.Seek(position);
}
