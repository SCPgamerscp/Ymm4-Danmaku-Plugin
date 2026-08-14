namespace Ymm4DanmakuPlugin.Audio;

/// <summary>
/// 効果音ファイルをメモリへ展開したもの。
/// <para>
/// 32bit float / 2ch インターリーブ (YMM4 の音声形式) に正規化して保持する。
/// 同じファイルは <see cref="Load"/> のキャッシュにより 1 度しか読み込まれない。
/// </para>
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

    private static readonly Dictionary<string, DanmakuSoundBuffer?> Cache = [];
    private static readonly object Gate = new();

    /// <summary>
    /// WAV ファイルを読み込む。読み込めない場合は null。
    /// <para>
    /// 対応形式は非圧縮 PCM (8/16/24/32bit 整数) と IEEE float の WAV。
    /// 外部ライブラリに依存しないため、mp3 等は対象外としている
    /// (YMM4 の効果音は wav が一般的なため実用上問題にならない)。
    /// </para>
    /// </summary>
    public static DanmakuSoundBuffer? Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        lock (Gate)
        {
            if (Cache.TryGetValue(path, out var cached)) return cached;

            DanmakuSoundBuffer? buffer = null;
            try
            {
                if (File.Exists(path)) buffer = ReadWav(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or EndOfStreamException)
            {
                buffer = null;
            }

            // 失敗も記録して再試行を防ぐ (毎フレーム読み込みを試みると重い)
            Cache[path] = buffer;
            return buffer;
        }
    }

    /// <summary>キャッシュを破棄する (設定変更でファイルを差し替えたときなど)。</summary>
    public static void ClearCache()
    {
        lock (Gate) Cache.Clear();
    }

    private static DanmakuSoundBuffer ReadWav(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        if (new string(reader.ReadChars(4)) != "RIFF") throw new InvalidDataException("RIFF ヘッダがありません。");
        reader.ReadUInt32(); // ファイルサイズ
        if (new string(reader.ReadChars(4)) != "WAVE") throw new InvalidDataException("WAVE ヘッダがありません。");

        var format = 1;
        var channels = 2;
        var hz = 48000;
        var bitsPerSample = 16;
        byte[]? data = null;

        while (stream.Position + 8 <= stream.Length)
        {
            var chunkId = new string(reader.ReadChars(4));
            var chunkSize = reader.ReadUInt32();
            var next = stream.Position + chunkSize + (chunkSize % 2); // チャンクは 2 バイト境界

            if (chunkId == "fmt ")
            {
                format = reader.ReadUInt16();
                channels = reader.ReadUInt16();
                hz = (int)reader.ReadUInt32();
                reader.ReadUInt32(); // 平均バイト/秒
                reader.ReadUInt16(); // ブロックサイズ
                bitsPerSample = reader.ReadUInt16();

                // WAVE_FORMAT_EXTENSIBLE は実サブフォーマットを見る
                if (format == 0xFFFE && chunkSize >= 40)
                {
                    var extraSize = reader.ReadUInt16();
                    if (extraSize >= 22)
                    {
                        reader.ReadUInt16(); // 有効ビット数
                        reader.ReadUInt32(); // チャンネルマスク
                        var subFormat = new Guid(reader.ReadBytes(16));
                        // 先頭 4 バイトがフォーマットタグに対応する
                        format = BitConverter.ToInt16(subFormat.ToByteArray(), 0);
                    }
                }
            }
            else if (chunkId == "data")
            {
                data = reader.ReadBytes((int)chunkSize);
            }

            if (stream.Position != next)
            {
                if (next > stream.Length) break;
                stream.Position = next;
            }

            if (data is not null && format != 0) break;
        }

        if (data is null || channels <= 0) throw new InvalidDataException("data チャンクがありません。");

        var mono = Decode(data, format, bitsPerSample, channels);
        return new DanmakuSoundBuffer(ToStereo(mono, channels), hz);
    }

    /// <summary>PCM バイト列を -1〜1 の float 列へ変換する (チャンネル順はそのまま)。</summary>
    private static float[] Decode(byte[] data, int format, int bitsPerSample, int channels)
    {
        const int FormatPcm = 1;
        const int FormatFloat = 3;

        if (format == FormatFloat && bitsPerSample == 32)
        {
            var count = data.Length / 4;
            var result = new float[count];
            Buffer.BlockCopy(data, 0, result, 0, count * 4);
            return result;
        }

        if (format != FormatPcm)
            throw new InvalidDataException($"未対応の WAV 形式です (format={format}, bits={bitsPerSample})。");

        switch (bitsPerSample)
        {
            case 8:
            {
                var result = new float[data.Length];
                for (var i = 0; i < data.Length; i++) result[i] = (data[i] - 128) / 128f;
                return result;
            }
            case 16:
            {
                var count = data.Length / 2;
                var result = new float[count];
                for (var i = 0; i < count; i++)
                    result[i] = BitConverter.ToInt16(data, i * 2) / 32768f;
                return result;
            }
            case 24:
            {
                var count = data.Length / 3;
                var result = new float[count];
                for (var i = 0; i < count; i++)
                {
                    var offset = i * 3;
                    var value = data[offset] | (data[offset + 1] << 8) | ((sbyte)data[offset + 2] << 16);
                    result[i] = value / 8388608f;
                }
                return result;
            }
            case 32:
            {
                var count = data.Length / 4;
                var result = new float[count];
                for (var i = 0; i < count; i++)
                    result[i] = BitConverter.ToInt32(data, i * 4) / 2147483648f;
                return result;
            }
            default:
                throw new InvalidDataException($"未対応のビット深度です ({bitsPerSample}bit)。");
        }
    }

    /// <summary>任意チャンネル数のサンプル列を 2ch インターリーブへ変換する。</summary>
    private static float[] ToStereo(float[] samples, int channels)
    {
        if (channels == 2) return samples;

        var frames = samples.Length / channels;
        var result = new float[frames * 2];

        if (channels == 1)
        {
            for (var i = 0; i < frames; i++)
            {
                result[i * 2] = samples[i];
                result[i * 2 + 1] = samples[i];
            }

            return result;
        }

        // 3ch 以上は先頭 2ch のみを使う
        for (var i = 0; i < frames; i++)
        {
            result[i * 2] = samples[i * channels];
            result[i * 2 + 1] = samples[i * channels + 1];
        }

        return result;
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
