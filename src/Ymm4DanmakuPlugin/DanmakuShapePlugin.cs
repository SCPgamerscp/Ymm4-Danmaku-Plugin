using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;
using Ymm4DanmakuPlugin.Parameters;

namespace Ymm4DanmakuPlugin;

/// <summary>
/// 東方風弾幕を生成する図形プラグイン。
/// <para>
/// YMM4 の公開拡張点のうち <see cref="IShapePlugin"/> を用いる。
/// タイムラインには「図形アイテム」として追加され、
/// 拡大率・回転・不透明度・座標・エフェクト・キーフレームといった
/// YMM4 本体の機能をそのまま利用できる。
/// </para>
/// <para>
/// <b>なぜ IVideoItem ではないのか:</b>
/// YMM4 の Plugin SDK に <c>IVideoItem</c> という公開拡張点は存在しない。
/// ユーザー定義のタイムラインアイテムを追加する手段として公開されているのは
/// 図形プラグイン (<see cref="IShapePlugin"/>) であり、
/// 実用上は「独自アイテム」と同等の使い勝手になる。
/// </para>
/// </summary>
public class DanmakuShapePlugin : IShapePlugin
{
    /// <summary>図形一覧に表示される名前。</summary>
    public string Name => "東方風弾幕";

    public PluginDetailsAttribute? Details => new()
    {
        AuthorName = "Ymm4DanmakuPlugin contributors",
        ContentId = string.Empty,
    };

    /// <summary>
    /// exo (AviUtl) 出力での図形描画には対応しない。
    /// 弾幕は独自シミュレーションであり AviUtl 側に等価な機能が無いため。
    /// </summary>
    public bool IsExoShapeSupported => false;

    /// <summary>exo 出力でのマスクにも対応しない。</summary>
    public bool IsExoMaskSupported => false;

    public IShapeParameter CreateShapeParameter(SharedDataStore? sharedData) =>
        new DanmakuShapeParameter(sharedData);
}
