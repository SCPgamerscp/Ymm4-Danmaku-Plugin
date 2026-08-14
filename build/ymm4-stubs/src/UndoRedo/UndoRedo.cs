// YMM4 API スタブ (ビルド検証専用)。実装は空で、実行はできません。
namespace YukkuriMovieMaker.UndoRedo;

/// <summary>元に戻す/やり直しの対象になるオブジェクト (スタブ)。</summary>
public interface IUndoRedoable
{
    void BeginEdit();

    Task EndEditAsync();
}
