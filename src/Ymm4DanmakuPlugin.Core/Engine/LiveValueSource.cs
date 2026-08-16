using Ymm4DanmakuPlugin.Core.Mathematics;

namespace Ymm4DanmakuPlugin.Core.Engine;

/// <summary>
/// キーフレーム (YMM4 の <c>Animation</c>) によって時間変化する値をエンジンへ供給する差し込み口。
/// <para>
/// エミッター位置やターゲット位置をタイムラインでアニメーションさせたい場合、
/// これらの値は「設定 (record)」ではなく「時刻の関数」として扱う必要がある。
/// 設定に埋め込んでしまうと 1 フレーム動かすたびに設定署名が変わり、
/// <see cref="DanmakuSimulator"/> がシミュレーションを作り直して極端に重くなる。
/// </para>
/// <para>
/// <b>決定論の担保:</b> ここに渡す関数は必ず「時刻のみに依存する純粋関数」でなければならない。
/// キーフレーム曲線の評価はまさにその条件を満たすため、
/// どのフレームへシークしても同一の弾幕が再現される性質は保たれる。
/// 乱数や前回値に依存する関数を渡してはならない。
/// </para>
/// </summary>
public sealed class LiveValueSource
{
    /// <summary>
    /// エミッター位置を供給する関数。引数は (エミッター番号, 時刻秒)。
    /// null を返した場合は設定値 (<c>EmitterSettings.X/Y</c>) が使われる。
    /// </summary>
    public Func<int, double, Vec2?>? EmitterPosition { get; set; }

    /// <summary>
    /// ターゲット (自機) 位置を供給する関数。引数は時刻秒。
    /// null を返した場合は設定値 (<c>CollisionSettings.TargetX/Y</c>) が使われる。
    /// </summary>
    public Func<double, Vec2?>? TargetPosition { get; set; }

    /// <summary>
    /// エミッターの発射基準角度 (度) を供給する関数。引数は (エミッター番号, 時刻秒)。
    /// null を返した場合は設定値 (<c>PatternSettings.BaseAngle</c>) が使われる。
    /// </summary>
    public Func<int, double, double?>? EmitterAngle { get; set; }

    // ---- 発射パターン ----
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
    public Func<int, double, double?>? EmitterSpawnRadius { get; set; }
    public Func<int, double, double?>? EmitterSpawnJitter { get; set; }
    public Func<int, double, double?>? EmitterWallWidth { get; set; }
    public Func<int, double, double?>? EmitterLaserSpacing { get; set; }
    public Func<int, double, double?>? EmitterWhipAmplitude { get; set; }
    public Func<int, double, double?>? EmitterWhipPeriod { get; set; }

    // ---- 弾の物理 ----
    public Func<int, double, double?>? EmitterSpeed { get; set; }
    public Func<int, double, double?>? EmitterAcceleration { get; set; }
    public Func<int, double, double?>? EmitterAngularVelocity { get; set; }
    public Func<int, double, double?>? EmitterDamping { get; set; }
    public Func<int, double, double?>? EmitterGravity { get; set; }
    public Func<int, double, double?>? EmitterWind { get; set; }
    public Func<int, double, double?>? EmitterLifetime { get; set; }
    public Func<int, double, double?>? EmitterHomingTurnRate { get; set; }
    public Func<int, double, double?>? EmitterHomingDuration { get; set; }
    public Func<int, double, double?>? EmitterHomingDelay { get; set; }
    public Func<int, double, double?>? EmitterHitRadius { get; set; }

    // ---- 見た目 & 残像 & 分裂 & 公転 ----
    public Func<int, double, double?>? EmitterScale { get; set; }
    public Func<int, double, double?>? EmitterRotationVelocity { get; set; }
    public Func<int, double, double?>? EmitterOpacity { get; set; }
    public Func<int, double, double?>? EmitterGlowIntensity { get; set; }
    public Func<int, double, double?>? EmitterOrbitRadius { get; set; }
    public Func<int, double, double?>? EmitterOrbitSpeed { get; set; }
    public Func<int, double, double?>? EmitterOrbitPhase { get; set; }

    public Func<int, double, int?>? EmitterSplitCount { get; set; }
    public Func<int, double, double?>? EmitterSplitSpread { get; set; }
    public Func<int, double, double?>? EmitterSplitSpeed { get; set; }
    public Func<int, double, double?>? EmitterSplitScaleFactor { get; set; }
    public Func<int, double, double?>? EmitterSplitDelay { get; set; }

    // ---- 全体設定 ----
    public Func<double, double?>? GlobalOpacity { get; set; }
    public Func<double, double?>? TimeScale { get; set; }
    public Func<double, double?>? TargetRadius { get; set; }

    /// <summary>いずれかの供給関数が設定されているかどうか。</summary>
    public bool HasAny =>
        EmitterPosition is not null ||
        TargetPosition is not null ||
        EmitterAngle is not null ||
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
        EmitterSpawnRadius is not null ||
        EmitterSpawnJitter is not null ||
        EmitterWallWidth is not null ||
        EmitterLaserSpacing is not null ||
        EmitterWhipAmplitude is not null ||
        EmitterWhipPeriod is not null ||
        EmitterSpeed is not null ||
        EmitterAcceleration is not null ||
        EmitterAngularVelocity is not null ||
        EmitterDamping is not null ||
        EmitterGravity is not null ||
        EmitterWind is not null ||
        EmitterLifetime is not null ||
        EmitterHomingTurnRate is not null ||
        EmitterHomingDuration is not null ||
        EmitterHomingDelay is not null ||
        EmitterHitRadius is not null ||
        EmitterScale is not null ||
        EmitterRotationVelocity is not null ||
        EmitterOpacity is not null ||
        EmitterGlowIntensity is not null ||
        EmitterOrbitRadius is not null ||
        EmitterOrbitSpeed is not null ||
        EmitterOrbitPhase is not null ||
        EmitterSplitCount is not null ||
        EmitterSplitSpread is not null ||
        EmitterSplitSpeed is not null ||
        EmitterSplitScaleFactor is not null ||
        EmitterSplitDelay is not null ||
        GlobalOpacity is not null ||
        TimeScale is not null ||
        TargetRadius is not null;

    /// <summary>すべての供給関数を解除する。</summary>
    public void Clear()
    {
        EmitterPosition = null;
        TargetPosition = null;
        EmitterAngle = null;
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
        EmitterSpawnRadius = null;
        EmitterSpawnJitter = null;
        EmitterWallWidth = null;
        EmitterLaserSpacing = null;
        EmitterWhipAmplitude = null;
        EmitterWhipPeriod = null;
        EmitterSpeed = null;
        EmitterAcceleration = null;
        EmitterAngularVelocity = null;
        EmitterDamping = null;
        EmitterGravity = null;
        EmitterWind = null;
        EmitterLifetime = null;
        EmitterHomingTurnRate = null;
        EmitterHomingDuration = null;
        EmitterHomingDelay = null;
        EmitterHitRadius = null;
        EmitterScale = null;
        EmitterRotationVelocity = null;
        EmitterOpacity = null;
        EmitterGlowIntensity = null;
        EmitterOrbitRadius = null;
        EmitterOrbitSpeed = null;
        EmitterOrbitPhase = null;
        EmitterSplitCount = null;
        EmitterSplitSpread = null;
        EmitterSplitSpeed = null;
        EmitterSplitScaleFactor = null;
        EmitterSplitDelay = null;
        GlobalOpacity = null;
        TimeScale = null;
        TargetRadius = null;
    }
}
