namespace Ymm4DanmakuPlugin.Core.Configuration;

/// <summary>
/// 第 1 階層「GUI プリセットスライダー」に相当するパターン形状設定。
/// </summary>
public sealed record PatternSettings
{
    public PatternKind Kind { get; init; } = PatternKind.Circle;

    /// <summary>1 回の発射で撃つ弾の本数 (way 数)。</summary>
    public int Way { get; init; } = 16;

    /// <summary>同心円状に重ねる段数。Way × Stack 発が 1 回で発射される。</summary>
    public int Stack { get; init; } = 1;

    /// <summary>Stack ごとの速度差 (px/秒)。</summary>
    public double StackSpeedStep { get; init; } = 40;

    /// <summary>Stack ごとの角度オフセット (度)。</summary>
    public double StackAngleStep { get; init; }

    /// <summary>基準角度 (度)。0 で右方向。</summary>
    public double BaseAngle { get; init; } = -90;

    /// <summary>弾を配置する扇の広がり角 (度)。Circle では 360 が既定。</summary>
    public double SpreadAngle { get; init; } = 360;

    /// <summary>1 回の発射ごとに基準角へ加算する角度 (度)。螺旋弾の要。</summary>
    public double AngleStepPerShot { get; init; } = 0;

    /// <summary>基準角のランダム幅 (±度)。</summary>
    public double AngleJitter { get; init; }

    /// <summary>発射間隔 (秒)。</summary>
    public double FireInterval { get; init; } = 0.1;

    /// <summary>連射のかたまり (バースト) 内の発射回数。</summary>
    public int BurstCount { get; init; } = 1;

    /// <summary>バースト内の発射間隔 (秒)。</summary>
    public double BurstInterval { get; init; } = 0.02;

    /// <summary>バースト後の待機時間 (秒)。</summary>
    public double BurstCooldown { get; init; }

    /// <summary>発射開始時間 (秒、アイテム先頭からの相対)。</summary>
    public double StartTime { get; init; }

    /// <summary>発射終了時間 (秒)。0 以下でアイテム終端まで。</summary>
    public double EndTime { get; init; }

    /// <summary>発射位置からの初期距離 (px)。リング状に離して撃ち出す。</summary>
    public double SpawnRadius { get; init; }

    /// <summary>発射位置のランダム幅 (±px)。</summary>
    public double SpawnJitter { get; init; }

    /// <summary>自機狙い度 (0%〜100%)。0 で固定角度、100 で完全自機狙い。</summary>
    public double AimRate { get; init; }

    /// <summary>ターゲット (自機) を狙うかどうか。AimRate が 0 より大きければ true。</summary>
    public bool AimAtTarget
    {
        get => AimRate > 0;
        init => AimRate = value ? 100.0 : 0.0;
    }

    /// <summary>Wall 横並び配置の横幅 (px)。0 で点発生。どのパターンにも重ねがけ可能。</summary>
    public double WallWidth { get; init; } = 0;

    /// <summary>Laser 前後ストリーム配置の間隔 (px)。0 で前後オフセットなし。どのパターンにも重ねがけ可能。</summary>
    public double LaserSpacing { get; init; } = 0;

    /// <summary>Whip 首振り振動の振れ幅 (度)。0 で首振りなし。どのパターンにも重ねがけ可能。</summary>
    public double WhipAmplitude { get; init; } = 0;

    /// <summary>Whip 首振り振動の周期 (秒)。</summary>
    public double WhipPeriod { get; init; } = 1.2;
}
