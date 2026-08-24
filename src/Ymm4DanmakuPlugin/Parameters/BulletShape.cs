using System.ComponentModel.DataAnnotations;

namespace Ymm4DanmakuPlugin.Parameters;

/// <summary>
/// 組み込みの弾スプライト形状。
/// <para>
/// 値がそのまま <c>BulletAppearance.SpriteIndex</c> (スプライトスロット番号) になる。
/// ユーザー指定画像は <see cref="SpriteSlots.CustomBase"/> 以降のスロットへ登録される。
/// </para>
/// </summary>
public enum BulletShape
{
    [Display(Name = "丸弾", Description = "東方の基本となる、芯のある丸い弾。")]
    Circle = 0,

    [Display(Name = "中弾", Description = "丸弾よりひとまわり大きく、輪郭のはっきりした弾。")]
    Medium = 1,

    [Display(Name = "大玉", Description = "ゆっくり漂う大きな玉。")]
    Large = 2,

    [Display(Name = "米弾", Description = "進行方向に細長い、いわゆる米粒弾。")]
    Rice = 3,

    [Display(Name = "札弾", Description = "縦長の長方形。お札のような弾。")]
    Card = 4,

    [Display(Name = "鱗弾", Description = "先の尖った菱形の弾。")]
    Scale = 5,

    [Display(Name = "星弾", Description = "5 芒星の弾。")]
    Star = 6,

    [Display(Name = "輪弾", Description = "中央が抜けたリング状の弾。")]
    Ring = 7,

    [Display(Name = "光弾", Description = "輪郭のない、ぼんやり発光する弾。加算合成向け。")]
    Glow = 8,

    [Display(Name = "矢弾", Description = "進行方向を向く矢印状の弾。")]
    Arrow = 9,

    [Display(Name = "蝶弾", Description = "蝶のような形の装飾弾。")]
    Butterfly = 10,

    [Display(Name = "粒", Description = "ごく小さな粒。ヒットエフェクト向け。")]
    Particle = 11,
}

/// <summary>スプライトスロット番号の割り当て規則。</summary>
public static class SpriteSlots
{
    /// <summary>組み込み形状の数。</summary>
    public static readonly int BuiltInCount = Enum.GetValues<BulletShape>().Length;

    /// <summary>組み込みの魔法陣スロット番号。</summary>
    public const int BuiltInMagicCircleSlot = 12;

    /// <summary>ユーザー指定弾画像を割り当てる先頭スロット番号 (64〜79)。</summary>
    public const int CustomBase = 64;

    /// <summary>自機 (ターゲット) のユーザー画像スロット番号 (80)。</summary>
    public const int TargetCustomSlot = 80;

    /// <summary>エネミー (敵) のユーザー画像スロットの先頭 (81〜96)。</summary>
    public const int EnemyCustomBase = 81;

    /// <summary>カスタム魔法陣画像の先頭スロット (97〜112)。</summary>
    public const int MagicCircleCustomBase = 97;

    /// <summary>スプライトスロットの総数。</summary>
    public const int Capacity = 128;

    /// <summary>エミッター番号に対応する弾画像スロット番号を返す。</summary>
    public static int CustomSlotOf(int emitterIndex) => CustomBase + emitterIndex;

    /// <summary>エミッター番号に対応するエネミー画像スロット番号を返す。</summary>
    public static int EnemyCustomSlotOf(int emitterIndex) => EnemyCustomBase + emitterIndex;

    /// <summary>エミッター番号に対応する魔法陣画像スロット番号を返す。</summary>
    public static int MagicCircleCustomSlotOf(int emitterIndex) => MagicCircleCustomBase + emitterIndex;

    /// <summary>ユーザー画像スロット番号に対応するエミッター番号を返す。</summary>
    public static int EmitterIndexOf(int slot) => slot - CustomBase;
}
