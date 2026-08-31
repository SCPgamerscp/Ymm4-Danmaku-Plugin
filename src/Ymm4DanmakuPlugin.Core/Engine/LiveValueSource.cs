using Ymm4DanmakuPlugin.Core.Mathematics;

namespace Ymm4DanmakuPlugin.Core.Engine;

/// <summary>
/// 自機 (ターゲット) の当たり判定領域。
/// </summary>
public readonly record struct TargetHitbox(Vec2 Position, double Radius, int TargetId = 0);

/// <summary>
/// エネミー (ボス) の当たり判定領域。
/// </summary>
public readonly record struct EnemyHitbox(Vec2 Position, double Radius, int EmitterIndex = 0, int Channel = 0);

/// <summary>
/// キーフレーム (YMM4 の <c>Animation</c>) によって時間変化する値をエンジンへ供給する差し込み口。
/// <para>
/// 弾幕の全パラメータをタイムライン上でアニメーション可能にするため、
/// 毎ステップの時刻に応じた最新の値を関数経由で取得する。
/// </para>
/// </summary>
public sealed class LiveValueSource
{
    // ---- エミッター位置・ターゲット位置 ----
    public Func<int, double, Vec2?>? EmitterPosition { get; set; }
    public Func<double, Vec2?>? TargetPosition { get; set; }
    public Func<double, double?>? TargetScale { get; set; }
    public Func<double, double?>? TargetRotation { get; set; }
    public Func<double, double?>? TargetOpacity { get; set; }

    // ---- 公転 & シード & 魔法陣 ----
    public Func<int, double, double?>? EmitterOrbitRadius { get; set; }
    public Func<int, double, double?>? EmitterOrbitSpeed { get; set; }
    public Func<int, double, double?>? EmitterOrbitPhase { get; set; }
    public Func<int, double, double?>? EmitterMagicCircleRotationSpeed { get; set; }
    public Func<int, double, int?>? EmitterSeedOffset { get; set; }

    // ---- 外部スクリプト ----
    public Func<int, double, double?>? EmitterScriptSpeedScale { get; set; }
    public Func<int, double, double?>? EmitterScriptRank { get; set; }

    // ---- 発射パターン ----
    public Func<int, double, double?>? EmitterAngle { get; set; }
    public Func<int, double, double?>? EmitterAimRate { get; set; }
    public Func<int, double, int?>? EmitterWay { get; set; }
    public Func<int, double, int?>? EmitterStack { get; set; }
    public Func<int, double, double?>? EmitterStackSpeedStep { get; set; }
    public Func<int, double, double?>? EmitterStackAngleStep { get; set; }
    public Func<int, double, double?>? EmitterSpreadAngle { get; set; }
    public Func<int, double, double?>? EmitterAngleStepPerShot { get; set; }
    public Func<int, double, double?>? EmitterAngleJitter { get; set; }
    public Func<int, double, double?>? EmitterFireInterval { get; set; }
    public Func<int, double, int?>? EmitterBurstCount { get; set; }
    public Func<int, double, double?>? EmitterBurstInterval { get; set; }
    public Func<int, double, double?>? EmitterBurstCooldown { get; set; }
    public Func<int, double, double?>? EmitterStartTime { get; set; }
    public Func<int, double, double?>? EmitterEndTime { get; set; }
    public Func<int, double, double?>? EmitterSpawnRadius { get; set; }
    public Func<int, double, double?>? EmitterSpawnJitter { get; set; }
    public Func<int, double, double?>? EmitterWallWidth { get; set; }
    public Func<int, double, double?>? EmitterLaserSpacing { get; set; }
    public Func<int, double, double?>? EmitterWhipAmplitude { get; set; }
    public Func<int, double, double?>? EmitterWhipPeriod { get; set; }

    // ---- 弾の物理 ----
    public Func<int, double, double?>? EmitterSpeed { get; set; }
    public Func<int, double, double?>? EmitterSpeedJitter { get; set; }
    public Func<int, double, double?>? EmitterSpeedStep { get; set; }
    public Func<int, double, double?>? EmitterAcceleration { get; set; }
    public Func<int, double, double?>? EmitterAngularVelocity { get; set; }
    public Func<int, double, double?>? EmitterAngularVelocityJitter { get; set; }
    public Func<int, double, double?>? EmitterDamping { get; set; }
    public Func<int, double, double?>? EmitterMinSpeed { get; set; }
    public Func<int, double, double?>? EmitterMaxSpeed { get; set; }
    public Func<int, double, double?>? EmitterGravity { get; set; }
    public Func<int, double, double?>? EmitterWind { get; set; }
    public Func<int, double, double?>? EmitterLifetime { get; set; }
    public Func<int, double, double?>? EmitterLifetimeJitter { get; set; }
    public Func<int, double, double?>? EmitterHomingTurnRate { get; set; }
    public Func<int, double, double?>? EmitterHomingDuration { get; set; }
    public Func<int, double, double?>? EmitterHomingDelay { get; set; }
    public Func<int, double, double?>? EmitterHitRadius { get; set; }

    // ---- 見た目 & 残像 ----
    public Func<int, double, double?>? EmitterScale { get; set; }
    public Func<int, double, double?>? EmitterScaleJitter { get; set; }
    public Func<int, double, double?>? EmitterScaleVelocity { get; set; }
    public Func<int, double, double?>? EmitterRotationVelocity { get; set; }
    public Func<int, double, double?>? EmitterHueVelocity { get; set; }
    public Func<int, double, double?>? EmitterHueStep { get; set; }
    public Func<int, double, double?>? EmitterOpacity { get; set; }
    public Func<int, double, double?>? EmitterGlowIntensity { get; set; }
    public Func<int, double, double?>? EmitterFadeInDuration { get; set; }
    public Func<int, double, double?>? EmitterFadeOutDuration { get; set; }
    public Func<int, double, int?>? EmitterTrailLength { get; set; }
    public Func<int, double, double?>? EmitterTrailInterval { get; set; }
    public Func<int, double, double?>? EmitterTrailFade { get; set; }
    public Func<int, double, double?>? EmitterTrailScale { get; set; }

    // ---- 分裂 ----
    public Func<int, double, int?>? EmitterSplitCount { get; set; }
    public Func<int, double, double?>? EmitterSplitSpread { get; set; }
    public Func<int, double, double?>? EmitterSplitSpeed { get; set; }
    public Func<int, double, double?>? EmitterSplitScaleFactor { get; set; }
    public Func<int, double, double?>? EmitterSplitDelay { get; set; }
    public Func<int, double, int?>? EmitterSplitMaxGeneration { get; set; }

    // ---- 全体設定 ----
    public Func<double, double?>? GlobalOpacity { get; set; }
    public Func<double, double?>? TimeScale { get; set; }
    // ---- エネミー (ボス) 位置 & 当たり判定 ----
    public Func<double, Vec2?>? EnemyPosition { get; set; }
    public Func<double, double?>? EnemyRadius { get; set; }

    // ---- 自機ショット ----
    public Func<double, int?>? PlayerShotWay { get; set; }
    public Func<double, double?>? PlayerShotInterval { get; set; }
    public Func<double, double?>? PlayerShotSpeed { get; set; }
    public Func<double, double?>? PlayerShotSpread { get; set; }
    public Func<double, double?>? PlayerShotScale { get; set; }
    public Func<double, double?>? PlayerShotHitRadius { get; set; }

    // ---- ボス体力バー (HP ゲージ) ----
    public Func<double, double?>? BossHp { get; set; }
    public Func<double, double?>? HpBarRadius { get; set; }
    public Func<double, double?>? HpBarWidth { get; set; }
    public Func<double, double?>? HpBarHeight { get; set; }
    public Func<double, double?>? HpBarX { get; set; }
    public Func<double, double?>? HpBarY { get; set; }
    public Func<double, double?>? HpBarOpacity { get; set; }

    // ---- トグル・スイッチ系 (キーフレーム 0/1 切替) ----
    public Func<int, double, bool?>? EmitterIsEnabled { get; set; }
    public Func<int, double, bool?>? EmitterAimAtTarget { get; set; }
    public Func<int, double, bool?>? EmitterHomingEnabled { get; set; }
    public Func<int, double, bool?>? EmitterAdditive { get; set; }
    public Func<int, double, bool?>? EmitterAlignToDirection { get; set; }
    public Func<int, double, bool?>? EmitterSplitEnabled { get; set; }
    public Func<int, double, bool?>? EmitterSplitSpeedIsRelative { get; set; }
    public Func<int, double, bool?>? EmitterAuraEnabled { get; set; }
    public Func<int, double, bool?>? EmitterMagicCircleEnabled { get; set; }
    public Func<int, double, bool?>? EmitterEnemyEnabled { get; set; }
    public Func<int, double, bool?>? EmitterEnemyBehindBullets { get; set; }
    public Func<int, double, bool?>? EmitterScriptLoop { get; set; }
    public Func<int, double, bool?>? EmitterDestroyOnHit { get; set; }

    public Func<double, bool?>? CollisionEnabled { get; set; }
    public Func<double, bool?>? EnemyHitEnabled { get; set; }
    public Func<double, bool?>? SpawnHitEffect { get; set; }
    public Func<double, bool?>? ShowTargetMarker { get; set; }
    public Func<double, bool?>? PlayerShotEnabled { get; set; }
    public Func<double, bool?>? PlayerShotAlignToDirection { get; set; }
    public Func<double, bool?>? PlayerShotAdditive { get; set; }
    public Func<double, bool?>? PlayerShotAutoAim { get; set; }
    public Func<double, bool?>? PlayerShotCancelEnemyBullets { get; set; }
    public Func<double, bool?>? PlayerShotDestroyOnHit { get; set; }
    public Func<double, bool?>? HpBarEnabled { get; set; }
    public Func<double, double?>? HpBarDamagePerHit { get; set; }

    public Func<double, int?>? Seed { get; set; }
    public Func<double, int?>? MaxBullets { get; set; }
    public Func<double, int?>? Channel { get; set; }
    public Func<double, double?>? BoundsMargin { get; set; }
    public Func<double, double?>? TargetRadius { get; set; }
    public Func<double, int?>? HitEffectCount { get; set; }
    public Func<double, double?>? HitEffectSpeed { get; set; }
    public Func<double, double?>? HitEffectLifetime { get; set; }

    /// <summary>他レイヤーからの累積被弾ダメージ供給関数。</summary>
    public Func<double, double?>? ExternalDamage { get; set; }

    /// <summary>自機ショットがエネミーに命中してダメージを与えた際のコールバック (damage, emitterIndex)。</summary>
    public Action<double, int>? OnDamageDealt { get; set; }

    /// <summary>他レイヤーの敵弾を相殺できるか判定・消滅させる関数 (position, radius) -> cancelled。</summary>
    public Func<Vec2, double, bool>? CancelExternalBullet { get; set; }

    /// <summary>このレイヤーの敵弾が他レイヤーの自機ショットによって相殺されたか判定する関数 (position, radius) -> cancelled。</summary>
    public Func<Vec2, double, bool>? IsBulletCancelledByExternalShot { get; set; }

    /// <summary>画面内に存在するすべての自機判定 (マルチターゲット)。</summary>
    public Func<double, IReadOnlyList<TargetHitbox>?>? Targets { get; set; }

    /// <summary>画面内に存在するすべてのエネミー判定 (マルチエネミー)。</summary>
    public Func<double, IReadOnlyList<EnemyHitbox>?>? Enemies { get; set; }

    /// <summary>いずれかの供給関数が設定されているかどうか。</summary>
    public bool HasAny =>
        EmitterPosition is not null ||
        TargetPosition is not null ||
        TargetScale is not null ||
        TargetRotation is not null ||
        TargetOpacity is not null ||
        EmitterOrbitRadius is not null ||
        EmitterOrbitSpeed is not null ||
        EmitterOrbitPhase is not null ||
        EmitterMagicCircleRotationSpeed is not null ||
        EmitterSeedOffset is not null ||
        EmitterScriptSpeedScale is not null ||
        EmitterScriptRank is not null ||
        EmitterAngle is not null ||
        EmitterAimRate is not null ||
        EmitterWay is not null ||
        EmitterStack is not null ||
        EmitterStackSpeedStep is not null ||
        EmitterStackAngleStep is not null ||
        EmitterSpreadAngle is not null ||
        EmitterAngleStepPerShot is not null ||
        EmitterAngleJitter is not null ||
        EmitterFireInterval is not null ||
        EmitterBurstCount is not null ||
        EmitterBurstInterval is not null ||
        EmitterBurstCooldown is not null ||
        EmitterStartTime is not null ||
        EmitterEndTime is not null ||
        EmitterSpawnRadius is not null ||
        EmitterSpawnJitter is not null ||
        EmitterWallWidth is not null ||
        EmitterLaserSpacing is not null ||
        EmitterWhipAmplitude is not null ||
        EmitterWhipPeriod is not null ||
        EmitterSpeed is not null ||
        EmitterSpeedJitter is not null ||
        EmitterSpeedStep is not null ||
        EmitterAcceleration is not null ||
        EmitterAngularVelocity is not null ||
        EmitterAngularVelocityJitter is not null ||
        EmitterDamping is not null ||
        EmitterMinSpeed is not null ||
        EmitterMaxSpeed is not null ||
        EmitterGravity is not null ||
        EmitterWind is not null ||
        EmitterLifetime is not null ||
        EmitterLifetimeJitter is not null ||
        EmitterHomingTurnRate is not null ||
        EmitterHomingDuration is not null ||
        EmitterHomingDelay is not null ||
        EmitterHitRadius is not null ||
        EmitterScale is not null ||
        EmitterScaleJitter is not null ||
        EmitterScaleVelocity is not null ||
        EmitterRotationVelocity is not null ||
        EmitterHueVelocity is not null ||
        EmitterHueStep is not null ||
        EmitterOpacity is not null ||
        EmitterGlowIntensity is not null ||
        EmitterFadeInDuration is not null ||
        EmitterFadeOutDuration is not null ||
        EmitterTrailLength is not null ||
        EmitterTrailInterval is not null ||
        EmitterTrailFade is not null ||
        EmitterTrailScale is not null ||
        EmitterSplitCount is not null ||
        EmitterSplitSpread is not null ||
        EmitterSplitSpeed is not null ||
        EmitterSplitScaleFactor is not null ||
        EmitterSplitDelay is not null ||
        EmitterSplitMaxGeneration is not null ||
        GlobalOpacity is not null ||
        TimeScale is not null ||
        Seed is not null ||
        MaxBullets is not null ||
        Channel is not null ||
        BoundsMargin is not null ||
        TargetRadius is not null ||
        HitEffectCount is not null ||
        HitEffectSpeed is not null ||
        HitEffectLifetime is not null;

    /// <summary>すべての供給関数を解除する。</summary>
    public void Clear()
    {
        EmitterPosition = null;
        TargetPosition = null;
        TargetScale = null;
        TargetRotation = null;
        TargetOpacity = null;
        EmitterOrbitRadius = null;
        EmitterOrbitSpeed = null;
        EmitterOrbitPhase = null;
        EmitterMagicCircleRotationSpeed = null;
        EmitterSeedOffset = null;
        EmitterScriptSpeedScale = null;
        EmitterScriptRank = null;
        EmitterAngle = null;
        EmitterAimRate = null;
        EmitterWay = null;
        EmitterStack = null;
        EmitterStackSpeedStep = null;
        EmitterStackAngleStep = null;
        EmitterSpreadAngle = null;
        EmitterAngleStepPerShot = null;
        EmitterAngleJitter = null;
        EmitterFireInterval = null;
        EmitterBurstCount = null;
        EmitterBurstInterval = null;
        EmitterBurstCooldown = null;
        EmitterStartTime = null;
        EmitterEndTime = null;
        EmitterSpawnRadius = null;
        EmitterSpawnJitter = null;
        EmitterWallWidth = null;
        EmitterLaserSpacing = null;
        EmitterWhipAmplitude = null;
        EmitterWhipPeriod = null;
        EmitterSpeed = null;
        EmitterSpeedJitter = null;
        EmitterSpeedStep = null;
        EmitterAcceleration = null;
        EmitterAngularVelocity = null;
        EmitterAngularVelocityJitter = null;
        EmitterDamping = null;
        EmitterMinSpeed = null;
        EmitterMaxSpeed = null;
        EmitterGravity = null;
        EmitterWind = null;
        EmitterLifetime = null;
        EmitterLifetimeJitter = null;
        EmitterHomingTurnRate = null;
        EmitterHomingDuration = null;
        EmitterHomingDelay = null;
        EmitterHitRadius = null;
        EmitterScale = null;
        EmitterScaleJitter = null;
        EmitterScaleVelocity = null;
        EmitterRotationVelocity = null;
        EmitterHueVelocity = null;
        EmitterHueStep = null;
        EmitterOpacity = null;
        EmitterGlowIntensity = null;
        EmitterFadeInDuration = null;
        EmitterFadeOutDuration = null;
        EmitterTrailLength = null;
        EmitterTrailInterval = null;
        EmitterTrailFade = null;
        EmitterTrailScale = null;
        EmitterSplitCount = null;
        EmitterSplitSpread = null;
        EmitterSplitSpeed = null;
        EmitterSplitScaleFactor = null;
        EmitterSplitDelay = null;
        EmitterSplitMaxGeneration = null;
        GlobalOpacity = null;
        TimeScale = null;
        Seed = null;
        MaxBullets = null;
        Channel = null;
        BoundsMargin = null;
        TargetRadius = null;
        HitEffectCount = null;
        HitEffectSpeed = null;
        HitEffectLifetime = null;
        BossHp = null;
        HpBarRadius = null;
        HpBarWidth = null;
        HpBarHeight = null;
        HpBarX = null;
        HpBarY = null;
        HpBarOpacity = null;
    }
}
