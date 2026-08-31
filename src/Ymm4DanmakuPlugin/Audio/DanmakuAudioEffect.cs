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
using Ymm4DanmakuPlugin.Parameters;

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

    [Display(GroupName = "発音制限", Name = "同時発音まとめ",
        Description = "極小時間内の発音を1回にまとめます。オフにすると超連射時に全弾の音が重なり爆音・音割れします。")]
    [ToggleSlider]
    [DefaultValue(true)]
    public bool CoalesceSimultaneous { get => coalesceSimultaneous; set => Set(ref coalesceSimultaneous, value); }
    private bool coalesceSimultaneous = true;

    [Display(GroupName = "発音制限", Name = "まとめ間隔",
        Description = "まとめる時間幅 (ミリ秒)。0ms で同一タイムステップ以外の制限を完全解除します。")]
    [TextBoxSlider("F1", "ms", 0, 50)]
    [DefaultValue(1.0d)]
    [Range(0, 1000)]
    public double CoalesceIntervalMs { get => coalesceIntervalMs; set => Set(ref coalesceIntervalMs, value); }
    private double coalesceIntervalMs = 1.0;

    [Display(GroupName = "発音制限", Name = "同時発音数",
        Description = "同時に重ねられる効果音の上限。超えた分は古い音から打ち切られます。")]
    [TextBoxSlider("F0", "音", 4, 8192)]
    [DefaultValue(1024)]
    [Range(1, 16384)]
    public int MaxVoices { get => maxVoices; set => Set(ref maxVoices, value); }
    private int maxVoices = 1024;

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

    private int processorCreationCount;

    public override IAudioEffectProcessor CreateAudioEffect(TimeSpan duration)
    {
        var index = Interlocked.Increment(ref processorCreationCount) - 1;
        return new DanmakuSingleSoundProcessor(this, SoundKind, duration, index);
    }

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

/// <summary>ボス被弾音 (自機ショット命中) 専用の弾幕効果音エフェクト。</summary>
[AudioEffect("弾幕効果音 (ボス被弾)", ["弾幕", "被弾"], ["hit", "ボス", "命中", "被弾", "ダメージ", "効果音"], isAviUtlSupported: false)]
public sealed class DanmakuEnemyHitAudioEffect : DanmakuSingleSoundAudioEffectBase
{
    public override string Label => $"弾幕効果音:ボス被弾 (ch{Channel})";
    public override DanmakuSoundKind SoundKind => DanmakuSoundKind.EnemyHit;
}

/// <summary>自機被弾音 (喰らい・ミス) 専用の弾幕効果音エフェクト。</summary>
[AudioEffect("弾幕効果音 (自機被弾)", ["弾幕", "被弾"], ["miss", "ピチューン", "自機", "被弾", "喰らい", "ミス", "効果音"], isAviUtlSupported: false)]
public sealed class DanmakuPlayerHitAudioEffect : DanmakuSingleSoundAudioEffectBase
{
    public override string Label => $"弾幕効果音:自機被弾 (ch{Channel})";
    public override DanmakuSoundKind SoundKind => DanmakuSoundKind.PlayerHit;
}

/// <summary>被弾音 (共通) 専用の弾幕効果音エフェクト。</summary>
[AudioEffect("弾幕効果音 (被弾音:共通)", ["弾幕", "被弾"], ["hit", "被弾", "命中", "効果音"], isAviUtlSupported: false)]
public sealed class DanmakuHitAudioEffect : DanmakuSingleSoundAudioEffectBase
{
    public override string Label => $"弾幕効果音:被弾(共通) (ch{Channel})";
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
    private readonly int processorIndex;
    private readonly List<Voice> voices = [];

    private DanmakuSoundBuffer? soundBuffer;
    private int preparedVersion = -1;

    public DanmakuSingleSoundProcessor(
        DanmakuSingleSoundAudioEffectBase effect,
        DanmakuSoundKind soundKind,
        TimeSpan duration,
        int processorIndex = 0)
    {
        this.effect = effect;
        this.soundKind = soundKind;
        this.duration = duration;
        this.processorIndex = processorIndex;
    }

    public override int Hz => Input?.Hz ?? 48000;

    public override long Duration => Input?.Duration ?? (long)(duration.TotalSeconds * Hz) * 2;

    public string? Diagnostics { get; private set; }

    protected override int read(float[] destBuffer, int offset, int count)
    {
        Array.Clear(destBuffer, offset, count);

        var busVersion = DanmakuChannelBus.Version;
        if (voices.Count == 0 || busVersion != preparedVersion)
        {
            PrepareVoices();
            preparedVersion = busVersion;
        }

        if (voices.Count == 0) return count;

        var regionStart = Position;
        var regionEnd = Position + count;
        var gain = (float)Math.Clamp(effect.Volume, 0.0, 4.0);
        var activeVoiceCount = 0;
        var maxVoices = effect.MaxVoices > 0 ? effect.MaxVoices : int.MaxValue;

        foreach (var voice in voices)
        {
            var voiceLength = (long)(voice.MaxFrames / Math.Max(0.01, voice.PitchRatio)) * 2;
            var voiceEnd = voice.StartPosition + voiceLength;

            if (voiceEnd <= regionStart || voice.StartPosition >= regionEnd) continue;

            MixVoice(destBuffer, offset, count, regionStart, voice, gain);
            activeVoiceCount++;
            if (activeVoiceCount >= maxVoices) break;
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

    private DanmakuSoundBuffer? EnsureSoundBuffer()
    {
        if (soundBuffer is not null) return soundBuffer;
        if (Input is null) return null;
        soundBuffer = DanmakuSoundBuffer.FromAudioStream(Input);
        return soundBuffer;
    }

    private void PrepareVoices()
    {
        var buffer = EnsureSoundBuffer();
        if (buffer is null)
        {
            Diagnostics = "音声ソースを取得できませんでした。音声アイテムに音源をセットしてください。";
            return;
        }

        var registrations = DanmakuChannelBus.GetRegistrations(effect.Channel);
        var fallbackSettings = registrations.Count == 0 ? DanmakuChannelBus.TryGetSettings(effect.Channel) : null;

        if (registrations.Count == 0 && fallbackSettings is null)
        {
            Diagnostics = $"チャンネル {effect.Channel} の弾幕アイテムが見つかりません。";
            return;
        }

        voices.Clear();

        var hz = Hz;
        var startFrame = Math.Max(0.0, effect.TrimStart * hz);
        var totalAvailableFrames = Math.Max(0.0, buffer.FrameCount - startFrame);
        var maxFrames = effect.PlayDuration > 0
            ? Math.Min(totalAvailableFrames, effect.PlayDuration * hz)
            : totalAvailableFrames;
        var fadeOutFrames = Math.Max(0.0, effect.FadeOut * hz);

        if (registrations.Count > 0)
        {
            var minTimelineStart = registrations.Min(r => r.TimelineStartSeconds);
            var maxTimelineEnd = registrations.Max(r => r.TimelineStartSeconds + r.TimelineDurationSeconds);
            var totalTimelineSpan = maxTimelineEnd - minTimelineStart;

            // 1本の長い音声アイテムで複数の弾幕アイテムを跨いでいる場合 (duration が全体の広がりをカバー)
            if (registrations.Count > 1 && duration.TotalSeconds >= totalTimelineSpan * 0.8)
            {
                foreach (var reg in registrations)
                {
                    if (!reg.ParameterRef.TryGetTarget(out var parameter)) continue;

                    var settings = parameter.ToSettings(parameter.LastCanvasWidth, parameter.LastCanvasHeight);
                    var timelineOffset = Math.Max(0.0, reg.TimelineStartSeconds - minTimelineStart);

                    if (timelineOffset >= duration.TotalSeconds) continue;

                    ProcessSettings(settings, parameter, buffer, hz, timelineOffset, startFrame, maxFrames, fadeOutFrames);
                }
            }
            else
            {
                // 個別アイテムの場合: processorIndex に対応する弾幕アイテム、またはアクティブなアイテムを使用
                var targetIndex = Math.Clamp(processorIndex, 0, registrations.Count - 1);
                var reg = registrations[targetIndex];
                if (reg.ParameterRef.TryGetTarget(out var parameter))
                {
                    var settings = parameter.ToSettings(parameter.LastCanvasWidth, parameter.LastCanvasHeight);
                    ProcessSettings(settings, parameter, buffer, hz, 0.0, startFrame, maxFrames, fadeOutFrames);
                }
                else
                {
                    var activeParam = DanmakuChannelBus.TryGetParameter(effect.Channel);
                    if (activeParam is not null)
                    {
                        var settings = activeParam.ToSettings(activeParam.LastCanvasWidth, activeParam.LastCanvasHeight);
                        ProcessSettings(settings, activeParam, buffer, hz, 0.0, startFrame, maxFrames, fadeOutFrames);
                    }
                }
            }
        }
        else if (fallbackSettings is not null)
        {
            ProcessSettings(fallbackSettings, null, buffer, hz, 0.0, startFrame, maxFrames, fadeOutFrames);
        }

        voices.Sort(static (a, b) => a.StartPosition.CompareTo(b.StartPosition));
    }

    private void ProcessSettings(
        DanmakuSettings baseSettings,
        DanmakuShapeParameter? parameter,
        DanmakuSoundBuffer buffer,
        int hz,
        double timelineOffsetSeconds,
        double startFrame,
        double maxFrames,
        double fadeOutFrames)
    {
        var customSound = baseSettings.GetSound(soundKind) with
        {
            CoalesceSimultaneous = effect.CoalesceSimultaneous,
            CoalesceIntervalSeconds = Math.Max(0.0, effect.CoalesceIntervalMs / 1000.0),
        };

        var settings = soundKind switch
        {
            DanmakuSoundKind.Fire => baseSettings with { FireSound = customSound },
            DanmakuSoundKind.Change => baseSettings with { ChangeSound = customSound },
            DanmakuSoundKind.Hit => baseSettings with { HitSound = customSound },
            DanmakuSoundKind.EnemyHit => baseSettings with { EnemyHitSound = customSound },
            DanmakuSoundKind.PlayerHit => baseSettings with { PlayerHitSound = customSound },
            DanmakuSoundKind.PlayerShot => baseSettings with { PlayerShotSound = customSound },
            _ => baseSettings with { VanishSound = customSound },
        };

        var simDuration = duration.TotalSeconds;
        var simulator = new DanmakuSimulator(settings)
        {
            MaxSimulationSeconds = Math.Max(1.0, simDuration + 1.0),
        };

        if (parameter is not null)
        {
            var fps = 60;
            var totalFrame = Math.Max(1, (int)Math.Ceiling(simDuration * fps));
            DanmakuLiveWiring.WireLiveValues(parameter, simulator, fps, totalFrame);
        }

        simulator.SeekTo(simDuration);

        var log = simulator.SoundLog;
        if (log.Count == 0) return;

        var random = new DeterministicRandom(settings.Seed + (int)soundKind * 1000);

        foreach (var e in log.Events)
        {
            if (e.Kind != soundKind) continue;

            var localTime = settings.TimeScale < 0
                ? (simDuration - e.TimeSeconds / Math.Max(0.01, Math.Abs(settings.TimeScale))) + effect.TimeOffset
                : (settings.TimeScale > 0 ? e.TimeSeconds / settings.TimeScale : e.TimeSeconds) + effect.TimeOffset;
            if (localTime < 0) continue;

            var time = timelineOffsetSeconds + localTime;
            if (time < 0 || time > duration.TotalSeconds) continue;

            var startPosition = (long)(time * hz) * 2;
            var semitones = effect.PitchJitter > 0 ? random.NextSymmetric(effect.PitchJitter) : 0;
            var pitchRatio = e.PitchRatio * DanmakuMath.SemitoneToRatio(semitones);

            voices.Add(new Voice(buffer, startPosition, pitchRatio, e.Volume, startFrame, maxFrames, fadeOutFrames));
        }
    }

    protected override void seek(long position)
    {
        // 巻き戻し時 (position == 0) かつ登録バージョンが変化していたら再準備
        if (position == 0)
        {
            var busVersion = DanmakuChannelBus.Version;
            if (busVersion != preparedVersion || voices.Count == 0)
            {
                PrepareVoices();
                preparedVersion = busVersion;
            }
        }
    }
}
