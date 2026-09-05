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
        double FadeOutFrames,
        object? SourceKey);

    private readonly DanmakuSingleSoundAudioEffectBase effect;
    private readonly DanmakuSoundKind soundKind;
    private readonly TimeSpan duration;
    private readonly List<Voice> voices = [];

    private DanmakuSoundBuffer? soundBuffer;
    private object? assignedSourceKey;
    private bool voicesPrepared;
    private bool isSpanningPlayback;
    private double spanningAnchorStart;
    private readonly HashSet<object> preparedSourceKeys = [];
    private readonly Dictionary<object, double> preparedDurations = [];

    public DanmakuSingleSoundProcessor(
        DanmakuSingleSoundAudioEffectBase effect,
        DanmakuSoundKind soundKind,
        TimeSpan duration)
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
        Array.Clear(destBuffer, offset, count);

        // プレビューは音声を映像より先にバッファする。再生中に Version 変化で voices を
        // 作り直すと、弾幕1の音が続き、弾幕2の先頭が欠ける。準備は初回と巻き戻しだけ行う。
        // 伸ばした1本の音声は、後から長さが確定した弾幕を足すだけにする。組み直さない。
        if (!voicesPrepared)
        {
            PrepareVoices();
        }
        else if (isSpanningPlayback)
        {
            AppendSpanningVoices();
        }
        else
        {
            PromoteToSpanningIfNeeded();
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

        var hz = Hz;
        var startFrame = Math.Max(0.0, effect.TrimStart * hz);
        var totalAvailableFrames = Math.Max(0.0, buffer.FrameCount - startFrame);
        var maxFrames = effect.PlayDuration > 0
            ? Math.Min(totalAvailableFrames, effect.PlayDuration * hz)
            : totalAvailableFrames;
        var fadeOutFrames = Math.Max(0.0, effect.FadeOut * hz);

        if (registrations.Count > 0)
        {
            if (LooksLikeSpanning(registrations))
            {
                MixSpanningRegistrations(registrations, buffer, hz, startFrame, maxFrames, fadeOutFrames, reset: true);
            }
            else
            {
                // 個別音声は同じチャンネルの弾幕を排他的に確保する。
                // パラメータ生成時の登録があるので、映像 Update を待たずに弾幕2へ割り当てられる。
                var reg = DanmakuChannelBus.ClaimRegistration(
                    effect.Channel,
                    soundKind,
                    this,
                    duration.TotalSeconds,
                    assignedSourceKey);
                if (reg is null)
                {
                    // 自分の弾幕がまだ連絡簿に無い。次の read で再試行する。
                    return;
                }

                // 確保したあとに長さが分かり、伸ばした1本だと分かったら跨ぎへ切り替える。
                // まだ voices を組んでいないので、弾幕1を10秒ぶんシミュレートせずに済む。
                assignedSourceKey = reg.SourceKey;
                registrations = DanmakuChannelBus.GetRegistrations(effect.Channel);
                if (LooksLikeSpanning(registrations))
                {
                    MixSpanningRegistrations(registrations, buffer, hz, startFrame, maxFrames, fadeOutFrames, reset: true);
                    return;
                }

                isSpanningPlayback = false;
                voices.Clear();
                preparedSourceKeys.Clear();
                preparedDurations.Clear();
                preparedSourceKeys.Add(reg.SourceKey);
                preparedDurations[reg.SourceKey] = reg.TimelineDurationSeconds;
                voicesPrepared = true;

                if (reg.ParameterRef.TryGetTarget(out var parameter))
                {
                    var settings = parameter.ToSettings(parameter.LastCanvasWidth, parameter.LastCanvasHeight);
                    // 長さ未確定の個別音声は、この音声アイテム自身の長さで鳴らす。
                    // 伸ばした1本だと後で分かったら、このキーの voices だけ作り直す。
                    ProcessSettings(
                        settings,
                        parameter,
                        buffer,
                        hz,
                        0.0,
                        startFrame,
                        maxFrames,
                        fadeOutFrames,
                        reg.TimelineDurationSeconds,
                        reg.SourceKey);
                }
            }
        }
        else if (fallbackSettings is not null)
        {
            isSpanningPlayback = false;
            voices.Clear();
            preparedSourceKeys.Clear();
            preparedDurations.Clear();
            voicesPrepared = true;
            ProcessSettings(fallbackSettings, null, buffer, hz, 0.0, startFrame, maxFrames, fadeOutFrames, duration.TotalSeconds, null);
        }

        voices.Sort(static (a, b) => a.StartPosition.CompareTo(b.StartPosition));
    }

    private void AppendSpanningVoices()
    {
        var buffer = EnsureSoundBuffer();
        if (buffer is null) return;

        var registrations = DanmakuChannelBus.GetRegistrations(effect.Channel);
        if (registrations.Count == 0) return;

        var hz = Hz;
        var startFrame = Math.Max(0.0, effect.TrimStart * hz);
        var totalAvailableFrames = Math.Max(0.0, buffer.FrameCount - startFrame);
        var maxFrames = effect.PlayDuration > 0
            ? Math.Min(totalAvailableFrames, effect.PlayDuration * hz)
            : totalAvailableFrames;
        var fadeOutFrames = Math.Max(0.0, effect.FadeOut * hz);

        MixSpanningRegistrations(registrations, buffer, hz, startFrame, maxFrames, fadeOutFrames, reset: false);
    }

    private void PromoteToSpanningIfNeeded()
    {
        var registrations = DanmakuChannelBus.GetRegistrations(effect.Channel);
        if (!LooksLikeSpanning(registrations)) return;

        var claimed = registrations.FirstOrDefault(
            registration => preparedSourceKeys.Contains(registration.SourceKey));
        if (claimed is not null)
        {
            spanningAnchorStart = claimed.TimelineStartSeconds;
            var preparedDuration = preparedDurations.TryGetValue(claimed.SourceKey, out var durationSeconds)
                ? durationSeconds
                : 0;
            // 未確定のまま音声全長で組んだ弾幕1は、長さが付いたときこのキーだけ作り直す。
            // 他の弾幕の voices は残す。Version では組み直さない。
            if (DanmakuSpanningAudio.ShouldResim(preparedDuration, claimed.TimelineDurationSeconds) ||
                DanmakuSpanningAudio.ShouldDropUntimed(preparedDuration, claimed.TimelineDurationSeconds, spanning: true))
            {
                RemoveVoicesFor(claimed.SourceKey);
                preparedSourceKeys.Remove(claimed.SourceKey);
                preparedDurations.Remove(claimed.SourceKey);
            }
        }

        AppendSpanningVoices();
    }

    private bool LooksLikeSpanning(IReadOnlyList<DanmakuChannelRegistration> registrations)
    {
        var ready = registrations
            .Where(registration => registration.TimelineDurationSeconds > 0)
            .ToArray();
        var longestReady = ready.Length > 0
            ? ready.Max(registration => registration.TimelineDurationSeconds)
            : 0;
        var minStart = ready.Length > 0
            ? ready.Min(registration => registration.TimelineStartSeconds)
            : 0;
        var maxEnd = ready.Length > 0
            ? ready.Max(registration => registration.TimelineStartSeconds + registration.TimelineDurationSeconds)
            : 0;
        return DanmakuSpanningAudio.LooksSpanning(
            duration.TotalSeconds,
            registrations.Count,
            longestReady,
            maxEnd - minStart,
            ready.Length);
    }

    private void MixSpanningRegistrations(
        IReadOnlyList<DanmakuChannelRegistration> registrations,
        DanmakuSoundBuffer buffer,
        int hz,
        double startFrame,
        double maxFrames,
        double fadeOutFrames,
        bool reset)
    {
        var readyRegistrations = registrations
            .Where(registration => registration.TimelineDurationSeconds > 0)
            .ToArray();
        if (readyRegistrations.Length == 0)
        {
            return;
        }

        DanmakuChannelBus.ReleaseRegistration(this);
        assignedSourceKey = null;
        isSpanningPlayback = true;

        if (reset)
        {
            voices.Clear();
            preparedSourceKeys.Clear();
            preparedDurations.Clear();
            spanningAnchorStart = readyRegistrations.Min(registration => registration.TimelineStartSeconds);
            voicesPrepared = true;
        }
        else if (preparedSourceKeys.Count == 0)
        {
            spanningAnchorStart = readyRegistrations.Min(registration => registration.TimelineStartSeconds);
            voicesPrepared = true;
        }

        // 先に長さ0で組んだキーは、本物の長さが付いたときこのキーだけ作り直す。
        foreach (var reg in readyRegistrations)
        {
            if (!preparedSourceKeys.Contains(reg.SourceKey)) continue;
            var preparedDuration = preparedDurations.TryGetValue(reg.SourceKey, out var durationSeconds)
                ? durationSeconds
                : 0;
            if (!DanmakuSpanningAudio.ShouldResim(preparedDuration, reg.TimelineDurationSeconds)) continue;
            RemoveVoicesFor(reg.SourceKey);
            preparedSourceKeys.Remove(reg.SourceKey);
            preparedDurations.Remove(reg.SourceKey);
        }

        var added = false;
        foreach (var reg in readyRegistrations)
        {
            if (!preparedSourceKeys.Add(reg.SourceKey)) continue;
            if (!reg.ParameterRef.TryGetTarget(out var parameter)) continue;

            var timelineOffset = Math.Max(0.0, reg.TimelineStartSeconds - spanningAnchorStart);
            if (timelineOffset >= duration.TotalSeconds) continue;

            var settings = parameter.ToSettings(parameter.LastCanvasWidth, parameter.LastCanvasHeight);
            ProcessSettings(
                settings,
                parameter,
                buffer,
                hz,
                timelineOffset,
                startFrame,
                maxFrames,
                fadeOutFrames,
                reg.TimelineDurationSeconds,
                reg.SourceKey);
            preparedDurations[reg.SourceKey] = reg.TimelineDurationSeconds;
            added = true;
        }

        if (added)
        {
            voices.Sort(static (a, b) => a.StartPosition.CompareTo(b.StartPosition));
        }
    }

    private void ProcessSettings(
        DanmakuSettings baseSettings,
        DanmakuShapeParameter? parameter,
        DanmakuSoundBuffer buffer,
        int hz,
        double timelineOffsetSeconds,
        double startFrame,
        double maxFrames,
        double fadeOutFrames,
        double itemDurationSeconds,
        object? sourceKey)
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

        // 跨ぎ再生で長さ未確定なら、音声全長で弾幕1を埋めない。個別の短い音声はその音声の長さで鳴らす。
        if (itemDurationSeconds <= 0 &&
            (isSpanningPlayback || LooksLikeSpanning(DanmakuChannelBus.GetRegistrations(effect.Channel))))
        {
            return;
        }

        var simDuration = itemDurationSeconds > 0 ? itemDurationSeconds : duration.TotalSeconds;
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

            voices.Add(new Voice(buffer, startPosition, pitchRatio, e.Volume, startFrame, maxFrames, fadeOutFrames, sourceKey));
        }
    }

    protected override void seek(long position)
    {
        // 巻き戻し時だけ再準備する。途中シークで作り直すと連続再生の音が途切れる。
        if (position == 0)
        {
            voicesPrepared = false;
            isSpanningPlayback = false;
            voices.Clear();
            preparedSourceKeys.Clear();
            preparedDurations.Clear();
            PrepareVoices();
        }
    }

    private void RemoveVoicesFor(object sourceKey)
    {
        voices.RemoveAll(voice => ReferenceEquals(voice.SourceKey, sourceKey));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DanmakuChannelBus.ReleaseRegistration(this);
        }

        base.Dispose(disposing);
    }
}
