namespace Ymm4DanmakuPlugin.Core.Scripting;

/// <summary>BulletML の direction 種別。</summary>
public enum BulletMlDirectionType
{
    /// <summary>自機 (ターゲット) からの相対角。BulletML の既定値。</summary>
    Aim,

    /// <summary>画面絶対角 (0 = 上方向、時計回り)。</summary>
    Absolute,

    /// <summary>自弾の進行方向からの相対角。</summary>
    Relative,

    /// <summary>直前の発射方向からの相対角。</summary>
    Sequence,
}

/// <summary>BulletML の speed 種別。</summary>
public enum BulletMlSpeedType
{
    Absolute,
    Relative,
    Sequence,
}

/// <summary>BulletML の命令。</summary>
public interface IBulletMlCommand;

/// <summary>&lt;wait&gt;</summary>
public sealed record BulletMlWait(BulletMlExpression Frames) : IBulletMlCommand;

/// <summary>&lt;vanish&gt;</summary>
public sealed record BulletMlVanish : IBulletMlCommand;

/// <summary>&lt;changeDirection&gt;</summary>
public sealed record BulletMlChangeDirection(
    BulletMlExpression Direction,
    BulletMlDirectionType Type,
    BulletMlExpression Term) : IBulletMlCommand;

/// <summary>&lt;changeSpeed&gt;</summary>
public sealed record BulletMlChangeSpeed(
    BulletMlExpression Speed,
    BulletMlSpeedType Type,
    BulletMlExpression Term) : IBulletMlCommand;

/// <summary>&lt;accel&gt;</summary>
public sealed record BulletMlAccel(
    BulletMlExpression? Horizontal,
    BulletMlSpeedType HorizontalType,
    BulletMlExpression? Vertical,
    BulletMlSpeedType VerticalType,
    BulletMlExpression Term) : IBulletMlCommand;

/// <summary>&lt;repeat&gt;</summary>
public sealed record BulletMlRepeat(BulletMlExpression Times, BulletMlActionRef Action) : IBulletMlCommand;

/// <summary>&lt;action&gt; / &lt;actionRef&gt;</summary>
public sealed record BulletMlActionRef(string? Label, BulletMlAction? Inline, IReadOnlyList<BulletMlExpression> Parameters)
    : IBulletMlCommand;

/// <summary>&lt;fire&gt; / &lt;fireRef&gt;</summary>
public sealed record BulletMlFireRef(string? Label, BulletMlFire? Inline, IReadOnlyList<BulletMlExpression> Parameters)
    : IBulletMlCommand;

/// <summary>&lt;fire&gt; の定義。</summary>
public sealed record BulletMlFire(
    string? Label,
    BulletMlExpression? Direction,
    BulletMlDirectionType DirectionType,
    BulletMlExpression? Speed,
    BulletMlSpeedType SpeedType,
    BulletMlBulletRef Bullet);

/// <summary>&lt;bullet&gt; / &lt;bulletRef&gt;</summary>
public sealed record BulletMlBulletRef(string? Label, BulletMlBullet? Inline, IReadOnlyList<BulletMlExpression> Parameters);

/// <summary>&lt;bullet&gt; の定義。</summary>
public sealed record BulletMlBullet(
    string? Label,
    BulletMlExpression? Direction,
    BulletMlDirectionType DirectionType,
    BulletMlExpression? Speed,
    BulletMlSpeedType SpeedType,
    IReadOnlyList<BulletMlActionRef> Actions);

/// <summary>&lt;action&gt; の定義。</summary>
public sealed record BulletMlAction(string? Label, IReadOnlyList<IBulletMlCommand> Commands);

/// <summary>BulletML ドキュメント全体。</summary>
public sealed class BulletMlProgram
{
    public required IReadOnlyDictionary<string, BulletMlAction> Actions { get; init; }

    public required IReadOnlyDictionary<string, BulletMlBullet> Bullets { get; init; }

    public required IReadOnlyDictionary<string, BulletMlFire> Fires { get; init; }

    /// <summary>label が "top" で始まるアクション (エントリポイント)。</summary>
    public required IReadOnlyList<BulletMlAction> TopActions { get; init; }

    /// <summary>横スクロール型 (type="horizontal") かどうか。</summary>
    public bool IsHorizontal { get; init; }

    public static BulletMlProgram Empty { get; } = new()
    {
        Actions = new Dictionary<string, BulletMlAction>(),
        Bullets = new Dictionary<string, BulletMlBullet>(),
        Fires = new Dictionary<string, BulletMlFire>(),
        TopActions = [],
    };

    public BulletMlAction? ResolveAction(BulletMlActionRef reference)
    {
        if (reference.Inline is not null) return reference.Inline;
        if (reference.Label is not null && Actions.TryGetValue(reference.Label, out var action)) return action;
        return null;
    }

    public BulletMlBullet? ResolveBullet(BulletMlBulletRef reference)
    {
        if (reference.Inline is not null) return reference.Inline;
        if (reference.Label is not null && Bullets.TryGetValue(reference.Label, out var bullet)) return bullet;
        return null;
    }

    public BulletMlFire? ResolveFire(BulletMlFireRef reference)
    {
        if (reference.Inline is not null) return reference.Inline;
        if (reference.Label is not null && Fires.TryGetValue(reference.Label, out var fire)) return fire;
        return null;
    }
}
