using System.Numerics;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Engine;
using Ymm4DanmakuPlugin.Core.Mathematics;
using Ymm4DanmakuPlugin.Core.Rendering;
using Ymm4DanmakuPlugin.Parameters;
using Ymm4DanmakuPlugin.Rendering;

namespace Ymm4DanmakuPlugin;

/// <summary>
/// 弾幕図形の描画ソース。
/// <para>
/// YMM4 は表示するフレームが変わるたびに <see cref="Update"/> を呼ぶ。
/// ここで
/// </para>
/// <list type="number">
/// <item>編集項目 → コアエンジン設定へ変換し、必要なら再構築する</item>
/// <item>キーフレーム値の供給関数 (<see cref="LiveValueSource"/>) を張り替える</item>
/// <item>当該フレームまでシミュレーションをシークする</item>
/// <item>描画データを組み立てて Direct2D へ描く</item>
/// </list>
/// <para>
/// <b>プレビュー上のドラッグ</b>は <see cref="IShapeSource2.Controllers"/> で実現している。
/// エミッター位置とターゲット位置に制御点を出し、ドラッグ量を
/// <c>Animation.AddToEachValues</c> でキーフレーム値へ加算する。
/// </para>
/// </summary>
public sealed class DanmakuShapeSource : IShapeSource2
{
    private readonly IGraphicsDevicesAndContext devices;
    private readonly DanmakuShapeParameter parameter;
    private readonly DanmakuRenderer renderer;
    private readonly RenderBatchBuilder batchBuilder = new() { MaxSpriteSlots = SpriteSlots.Capacity };
    private readonly DisposeCollector disposer = new();

    private DanmakuSimulator? simulator;
    private ID2D1CommandList? output;
    private ID2D1CommandList? emptyOutput;

    /// <summary>直近の描画で使った情報 (再描画の省略判定に使う)。</summary>
    private int lastFrame = -1;
    private int lastFps;
    private int lastCanvasWidth;
    private int lastCanvasHeight;
    private int lastTotalFrame;

    public DanmakuShapeSource(IGraphicsDevicesAndContext devices, DanmakuShapeParameter parameter)
    {
        this.devices = devices;
        this.parameter = parameter;

        renderer = new DanmakuRenderer(devices);
        disposer.Collect(renderer);

        // 音声エフェクト側から設定を引けるように登録する (弱参照なので解放を妨げない)
        Audio.DanmakuChannelBus.Register(parameter);
    }

    /// <summary>YMM4 が合成に使う描画結果。</summary>
    public ID2D1Image Output => output ?? GetEmptyOutput();

    /// <summary>プレビューエリアに表示する制御点。</summary>
    public IEnumerable<VideoController> Controllers => BuildControllers();

    /// <summary>直近のシミュレーションで生存していた弾数 (デバッグ用)。</summary>
    public int LastBulletCount { get; private set; }

    public void Update(TimelineItemSourceDescription description)
    {
        var canvasWidth = Math.Max(1, description.ScreenSize.Width);
        var canvasHeight = Math.Max(1, description.ScreenSize.Height);
        var fps = description.FPS > 0 ? description.FPS : 60;
        var frame = description.ItemPosition.Frame;
        var totalFrame = Math.Max(1, description.ItemDuration.Frame);

        // 音声側が同じ条件でシミュレーションを再現できるよう記録しておく
        parameter.LastCanvasWidth = canvasWidth;
        parameter.LastCanvasHeight = canvasHeight;

        var settings = parameter.ToSettings(canvasWidth, canvasHeight);

        if (simulator is null)
        {
            simulator = new DanmakuSimulator(settings);
        }
        else
        {
            // 構造が変わっていなければ再構築されない (Configure 内で署名比較している)
            simulator.Configure(settings);
        }

        // --- キーフレーム値の供給関数を張り替える ---
        // 毎フレーム作り直しても設定署名は変化しないため、シミュレーションは維持される。
        WireLiveValues(simulator, fps, totalFrame);

        // --- ユーザー指定画像をスロットへ読み込む ---
        LoadCustomImages();

        // キーフレームやスライダーの編集が一時停止中に行われても確実に最新の弾幕状態を反映するため、
        // 常に先頭から現在フレームまで確定的にシミュレーションを再現して描画する
        simulator.Reset();
        simulator.SeekToFrame(frame, fps);
        LastBulletCount = simulator.Bullets.Count;

        lastFrame = frame;
        lastFps = fps;
        lastCanvasWidth = canvasWidth;
        lastCanvasHeight = canvasHeight;
        lastTotalFrame = totalFrame;

        var globalOpacity = Math.Clamp(parameter.GlobalOpacity.GetValue(frame, totalFrame, fps) / 100.0, 0.0, 1.0);
        batchBuilder.Build(simulator.Bullets, GetAppearance, globalOpacity);
        output = renderer.Render(batchBuilder, parameter.GetGlowIntensity);
    }

    /// <summary>
    /// キーフレームで動く値をエンジンへ供給する。
    /// <para>
    /// 渡す関数は「時刻のみに依存する純粋関数」でなければならない。
    /// <c>Animation.GetValue</c> はまさにその条件を満たすため、
    /// どのフレームへシークしても弾幕は同一に再現される。
    /// </para>
    /// </summary>
    private void WireLiveValues(DanmakuSimulator sim, int fps, int totalFrame)
    {
        var emitters = parameter.Emitters;

        sim.Live.EmitterPosition = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return new Vec2(
                emitter.X.GetValue(frame, totalFrame, fps),
                emitter.Y.GetValue(frame, totalFrame, fps));
        };

        sim.Live.TargetPosition = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return new Vec2(
                parameter.TargetX.GetValue(frame, totalFrame, fps),
                parameter.TargetY.GetValue(frame, totalFrame, fps));
        };

        sim.Live.EmitterOrbitRadius = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.OrbitRadius.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterOrbitSpeed = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.OrbitSpeed.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterOrbitPhase = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.OrbitPhase.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterSeedOffset = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return (int)Math.Round(emitter.SeedOffset.GetValue(frame, totalFrame, fps));
        };

        sim.Live.EmitterScriptSpeedScale = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.ScriptSpeedScale.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterScriptRank = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.ScriptRank.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterAngle = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.BaseAngle.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterWay = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return Math.Max(0, (int)Math.Round(emitter.Way.GetValue(frame, totalFrame, fps)));
        };

        sim.Live.EmitterStack = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return Math.Max(0, (int)Math.Round(emitter.Stack.GetValue(frame, totalFrame, fps)));
        };

        sim.Live.EmitterStackSpeedStep = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.StackSpeedStep.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterStackAngleStep = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.StackAngleStep.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterSpreadAngle = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.SpreadAngle.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterAngleStepPerShot = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.AngleStepPerShot.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterAngleJitter = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.AngleJitter.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterFireInterval = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.FireInterval.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterBurstCount = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return Math.Max(0, (int)Math.Round(emitter.BurstCount.GetValue(frame, totalFrame, fps)));
        };

        sim.Live.EmitterBurstInterval = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.BurstInterval.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterBurstCooldown = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.BurstCooldown.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterStartTime = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.StartTime.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterEndTime = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.EndTime.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterSpawnRadius = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.SpawnRadius.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterSpawnJitter = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.SpawnJitter.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterWallWidth = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.WallWidth.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterLaserSpacing = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.LaserSpacing.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterWhipAmplitude = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.WhipAmplitude.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterWhipPeriod = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.WhipPeriod.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterSpeed = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.Speed.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterSpeedJitter = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.SpeedJitter.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterSpeedStep = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.SpeedStep.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterAcceleration = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.Acceleration.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterAngularVelocity = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.AngularVelocity.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterAngularVelocityJitter = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.AngularVelocityJitter.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterDamping = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.Damping.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterMinSpeed = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.MinSpeed.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterMaxSpeed = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.MaxSpeed.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterGravity = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.Gravity.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterWind = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.Wind.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterLifetime = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.Lifetime.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterLifetimeJitter = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.LifetimeJitter.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterHomingTurnRate = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.HomingTurnRate.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterHomingDuration = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.HomingDuration.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterHomingDelay = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.HomingDelay.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterHitRadius = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.HitRadius.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterScale = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.Scale.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterScaleJitter = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.ScaleJitter.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterScaleVelocity = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.ScaleVelocity.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterRotationVelocity = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.RotationVelocity.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterHueVelocity = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.HueVelocity.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterHueStep = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.HueStep.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterOpacity = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.Opacity.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterGlowIntensity = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.GlowIntensity.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterFadeInDuration = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.FadeInDuration.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterFadeOutDuration = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.FadeOutDuration.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterTrailLength = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return Math.Max(0, (int)Math.Round(emitter.TrailLength.GetValue(frame, totalFrame, fps)));
        };

        sim.Live.EmitterTrailInterval = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.TrailInterval.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterTrailFade = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.TrailFade.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterTrailScale = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.TrailScale.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterSplitCount = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return Math.Max(0, (int)Math.Round(emitter.SplitCount.GetValue(frame, totalFrame, fps)));
        };

        sim.Live.EmitterSplitSpread = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.SplitSpread.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterSplitSpeed = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.SplitSpeed.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterSplitScaleFactor = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.SplitScaleFactor.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterSplitDelay = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return emitter.SplitDelay.GetValue(frame, totalFrame, fps);
        };

        sim.Live.EmitterSplitMaxGeneration = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return Math.Max(0, (int)Math.Round(emitter.SplitMaxGeneration.GetValue(frame, totalFrame, fps)));
        };

        sim.Live.GlobalOpacity = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return Math.Clamp(parameter.GlobalOpacity.GetValue(frame, totalFrame, fps) / 100.0, 0.0, 1.0);
        };

        sim.Live.TimeScale = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.TimeScale.GetValue(frame, totalFrame, fps);
        };

        sim.Live.BoundsMargin = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.BoundsMargin.GetValue(frame, totalFrame, fps);
        };

        sim.Live.TargetRadius = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.TargetRadius.GetValue(frame, totalFrame, fps);
        };

        sim.Live.HitEffectCount = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return Math.Max(0, (int)Math.Round(parameter.HitEffectCount.GetValue(frame, totalFrame, fps)));
        };

        sim.Live.HitEffectSpeed = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.HitEffectSpeed.GetValue(frame, totalFrame, fps);
        };

        sim.Live.HitEffectLifetime = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return parameter.HitEffectLifetime.GetValue(frame, totalFrame, fps);
        };
    }

    /// <summary>
    /// 秒をフレーム番号へ変換する。
    /// </summary>
    private static int TimeToFrame(double timeSeconds, int fps, int totalFrame) =>
        Math.Clamp((int)Math.Round(timeSeconds * fps), 0, Math.Max(0, totalFrame - 1));

    /// <summary>弾の描画用プロパティを引く (バッチ生成用)。</summary>
    private BulletAppearance GetAppearance(int emitterIndex)
    {
        if (simulator is null) return new BulletAppearance();

        var settings = simulator.Settings;
        if (emitterIndex < 0 || emitterIndex >= settings.Emitters.Length)
            return settings.Emitters.Length > 0 ? settings.Emitters[0].Appearance : new BulletAppearance();

        return settings.Emitters[emitterIndex].Appearance;
    }

    /// <summary>各エミッターのユーザー指定画像をスプライトスロットへ読み込む。</summary>
    private void LoadCustomImages()
    {
        var emitters = parameter.Emitters;
        for (var i = 0; i < emitters.Count && i < DanmakuShapeParameter.MaxEmitters; i++)
        {
            var emitter = emitters[i];
            renderer.Sprites.SetCustomImage(
                SpriteSlots.CustomSlotOf(i),
                emitter.HasCustomImage ? emitter.ImagePath : null);
        }
    }

    /// <summary>
    /// プレビューエリア上の制御点を組み立てる。
    /// <para>
    /// エミッターごとに 1 点、当たり判定が有効ならターゲットにも 1 点を出す。
    /// ドラッグ量はスクリーン座標の差分なので、そのままキーフレーム値へ加算すればよい。
    /// </para>
    /// </summary>
    private IEnumerable<VideoController> BuildControllers()
    {
        var emitters = parameter.Emitters;
        var fps = lastFps > 0 ? lastFps : 60;
        var frame = Math.Max(0, lastFrame);
        var totalFrame = Math.Max(1, lastTotalFrame > 0 ? lastTotalFrame : frame + 1);

        foreach (var emitter in emitters)
        {
            var captured = emitter;
            var position = new Vector3(
                (float)captured.X.GetValue(frame, totalFrame, fps),
                (float)captured.Y.GetValue(frame, totalFrame, fps),
                0);

            var point = new ControllerPoint(position, arg =>
            {
                ApplyControllerDrag(captured.X, arg.Delta.X, frame, totalFrame);
                ApplyControllerDrag(captured.Y, arg.Delta.Y, frame, totalFrame);
            })
            {
                Shape = VideoControllerPointShape.Circle,
            };

            yield return new VideoController([point])
            {
                Connection = VideoControllerPointConnection.None,
            };
        }

        if (!parameter.CollisionEnabled) yield break;

        var targetPosition = new Vector3(
            (float)parameter.TargetX.GetValue(frame, totalFrame, fps),
            (float)parameter.TargetY.GetValue(frame, totalFrame, fps),
            0);

        var targetPoint = new ControllerPoint(targetPosition, arg =>
        {
            ApplyControllerDrag(parameter.TargetX, arg.Delta.X, frame, totalFrame);
            ApplyControllerDrag(parameter.TargetY, arg.Delta.Y, frame, totalFrame);
        })
        {
            Shape = VideoControllerPointShape.Square,
        };

        yield return new VideoController([targetPoint])
        {
            Connection = VideoControllerPointConnection.None,
        };
    }

    /// <summary>
    /// プレビュー上の制御点ドラッグ時、現在フレームに応じた適切なキーフレーム値を更新する。
    /// </summary>
    private static void ApplyControllerDrag(Animation anim, double delta, int frame, int totalFrame)
    {
        if (anim.Values.Count == 0) return;

        if (anim.AnimationType == AnimationType.なし)
        {
            // 固定値の場合は全体を加算
            anim.AddToEachValues(delta);
        }
        else if (anim.Values.Count == 2)
        {
            // 直線移動・加減速など (From -> To)
            if (frame <= 0)
            {
                // 先頭フレームでは開始位置 (From) のみを移動
                anim.Values[0].Value += delta;
            }
            else if (frame >= totalFrame - 1)
            {
                // 末尾フレームでは終了位置 (To) のみを移動
                anim.Values[1].Value += delta;
            }
            else
            {
                // 中間フレームでは現在比率に応じて移動
                var ratio = (double)frame / Math.Max(1, totalFrame - 1);
                anim.Values[0].Value += delta * (1.0 - ratio);
                anim.Values[1].Value += delta * ratio;
            }
        }
        else
        {
            anim.AddToEachValues(delta);
        }
    }

    /// <summary>
    /// 弾が 1 つも無いときに返す空の描画結果。
    /// <para>
    /// <see cref="Output"/> は null を返せないため、空の CommandList を 1 つだけ用意しておく。
    /// </para>
    /// </summary>
    private ID2D1CommandList GetEmptyOutput()
    {
        if (emptyOutput is not null) return emptyOutput;

        var dc = devices.DeviceContext;
        emptyOutput = dc.CreateCommandList();
        disposer.Collect(emptyOutput);

        var previousTarget = dc.Target;
        dc.Target = emptyOutput;
        dc.BeginDraw();
        dc.Clear(null);
        dc.EndDraw();
        dc.Target = previousTarget;
        emptyOutput.Close();

        return emptyOutput;
    }

    public void Dispose()
    {
        Audio.DanmakuChannelBus.Unregister(parameter);
        simulator = null;
        output = null;
        emptyOutput = null;
        disposer.DisposeAndClear();
    }
}
