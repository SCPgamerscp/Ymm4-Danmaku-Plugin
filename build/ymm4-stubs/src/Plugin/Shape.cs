// YMM4 API スタブ (ビルド検証専用)。実装は空で、実行はできません。
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Shape;

/// <summary>マスクの exo 出力に必要な情報 (スタブ)。</summary>
public class ShapeMaskExoOutputDescription
{
    public bool IsEnabled { get; init; } = true;

    public bool IsInverted { get; init; }

    public Animation X { get; } = new Animation(0);

    public Animation Y { get; } = new Animation(0);

    public Animation Rotation { get; } = new Animation(0);

    public Animation Blur { get; } = new Animation(0);
}

/// <summary>図形の設定を保持するインターフェース (スタブ)。</summary>
public interface IShapeParameter : IAnimatable
{
    /// <summary>マスクの exo フィルタを生成する。</summary>
    IEnumerable<string> CreateMaskExoFilter(
        int keyFrameIndex,
        ExoOutputDescription desc,
        ShapeMaskExoOutputDescription shapeMaskParameters);

    /// <summary>図形アイテムの exo フィルタを生成する。</summary>
    IEnumerable<string> CreateShapeItemExoFilter(int keyFrameIndex, ExoOutputDescription desc);

    /// <summary>描画を担当する図形ソースを生成する。</summary>
    IShapeSource CreateShapeSource(IGraphicsDevicesAndContext devices);
}

/// <summary>図形パラメーターの基底クラス (スタブ)。</summary>
public abstract class ShapeParameterBase : Animatable, IShapeParameter
{
    protected ShapeParameterBase(SharedDataStore? sharedData = null)
    {
        if (sharedData is not null) LoadSharedData(sharedData);
    }

    public abstract IEnumerable<string> CreateMaskExoFilter(
        int keyFrameIndex,
        ExoOutputDescription desc,
        ShapeMaskExoOutputDescription shapeMaskParameters);

    public abstract IEnumerable<string> CreateShapeItemExoFilter(int keyFrameIndex, ExoOutputDescription desc);

    public abstract IShapeSource CreateShapeSource(IGraphicsDevicesAndContext devices);

    /// <summary>図形の種類を切り替えたときに設定を復元する。</summary>
    protected virtual void LoadSharedData(SharedDataStore store) { }

    /// <summary>図形の種類を切り替える前に設定を退避する。</summary>
    protected virtual void SaveSharedData(SharedDataStore store) { }
}

/// <summary>図形プラグインを表すインターフェース (スタブ)。</summary>
public interface IShapePlugin : IPlugin
{
    /// <summary>exo 出力での図形描画に対応しているかどうか。</summary>
    bool IsExoShapeSupported { get; }

    /// <summary>exo 出力でのマスクに対応しているかどうか。</summary>
    bool IsExoMaskSupported { get; }

    /// <summary>図形パラメーターを作成する。</summary>
    IShapeParameter CreateShapeParameter(SharedDataStore? sharedData);
}
