// YMM4 API スタブ (ビルド検証専用)。実装は空で、実行はできません。
using Vortice.Direct2D1;

namespace YukkuriMovieMaker.Commons;

/// <summary>Direct2D デバイス群 (スタブ)。</summary>
public interface IGraphicsDevices
{
    ID2D1Device D2D { get; }
}

/// <summary>Direct2D デバイスと描画コンテキスト (スタブ)。</summary>
public interface IGraphicsDevicesAndContext : IGraphicsDevices
{
    ID2D1DeviceContext6 DeviceContext { get; }
}

/// <summary>
/// <see cref="IDisposable"/> をまとめて破棄するためのヘルパー (スタブ)。
/// </summary>
public sealed class DisposeCollector : IDisposable
{
    private readonly List<IDisposable> items = [];

    public void Collect(IDisposable disposable) => items.Add(disposable);

    public void Remove(IDisposable disposable) => items.Remove(disposable);

    /// <summary>登録を解除してから破棄し、参照を null にする。</summary>
    public void RemoveAndDispose<T>(ref T? disposable) where T : class, IDisposable
    {
        if (disposable is null) return;
        items.Remove(disposable);
        disposable.Dispose();
        disposable = null;
    }

    public void DisposeAndClear()
    {
        for (var i = items.Count - 1; i >= 0; i--) items[i].Dispose();
        items.Clear();
    }

    public void Dispose() => DisposeAndClear();
}
