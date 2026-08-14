namespace Ymm4DanmakuPlugin.Core.Configuration;

/// <summary>弾幕定義の供給元。開発計画書における「3 階層」に対応する。</summary>
public enum DanmakuSourceMode
{
    /// <summary>第 1 階層: GUI のプリセット + スライダーで組み立てる。</summary>
    Pattern = 0,

    /// <summary>第 3 階層: JSON ファイル/テキストから読み込む。</summary>
    Json = 1,

    /// <summary>第 3 階層: BulletML (XML) から読み込む。</summary>
    BulletMl = 2,

    /// <summary>第 3 階層: Lua サブセットスクリプトから読み込む。</summary>
    Lua = 3,
}

/// <summary>組み込み弾幕パターン。東方 Project でよく見られる形状を揃えている。</summary>
public enum PatternKind
{
    /// <summary>全方位 (リング) 弾。</summary>
    Circle = 0,

    /// <summary>扇状の n-way 弾。</summary>
    Fan = 1,

    /// <summary>回転しながら発射する螺旋弾。</summary>
    Spiral = 2,

    /// <summary>自機 (ターゲット) 狙い弾。</summary>
    Aimed = 3,

    /// <summary>ランダムばら撒き。</summary>
    Scatter = 4,

    /// <summary>横一列に並べて降らせる壁弾。</summary>
    Wall = 5,

    /// <summary>速度差をつけた同心円 (蕾が開くような弾幕)。</summary>
    Bloom = 6,

    /// <summary>黄金角を用いた花弁状配置。</summary>
    Rose = 7,

    /// <summary>直線状に連続配置するレーザー風。</summary>
    Laser = 8,

    /// <summary>左右に振れる鞭のような軌道。</summary>
    Whip = 9,
}

/// <summary>色の決定方法。</summary>
public enum ColorMode
{
    /// <summary>単色。</summary>
    Single = 0,

    /// <summary>2 色間のグラデーション (弾のインデックスで補間)。</summary>
    Gradient = 1,

    /// <summary>色相を連続的に変化させる虹色。</summary>
    Rainbow = 2,

    /// <summary>東方風 8 色パレットから循環。</summary>
    Palette = 3,

    /// <summary>ランダム。</summary>
    Random = 4,
}

/// <summary>画面外に出た弾の扱い。</summary>
public enum OutOfBoundsBehavior
{
    /// <summary>消滅させる。</summary>
    Destroy = 0,

    /// <summary>反射させる。</summary>
    Bounce = 1,

    /// <summary>反対側へワープさせる。</summary>
    Wrap = 2,

    /// <summary>何もしない (寿命で消える)。</summary>
    None = 3,
}

/// <summary>効果音の種類。</summary>
public enum DanmakuSoundKind
{
    /// <summary>発射音。</summary>
    Fire = 0,

    /// <summary>軌道変化 / 分裂音。</summary>
    Change = 1,

    /// <summary>被弾音。</summary>
    Hit = 2,

    /// <summary>消滅音。</summary>
    Vanish = 3,
}
