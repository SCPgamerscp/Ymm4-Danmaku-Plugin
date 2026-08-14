// YMM4 API スタブ (ビルド検証専用)。実装は空で、実行はできません。
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Player.Audio.Effects;

/// <summary>
/// 音声ストリーム (スタブ)。
/// <para>
/// YMM4 の音声は 32bit float / 2ch インターリーブで扱われる。
/// <c>Position</c> と <c>Duration</c> は「サンプル数 × チャンネル数」であることに注意。
/// </para>
/// </summary>
public interface IAudioStream
{
    /// <summary>サンプリング周波数 (Hz)。</summary>
    int Hz { get; }

    /// <summary>全体の長さ (float 要素数、= サンプル数 × 2ch)。</summary>
    long Duration { get; }

    /// <summary>現在位置 (float 要素数)。</summary>
    long Position { get; }

    int Read(float[] destBuffer, int offset, int count);

    void Seek(long position);

    void Seek(TimeSpan time);
}

/// <summary>音声エフェクトプロセッサー (スタブ)。</summary>
public interface IAudioEffectProcessor : IAudioStream, IDisposable
{
    /// <summary>エフェクトの入力ストリーム。</summary>
    IAudioStream? Input { get; set; }
}

/// <summary>音声エフェクト処理の基底クラス (スタブ)。</summary>
public abstract class AudioEffectProcessorBase : IAudioEffectProcessor
{
    protected readonly DisposeCollector disposer = new();

    protected AudioEffectProcessorBase() { }

    public virtual IAudioStream? Input { get; set; }

    public abstract int Hz { get; }

    public abstract long Duration { get; }

    public long Position { get; private set; }

    /// <summary>音声を読み出す。派生クラスは <see cref="read"/> を実装する。</summary>
    public int Read(float[] destBuffer, int offset, int count)
    {
        var read = this.read(destBuffer, offset, count);
        Position += read;
        return read;
    }

    /// <summary>エフェクトの本体。destBuffer に count 要素ぶん書き込む。</summary>
    protected abstract int read(float[] destBuffer, int offset, int count);

    public void Seek(long position)
    {
        Position = position;
        seek(position);
    }

    public void Seek(TimeSpan time) => Seek(TimeToPosition(time));

    /// <summary>シーク処理。派生クラスは Input のシークを行う。</summary>
    protected abstract void seek(long position);

    protected TimeSpan PositionToTime(long position) =>
        Hz <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds((double)position / 2 / Hz);

    protected long TimeToPosition(TimeSpan time) => (long)(time.TotalSeconds * Hz) * 2;

    protected virtual void Dispose(bool disposing)
    {
        if (disposing) disposer.DisposeAndClear();
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
