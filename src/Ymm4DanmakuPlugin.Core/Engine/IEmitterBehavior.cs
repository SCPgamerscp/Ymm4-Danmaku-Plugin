using Ymm4DanmakuPlugin.Core.Audio;
using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Mathematics;
using Ymm4DanmakuPlugin.Core.Model;

namespace Ymm4DanmakuPlugin.Core.Engine;

/// <summary>
/// エミッターの挙動 (いつ・どこへ・何発撃つか) を決める戦略インターフェース。
/// パターン生成・JSON・BulletML・Lua はすべてこの形に落とし込まれる。
/// </summary>
public interface IEmitterBehavior
{
    /// <summary>内部状態を初期化する (タイムラインの先頭へシークしたときに呼ばれる)。</summary>
    void Reset();

    /// <summary>1 ステップ進める。</summary>
    /// <param name="context">発射コンテキスト。</param>
    /// <param name="deltaTime">経過時間 (秒)。</param>
    void Update(EmitterContext context, double deltaTime);
}

/// <summary>エミッターがエンジンへアクセスするためのコンテキスト。</summary>
public sealed class EmitterContext(DanmakuEngine engine, int emitterIndex)
{
    public DanmakuEngine Engine { get; } = engine;

    public int EmitterIndex { get; } = emitterIndex;

    /// <summary>このエミッターの設定。</summary>
    public EmitterSettings Settings => Engine.Settings.Emitters[EmitterIndex];

    /// <summary>シミュレーション開始からの経過秒数。</summary>
    public double Time => Engine.CurrentTime;

    /// <summary>エミッターの現在位置 (公転を反映済み)。</summary>
    public Vec2 Position { get; internal set; }

    /// <summary>ターゲット (自機) の位置。</summary>
    public Vec2 TargetPosition => Engine.TargetPosition;

    public DeterministicRandom Random => Engine.Random;

    public SoundEventLog Sounds => Engine.SoundLog;

    /// <summary>弾を 1 発生成する。</summary>
    public Bullet? Spawn(in BulletSpawnRequest request) => Engine.Spawn(in request);

    /// <summary>ターゲットへの角度 (度) を返す。</summary>
    public double AngleToTarget() => (TargetPosition - Position).Degrees;

    // ---- 公転 & シード & スクリプト ----
    public double? EmitterOrbitRadius(double time) => Engine.Live.EmitterOrbitRadius?.Invoke(EmitterIndex, time);
    public double? EmitterOrbitSpeed(double time) => Engine.Live.EmitterOrbitSpeed?.Invoke(EmitterIndex, time);
    public double? EmitterOrbitPhase(double time) => Engine.Live.EmitterOrbitPhase?.Invoke(EmitterIndex, time);
    public int? EmitterSeedOffset(double time) => Engine.Live.EmitterSeedOffset?.Invoke(EmitterIndex, time);
    public double? EmitterScriptSpeedScale(double time) => Engine.Live.EmitterScriptSpeedScale?.Invoke(EmitterIndex, time);
    public double? EmitterScriptRank(double time) => Engine.Live.EmitterScriptRank?.Invoke(EmitterIndex, time);

    // ---- 発射パターン ----
    public double? EmitterAngle(double time) => Engine.Live.EmitterAngle?.Invoke(EmitterIndex, time);
    public double? EmitterAimRate(double time) => Engine.Live.EmitterAimRate?.Invoke(EmitterIndex, time);
    public int? EmitterWay(double time) => Engine.Live.EmitterWay?.Invoke(EmitterIndex, time);
    public int? EmitterStack(double time) => Engine.Live.EmitterStack?.Invoke(EmitterIndex, time);
    public double? EmitterStackSpeedStep(double time) => Engine.Live.EmitterStackSpeedStep?.Invoke(EmitterIndex, time);
    public double? EmitterStackAngleStep(double time) => Engine.Live.EmitterStackAngleStep?.Invoke(EmitterIndex, time);
    public double? EmitterSpreadAngle(double time) => Engine.Live.EmitterSpreadAngle?.Invoke(EmitterIndex, time);
    public double? EmitterAngleStepPerShot(double time) => Engine.Live.EmitterAngleStepPerShot?.Invoke(EmitterIndex, time);
    public double? EmitterAngleJitter(double time) => Engine.Live.EmitterAngleJitter?.Invoke(EmitterIndex, time);
    public double? EmitterFireInterval(double time) => Engine.Live.EmitterFireInterval?.Invoke(EmitterIndex, time);
    public int? EmitterBurstCount(double time) => Engine.Live.EmitterBurstCount?.Invoke(EmitterIndex, time);
    public double? EmitterBurstInterval(double time) => Engine.Live.EmitterBurstInterval?.Invoke(EmitterIndex, time);
    public double? EmitterBurstCooldown(double time) => Engine.Live.EmitterBurstCooldown?.Invoke(EmitterIndex, time);
    public double? EmitterStartTime(double time) => Engine.Live.EmitterStartTime?.Invoke(EmitterIndex, time);
    public double? EmitterEndTime(double time) => Engine.Live.EmitterEndTime?.Invoke(EmitterIndex, time);
    public double? EmitterSpawnRadius(double time) => Engine.Live.EmitterSpawnRadius?.Invoke(EmitterIndex, time);
    public double? EmitterSpawnJitter(double time) => Engine.Live.EmitterSpawnJitter?.Invoke(EmitterIndex, time);
    public double? EmitterWallWidth(double time) => Engine.Live.EmitterWallWidth?.Invoke(EmitterIndex, time);
    public double? EmitterLaserSpacing(double time) => Engine.Live.EmitterLaserSpacing?.Invoke(EmitterIndex, time);
    public double? EmitterWhipAmplitude(double time) => Engine.Live.EmitterWhipAmplitude?.Invoke(EmitterIndex, time);
    public double? EmitterWhipPeriod(double time) => Engine.Live.EmitterWhipPeriod?.Invoke(EmitterIndex, time);

    // ---- 弾の物理 ----
    public double? EmitterSpeed(double time) => Engine.Live.EmitterSpeed?.Invoke(EmitterIndex, time);
    public double? EmitterSpeedJitter(double time) => Engine.Live.EmitterSpeedJitter?.Invoke(EmitterIndex, time);
    public double? EmitterSpeedStep(double time) => Engine.Live.EmitterSpeedStep?.Invoke(EmitterIndex, time);
    public double? EmitterAcceleration(double time) => Engine.Live.EmitterAcceleration?.Invoke(EmitterIndex, time);
    public double? EmitterAngularVelocity(double time) => Engine.Live.EmitterAngularVelocity?.Invoke(EmitterIndex, time);
    public double? EmitterAngularVelocityJitter(double time) => Engine.Live.EmitterAngularVelocityJitter?.Invoke(EmitterIndex, time);
    public double? EmitterDamping(double time) => Engine.Live.EmitterDamping?.Invoke(EmitterIndex, time);
    public double? EmitterMinSpeed(double time) => Engine.Live.EmitterMinSpeed?.Invoke(EmitterIndex, time);
    public double? EmitterMaxSpeed(double time) => Engine.Live.EmitterMaxSpeed?.Invoke(EmitterIndex, time);
    public double? EmitterGravity(double time) => Engine.Live.EmitterGravity?.Invoke(EmitterIndex, time);
    public double? EmitterWind(double time) => Engine.Live.EmitterWind?.Invoke(EmitterIndex, time);
    public double? EmitterLifetime(double time) => Engine.Live.EmitterLifetime?.Invoke(EmitterIndex, time);
    public double? EmitterLifetimeJitter(double time) => Engine.Live.EmitterLifetimeJitter?.Invoke(EmitterIndex, time);
    public double? EmitterHomingTurnRate(double time) => Engine.Live.EmitterHomingTurnRate?.Invoke(EmitterIndex, time);
    public double? EmitterHomingDuration(double time) => Engine.Live.EmitterHomingDuration?.Invoke(EmitterIndex, time);
    public double? EmitterHomingDelay(double time) => Engine.Live.EmitterHomingDelay?.Invoke(EmitterIndex, time);
    public double? EmitterHitRadius(double time) => Engine.Live.EmitterHitRadius?.Invoke(EmitterIndex, time);

    // ---- 見た目 & 残像 ----
    public double? EmitterScale(double time) => Engine.Live.EmitterScale?.Invoke(EmitterIndex, time);
    public double? EmitterScaleJitter(double time) => Engine.Live.EmitterScaleJitter?.Invoke(EmitterIndex, time);
    public double? EmitterScaleVelocity(double time) => Engine.Live.EmitterScaleVelocity?.Invoke(EmitterIndex, time);
    public double? EmitterRotationVelocity(double time) => Engine.Live.EmitterRotationVelocity?.Invoke(EmitterIndex, time);
    public double? EmitterHueVelocity(double time) => Engine.Live.EmitterHueVelocity?.Invoke(EmitterIndex, time);
    public double? EmitterHueStep(double time) => Engine.Live.EmitterHueStep?.Invoke(EmitterIndex, time);
    public double? EmitterOpacity(double time) => Engine.Live.EmitterOpacity?.Invoke(EmitterIndex, time);
    public double? EmitterGlowIntensity(double time) => Engine.Live.EmitterGlowIntensity?.Invoke(EmitterIndex, time);
    public double? EmitterFadeInDuration(double time) => Engine.Live.EmitterFadeInDuration?.Invoke(EmitterIndex, time);
    public double? EmitterFadeOutDuration(double time) => Engine.Live.EmitterFadeOutDuration?.Invoke(EmitterIndex, time);
    public int? EmitterTrailLength(double time) => Engine.Live.EmitterTrailLength?.Invoke(EmitterIndex, time);
    public double? EmitterTrailInterval(double time) => Engine.Live.EmitterTrailInterval?.Invoke(EmitterIndex, time);
    public double? EmitterTrailFade(double time) => Engine.Live.EmitterTrailFade?.Invoke(EmitterIndex, time);
    public double? EmitterTrailScale(double time) => Engine.Live.EmitterTrailScale?.Invoke(EmitterIndex, time);

    // ---- 分裂 ----
    public int? EmitterSplitCount(double time) => Engine.Live.EmitterSplitCount?.Invoke(EmitterIndex, time);
    public double? EmitterSplitSpread(double time) => Engine.Live.EmitterSplitSpread?.Invoke(EmitterIndex, time);
    public double? EmitterSplitSpeed(double time) => Engine.Live.EmitterSplitSpeed?.Invoke(EmitterIndex, time);
    public double? EmitterSplitScaleFactor(double time) => Engine.Live.EmitterSplitScaleFactor?.Invoke(EmitterIndex, time);
    public double? EmitterSplitDelay(double time) => Engine.Live.EmitterSplitDelay?.Invoke(EmitterIndex, time);
    public int? EmitterSplitMaxGeneration(double time) => Engine.Live.EmitterSplitMaxGeneration?.Invoke(EmitterIndex, time);
}
