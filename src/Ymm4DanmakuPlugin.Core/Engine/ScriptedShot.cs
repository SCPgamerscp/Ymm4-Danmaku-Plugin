using Ymm4DanmakuPlugin.Core.Model;

namespace Ymm4DanmakuPlugin.Core.Engine;

/// <summary>
/// 外部データ (JSON / Lua) から読み込んだ「時刻付きの発射命令」。
/// BulletML のようなランタイム分岐を持たない、フラットな弾幕定義に使う。
/// </summary>
public sealed record ScriptedShot
{
    /// <summary>発射時刻 (エミッター開始からの秒数)。</summary>
    public double Time { get; init; }

    /// <summary>発射角 (度)。</summary>
    public double Angle { get; init; }

    /// <summary>true の場合、Angle をターゲット方向からの相対角として扱う。</summary>
    public bool AimAtTarget { get; init; }

    /// <summary>1 命令で撃つ弾数。</summary>
    public int Way { get; init; } = 1;

    /// <summary>Way > 1 のときの扇の広がり角 (度)。360 で全方位。</summary>
    public double Spread { get; init; } = 360;

    /// <summary>速度 (px/秒)。</summary>
    public double Speed { get; init; } = 200;

    /// <summary>進行方向の加速度 (px/秒^2)。</summary>
    public double Acceleration { get; init; }

    /// <summary>旋回速度 (度/秒)。</summary>
    public double AngularVelocity { get; init; }

    /// <summary>寿命 (秒)。0 以下でエミッター既定値。</summary>
    public double Lifetime { get; init; }

    /// <summary>スプライト番号 (-1 でエミッター既定値)。</summary>
    public int SpriteIndex { get; init; } = -1;

    /// <summary>色 (null でエミッター既定値)。</summary>
    public BulletColor? Color { get; init; }

    /// <summary>スケール倍率。</summary>
    public double ScaleFactor { get; init; } = 1.0;

    /// <summary>発射位置のオフセット X。</summary>
    public double OffsetX { get; init; }

    /// <summary>発射位置のオフセット Y。</summary>
    public double OffsetY { get; init; }

    /// <summary>発射音を鳴らすか。</summary>
    public bool PlaySound { get; init; } = true;

    /// <summary>この命令で撃った弾に適用する分裂設定。</summary>
    public SplitSpec? Split { get; init; }

    /// <summary>分裂までの時間 (秒)。</summary>
    public double SplitDelay { get; init; } = 0.5;

    /// <summary>ホーミングを有効にするか (null でエミッター既定値)。</summary>
    public bool? Homing { get; init; }
}

/// <summary>時刻付き発射命令の集合。</summary>
public sealed class ScriptedShotProgram
{
    /// <summary>時刻昇順にソート済みの発射命令。</summary>
    public IReadOnlyList<ScriptedShot> Shots { get; }

    /// <summary>ループする場合の 1 周の長さ (秒)。0 以下でループしない。</summary>
    public double LoopDuration { get; }

    public ScriptedShotProgram(IEnumerable<ScriptedShot> shots, double loopDuration = 0)
    {
        Shots = shots.OrderBy(s => s.Time).ToArray();
        LoopDuration = loopDuration;
    }

    public static ScriptedShotProgram Empty { get; } = new([]);

    /// <summary>全命令が終わる時刻。</summary>
    public double Duration => Shots.Count == 0 ? 0 : Shots[^1].Time;
}
