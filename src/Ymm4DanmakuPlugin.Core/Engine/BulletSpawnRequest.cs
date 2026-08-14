using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Mathematics;
using Ymm4DanmakuPlugin.Core.Model;
using Ymm4DanmakuPlugin.Core.Scripting;

namespace Ymm4DanmakuPlugin.Core.Engine;

/// <summary>弾 1 発の生成要求。</summary>
public struct BulletSpawnRequest
{
    /// <summary>発射位置。</summary>
    public Vec2 Position;

    /// <summary>発射方向 (度)。</summary>
    public double Direction;

    /// <summary>速度を明示指定する場合の値 (NaN で Physics の値を使用)。</summary>
    public double SpeedOverride;

    /// <summary>発射元エミッターのインデックス。</summary>
    public int EmitterIndex;

    /// <summary>1 回の発射内での通し番号 (色/速度の段階付けに使う)。</summary>
    public int IndexInBurst;

    /// <summary>発射回数の通し番号。</summary>
    public int ShotIndex;

    /// <summary>分裂世代。</summary>
    public int Generation;

    /// <summary>物理設定。</summary>
    public BulletPhysics Physics;

    /// <summary>見た目設定。</summary>
    public BulletAppearance Appearance;

    /// <summary>スプライト番号の上書き (-1 で Appearance の値)。</summary>
    public int SpriteIndexOverride;

    /// <summary>色の上書き (null で Appearance から算出)。</summary>
    public BulletColor? ColorOverride;

    /// <summary>スケール倍率。</summary>
    public double ScaleFactor;

    /// <summary>寿命の上書き (0 以下で Physics の値)。</summary>
    public double LifetimeOverride;

    /// <summary>分裂設定。</summary>
    public SplitSpec? Split;

    /// <summary>分裂までの時間 (秒)。</summary>
    public double SplitDelay;

    /// <summary>この弾に紐付ける BulletML スクリプト。</summary>
    public BulletMlRunner? Script;

    /// <summary>発射音を鳴らすかどうか。</summary>
    public bool PlayFireSound;

    /// <summary>既定値で初期化した要求を作る。</summary>
    public static BulletSpawnRequest Create(BulletPhysics physics, BulletAppearance appearance) => new()
    {
        Position = Vec2.Zero,
        Direction = 0,
        SpeedOverride = double.NaN,
        EmitterIndex = 0,
        IndexInBurst = 0,
        ShotIndex = 0,
        Generation = 0,
        Physics = physics,
        Appearance = appearance,
        SpriteIndexOverride = -1,
        ColorOverride = null,
        ScaleFactor = 1.0,
        LifetimeOverride = 0,
        Split = null,
        SplitDelay = 0,
        Script = null,
        PlayFireSound = true,
    };
}
