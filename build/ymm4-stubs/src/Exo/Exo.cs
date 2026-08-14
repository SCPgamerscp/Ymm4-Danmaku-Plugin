// YMM4 API スタブ (ビルド検証専用)。実装は空で、実行はできません。
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Exo;

/// <summary>AviUtl 関連のディレクトリ情報 (スタブ)。</summary>
public class AviUtlDirectories;

/// <summary>exo 出力に必要な情報 (スタブ)。</summary>
public class ExoOutputDescription(VideoInfo videoInfo, string exoFilesDirectory, AviUtlDirectories aviutl)
{
    public VideoInfo VideoInfo { get; } = videoInfo;

    public string ExoFilesDirectory { get; } = exoFilesDirectory;

    public AviUtlDirectories AviUtl { get; } = aviutl;
}
