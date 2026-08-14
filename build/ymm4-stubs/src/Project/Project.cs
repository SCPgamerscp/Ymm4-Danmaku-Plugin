// YMM4 API スタブ (ビルド検証専用)。実装は空で、実行はできません。
namespace YukkuriMovieMaker.Project;

/// <summary>動画全体の情報 (スタブ)。</summary>
public class VideoInfo
{
    public int FPS { get; init; } = 60;

    public int Width { get; init; } = 1920;

    public int Height { get; init; } = 1080;
}

/// <summary>
/// 図形の種類を切り替えたときに設定を復元するための一時保存領域 (スタブ)。
/// </summary>
public sealed class SharedDataStore
{
    private readonly Dictionary<Type, object> values = [];

    public T? Load<T>() where T : class => values.TryGetValue(typeof(T), out var v) ? (T)v : null;

    public void Save(object value) => values[value.GetType()] = value;
}
