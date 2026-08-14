using Ymm4DanmakuPlugin.Core.Mathematics;
using Ymm4DanmakuPlugin.Core.Scripting;

namespace Ymm4DanmakuPlugin.Core.Model;

/// <summary>弾の消滅理由。</summary>
public enum BulletDeathReason
{
    None,
    Lifetime,
    OutOfBounds,
    Vanished,
    Hit,
    Split,
}

/// <summary>
/// 1 発の弾。<see cref="BulletPool"/> により使い回されるため、
/// <see cref="Reset"/> ですべてのフィールドを初期化できるようにしてある。
/// </summary>
public sealed class Bullet
{
    /// <summary>トレイル (残像) 用の履歴保持数の上限。</summary>
    public const int MaxTrailLength = 48;

    // ---- 識別 ----
    /// <summary>プール内での固定インデックス。</summary>
    public int PoolIndex { get; internal set; }

    /// <summary>生成のたびに増加する一意 ID。ヒット判定の重複防止などに使う。</summary>
    public long Id { get; internal set; }

    /// <summary>生存中かどうか。</summary>
    public bool IsAlive { get; internal set; }

    /// <summary>
    /// <see cref="BulletPool.ActiveBullets"/> に登録済みかどうか。
    /// <para>
    /// 同一ステップ内で Return → Rent が起こると、Compact 前のため
    /// インスタンスがまだ active リストに残っている。このフラグで
    /// 二重登録 (= 物理更新/描画の二重適用) を防ぐ。
    /// <see cref="Reset"/> ではクリアしない点に注意。
    /// </para>
    /// </summary>
    internal bool InActiveList;

    /// <summary>この弾を発射したエミッターのインデックス。</summary>
    public int EmitterIndex;

    /// <summary>分裂の世代 (0 = 親)。</summary>
    public int Generation;

    // ---- 運動 ----
    public Vec2 Position;
    public Vec2 PreviousPosition;

    /// <summary>進行方向 (度)。速度は Speed と分離して保持し、BulletML の changeDirection/changeSpeed に対応する。</summary>
    public double Direction;

    /// <summary>速度 (px/秒)。</summary>
    public double Speed;

    /// <summary>進行方向に対する加速度 (px/秒^2)。</summary>
    public double Acceleration;

    /// <summary>旋回速度 (度/秒)。正で時計回り。</summary>
    public double AngularVelocity;

    /// <summary>ワールド座標系での外力加速度 (重力・風など)。</summary>
    public Vec2 ExternalAcceleration;

    /// <summary>速度減衰係数 (1 秒あたりに残る割合、1 で減衰なし)。</summary>
    public double Damping = 1.0;

    /// <summary>速度の下限/上限 (px/秒)。</summary>
    public double MinSpeed = double.NegativeInfinity;
    public double MaxSpeed = double.PositiveInfinity;

    // ---- ホーミング ----
    public bool HomingEnabled;

    /// <summary>ホーミングの旋回力 (度/秒)。</summary>
    public double HomingTurnRate;

    /// <summary>ホーミングが有効な残り時間 (秒)。0 以下で無効化。</summary>
    public double HomingRemaining;

    /// <summary>ホーミング開始までの残り遅延時間 (秒)。</summary>
    public double HomingDelay;

    /// <summary>追尾対象の座標 (エンジンから毎フレーム供給される)。</summary>
    public Vec2 HomingTarget;

    // ---- 見た目 ----
    /// <summary>スプライトスロット番号 (ユーザー指定画像の何番目か)。</summary>
    public int SpriteIndex;

    /// <summary>スプライトの基準スケール。</summary>
    public double Scale = 1.0;

    /// <summary>1 秒あたりのスケール変化量。</summary>
    public double ScaleVelocity;

    /// <summary>描画上の回転角 (度)。</summary>
    public double Rotation;

    /// <summary>回転速度 (度/秒)。</summary>
    public double RotationVelocity;

    /// <summary>進行方向に画像を向けるかどうか。</summary>
    public bool AlignToDirection;

    public BulletColor Color = BulletColor.White;

    /// <summary>色相の 1 秒あたりの変化量 (度)。虹色弾に使う。</summary>
    public double HueVelocity;

    /// <summary>現在の色相 (HueVelocity 使用時のみ有効)。</summary>
    public double Hue;

    /// <summary>HSV 由来の彩度・明度 (HueVelocity 使用時のみ有効)。</summary>
    public double Saturation = 0.85;
    public double Value = 1.0;

    /// <summary>加算合成 (発光) するかどうか。</summary>
    public bool Additive = true;

    /// <summary>発生直後のフェードイン時間 (秒)。</summary>
    public double FadeInDuration;

    /// <summary>消滅前のフェードアウト時間 (秒)。</summary>
    public double FadeOutDuration;

    /// <summary>スプライトアニメーションの現在コマ。</summary>
    public int AnimationFrame;

    /// <summary>スプライトアニメーションの速度 (コマ/秒)。0 でアニメーションなし。</summary>
    public double AnimationFps;

    private double animationAccumulator;

    // ---- 寿命 ----
    /// <summary>生成からの経過時間 (秒)。</summary>
    public double Age;

    /// <summary>寿命 (秒)。無限の場合は double.PositiveInfinity。</summary>
    public double Lifetime = double.PositiveInfinity;

    public BulletDeathReason DeathReason;

    // ---- 分裂 / 多段 ----
    /// <summary>分裂までの残り時間 (秒)。0 未満で分裂しない。</summary>
    public double SplitTimer = -1;

    /// <summary>分裂設定 (null で分裂しない)。</summary>
    public SplitSpec? Split;

    // ---- スクリプト (BulletML / Lua) ----
    /// <summary>この弾に紐付く BulletML ランナー。</summary>
    public BulletMlRunner? Script;

    // ---- 衝突 ----
    /// <summary>当たり判定半径 (px)。0 以下で判定なし。</summary>
    public double HitRadius;

    /// <summary>衝突時に消滅するかどうか。</summary>
    public bool DestroyOnHit = true;

    /// <summary>既に衝突済みかどうか。</summary>
    public bool HasHit;

    // ---- トレイル ----
    /// <summary>過去位置のリングバッファ。</summary>
    public readonly Vec2[] TrailPositions = new Vec2[MaxTrailLength];

    /// <summary>リングバッファの書き込み位置。</summary>
    public int TrailHead;

    /// <summary>有効なトレイル点数。</summary>
    public int TrailCount;

    /// <summary>このフレームで記録すべきトレイル長 (0 でトレイル無効)。</summary>
    public int TrailLength;

    /// <summary>トレイル記録の間隔 (秒)。</summary>
    public double TrailInterval = 1.0 / 60.0;

    private double trailAccumulator;

    /// <summary>速度ベクトル (px/秒)。</summary>
    public Vec2 Velocity => Vec2.FromDegrees(Direction, Speed);

    /// <summary>現在の不透明度倍率 (フェードイン/アウトを考慮)。</summary>
    public float OpacityFactor
    {
        get
        {
            var factor = 1.0;
            if (FadeInDuration > 0 && Age < FadeInDuration)
                factor *= DanmakuMath.Clamp(Age / FadeInDuration, 0, 1);

            if (FadeOutDuration > 0 && double.IsFinite(Lifetime))
            {
                var remaining = Lifetime - Age;
                if (remaining < FadeOutDuration)
                    factor *= DanmakuMath.Clamp(remaining / FadeOutDuration, 0, 1);
            }

            return (float)DanmakuMath.Clamp(factor, 0, 1);
        }
    }

    /// <summary>プールから取り出す際に全フィールドを初期化する。</summary>
    public void Reset()
    {
        IsAlive = false;
        EmitterIndex = 0;
        Generation = 0;

        Position = Vec2.Zero;
        PreviousPosition = Vec2.Zero;
        Direction = 0;
        Speed = 0;
        Acceleration = 0;
        AngularVelocity = 0;
        ExternalAcceleration = Vec2.Zero;
        Damping = 1.0;
        MinSpeed = double.NegativeInfinity;
        MaxSpeed = double.PositiveInfinity;

        HomingEnabled = false;
        HomingTurnRate = 0;
        HomingRemaining = 0;
        HomingDelay = 0;
        HomingTarget = Vec2.Zero;

        SpriteIndex = 0;
        Scale = 1.0;
        ScaleVelocity = 0;
        Rotation = 0;
        RotationVelocity = 0;
        AlignToDirection = false;
        Color = BulletColor.White;
        HueVelocity = 0;
        Hue = 0;
        Saturation = 0.85;
        Value = 1.0;
        Additive = true;
        FadeInDuration = 0;
        FadeOutDuration = 0;
        AnimationFrame = 0;
        AnimationFps = 0;
        animationAccumulator = 0;

        Age = 0;
        Lifetime = double.PositiveInfinity;
        DeathReason = BulletDeathReason.None;

        SplitTimer = -1;
        Split = null;
        Script = null;

        HitRadius = 0;
        DestroyOnHit = true;
        HasHit = false;

        TrailHead = 0;
        TrailCount = 0;
        TrailLength = 0;
        TrailInterval = 1.0 / 60.0;
        trailAccumulator = 0;
    }

    /// <summary>トレイル履歴を更新する。</summary>
    public void UpdateTrail(double deltaTime)
    {
        if (TrailLength <= 0)
        {
            TrailCount = 0;
            return;
        }

        trailAccumulator += deltaTime;
        var interval = TrailInterval > 0 ? TrailInterval : deltaTime;
        var guard = 0;
        while (trailAccumulator >= interval && guard++ < MaxTrailLength)
        {
            trailAccumulator -= interval;
            TrailPositions[TrailHead] = Position;
            TrailHead = (TrailHead + 1) % MaxTrailLength;
            var limit = Math.Min(TrailLength, MaxTrailLength);
            if (TrailCount < limit) TrailCount++;
        }
    }

    /// <summary>index 番目 (0 = 直近) のトレイル位置を取得する。</summary>
    public Vec2 GetTrailPosition(int index)
    {
        if (index < 0 || index >= TrailCount) return Position;
        var i = TrailHead - 1 - index;
        while (i < 0) i += MaxTrailLength;
        return TrailPositions[i % MaxTrailLength];
    }

    /// <summary>スプライトアニメーションを進める。</summary>
    public void AdvanceAnimation(double deltaTime, int frameCount)
    {
        if (AnimationFps <= 0 || frameCount <= 1) return;
        animationAccumulator += deltaTime * AnimationFps;
        if (animationAccumulator >= 1.0)
        {
            var step = (int)animationAccumulator;
            animationAccumulator -= step;
            AnimationFrame = (AnimationFrame + step) % frameCount;
        }
    }
}

/// <summary>弾の分裂 (多段) 設定。</summary>
public sealed record SplitSpec
{
    /// <summary>分裂後の弾数。</summary>
    public int Count { get; init; } = 8;

    /// <summary>分裂後の弾を配置する扇形の角度 (度)。360 で全方位。</summary>
    public double SpreadDegrees { get; init; } = 360;

    /// <summary>親の進行方向に対するオフセット角 (度)。</summary>
    public double AngleOffset { get; init; }

    /// <summary>分裂後の速度 (px/秒)。</summary>
    public double Speed { get; init; } = 180;

    /// <summary>分裂後の速度を親の速度からの相対値にするか。</summary>
    public bool SpeedIsRelative { get; init; }

    /// <summary>分裂後の弾のスプライト番号 (-1 で親を継承)。</summary>
    public int SpriteIndex { get; init; } = -1;

    /// <summary>分裂後の弾の色 (null で親を継承)。</summary>
    public BulletColor? Color { get; init; }

    /// <summary>分裂後の弾のスケール倍率。</summary>
    public double ScaleFactor { get; init; } = 0.8;

    /// <summary>分裂後の弾の寿命 (秒)。0 以下で親の残り寿命を引き継ぐ。</summary>
    public double Lifetime { get; init; }

    /// <summary>分裂後、親を消滅させるか。</summary>
    public bool DestroyParent { get; init; } = true;

    /// <summary>さらに分裂する場合の子の分裂設定 (多段)。</summary>
    public SplitSpec? Next { get; init; }

    /// <summary>子が分裂するまでの時間 (秒)。</summary>
    public double NextDelay { get; init; } = 0.5;

    /// <summary>分裂を許容する最大世代。</summary>
    public int MaxGeneration { get; init; } = 3;
}
