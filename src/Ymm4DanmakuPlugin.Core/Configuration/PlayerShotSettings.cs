using Ymm4DanmakuPlugin.Core.Model;

namespace Ymm4DanmakuPlugin.Core.Configuration;

/// <summary>
/// 自機ショットの発射種別。
/// </summary>
public enum PlayerShotType
{
    /// <summary>前方集中ショット (霊夢の博麗札 / 直線高速連射)。</summary>
    FocusStraight = 0,

    /// <summary>前方拡散ショット (霊夢の拡散アミュレット / ワイドショット)。</summary>
    WideSpread = 1,

    /// <summary>誘導札 (エネミーの位置へ自動旋回・追尾するアミュレット)。</summary>
    HomingAmulet = 2,

    /// <summary>高速針 (咲夜の投げナイフ / 高速狭角針弾)。</summary>
    FastNeedle = 3,

    /// <summary>ユーザー指定のカスタム画像。</summary>
    CustomImage = 4,
}

/// <summary>
/// 自機 (ターゲット) から発射されるショットの設定。
/// </summary>
public sealed record PlayerShotSettings
{
    /// <summary>自機射撃を有効にするかどうか。</summary>
    public bool IsEnabled { get; init; }

    /// <summary>ショットの種別。</summary>
    public PlayerShotType ShotType { get; init; } = PlayerShotType.FocusStraight;

    /// <summary>カスタム弾画像のファイルパス (未指定時は組み込みスプライト)。</summary>
    public string? ImagePath { get; init; }

    /// <summary>同時に発射する弾数 (Way 数)。</summary>
    public int Way { get; init; } = 2;

    /// <summary>連射間隔 (秒)。</summary>
    public double FireInterval { get; init; } = 0.08;

    /// <summary>弾速 (px/秒)。</summary>
    public double Speed { get; init; } = 1200;

    /// <summary>拡散角度 (度)。0 で平行発射。</summary>
    public double SpreadAngle { get; init; } = 15;

    /// <summary>弾の拡大倍率。</summary>
    public double Scale { get; init; } = 1.0;

    /// <summary>進行方向に弾の向きを合わせるかどうか。</summary>
    public bool AlignToDirection { get; init; } = true;

    /// <summary>弾の色。</summary>
    public BulletColor Color { get; init; } = BulletColor.White;

    /// <summary>加算合成 (発光) するかどうか。</summary>
    public bool Additive { get; init; } = true;

    /// <summary>エネミー (ボス) の方向を自動で狙って発射するかどうか。</summary>
    public bool AutoAim { get; init; }

    /// <summary>当たり判定半径 (px)。</summary>
    public double HitRadius { get; init; } = 12;

    /// <summary>エネミー被弾時に弾を消すかどうか。</summary>
    public bool DestroyOnHit { get; init; } = true;

    /// <summary>敵弾と衝突した際に敵弾を相殺・消滅させるかどうか。</summary>
    public bool CancelEnemyBullets { get; init; }

    /// <summary>
    /// 当たり判定の対象とするチャンネル番号 (-1 で全チャンネル対象)。
    /// </summary>
    public int TargetChannel { get; init; } = -1;
}
