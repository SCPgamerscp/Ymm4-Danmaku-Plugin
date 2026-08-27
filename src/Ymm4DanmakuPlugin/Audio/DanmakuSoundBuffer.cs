namespace Ymm4DanmakuPlugin.Audio;

/// <summary>
/// 音声アイテムから読み込んだ PCM サンプル列を保持し、ピッチ変調付きでサンプリングするバッファ。
/// </summary>
public sealed class DanmakuSoundBuffer
{
    /// <summary>2ch インターリーブのサンプル列。</summary>
    public float[] Samples { get; }

    /// <summary>サンプリング周波数 (Hz)。</summary>
    public int Hz { get; }

    /// <summary>1ch あたりのサンプル数。</summary>
    public int FrameCount => Samples.Length / 2;

    private DanmakuSoundBuffer(float[] samples, int hz)
    {
        Samples = samples;
        Hz = hz;
    }

    /// <summary>
    /// YMM4 の音声ストリーム (Input) からサンプルを読み取ってバッファを生成する。
    /// </summary>
    public static DanmakuSoundBuffer? FromAudioStream(YukkuriMovieMaker.Player.Audio.Effects.IAudioStream? stream, int maxSeconds = 60)
    {
        if (stream is null) return null;
        var hz = stream.Hz;
        if (hz <= 0) hz = 48000;

        // 最大 maxSeconds 分 (2ch float なので hz * 2 * maxSeconds)
        var maxElements = hz * 2 * maxSeconds;
        var totalElements = (int)Math.Min(stream.Duration, maxElements);
        if (totalElements <= 0) return null;

        var originalPosition = stream.Position;
        stream.Seek(0);

        var samples = new float[totalElements];
        var readTotal = 0;
        var chunk = new float[4096];
        while (readTotal < totalElements)
        {
            var toRead = Math.Min(chunk.Length, totalElements - readTotal);
            var read = stream.Read(chunk, 0, toRead);
            if (read <= 0) break;
            Array.Copy(chunk, 0, samples, readTotal, read);
            readTotal += read;
        }

        stream.Seek(originalPosition);

        if (readTotal == 0) return null;
        if (readTotal < totalElements)
        {
            Array.Resize(ref samples, readTotal);
        }

        return new DanmakuSoundBuffer(samples, hz);
    }

    /// <summary>
    /// 指定した位置のサンプルを線形補間で取得する。
    /// ピッチ変更 (再生速度の変更) のために小数位置を扱える。
    /// </summary>
    /// <param name="framePosition">1ch 基準のサンプル位置 (小数)。</param>
    /// <param name="channel">0 = L、1 = R。</param>
    public float SampleAt(double framePosition, int channel)
    {
        if (framePosition < 0) return 0f;

        var index = (int)framePosition;
        if (index >= FrameCount) return 0f;

        var a = Samples[index * 2 + channel];
        var next = index + 1;
        if (next >= FrameCount) return a;

        var b = Samples[next * 2 + channel];
        var t = (float)(framePosition - index);
        return a + (b - a) * t;
    }
}
