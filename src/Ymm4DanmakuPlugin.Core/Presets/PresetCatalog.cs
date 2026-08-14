namespace Ymm4DanmakuPlugin.Core.Presets;

/// <summary>
/// フォルダ上のプリセットファイル群を走査して、組み込みプリセットと統合した一覧を作る。
/// <para>
/// プラグイン層 (WPF) からは <c>DanmakuPresetManager</c> が薄いラッパーとして呼び出す。
/// ロジックをここ (プラットフォーム非依存のコア) に置いているのは、
/// Windows 以外でも単体テストできるようにするため。
/// </para>
/// </summary>
public static class PresetCatalog
{
    /// <summary>プリセットファイルの拡張子。</summary>
    public const string Extension = ".json";

    /// <summary>収集したエラーメッセージの保持上限。</summary>
    public const int MaxErrors = 16;

    /// <summary>
    /// 組み込みプリセットとフォルダ内のプリセットを統合した一覧を返す。
    /// <para>
    /// 同名のプリセットがある場合は<b>フォルダ側 (ユーザー定義) が勝つ</b>。
    /// これにより、同梱サンプルと同じ名前で保存すれば内容を差し替えられる。
    /// 並び順は「組み込みの定義順 → 新規に見つかったユーザー定義のファイル名順」。
    /// </para>
    /// </summary>
    /// <param name="directory">走査するフォルダ。null / 存在しない場合は組み込みのみ返す。</param>
    /// <param name="includeBuiltIn">組み込みプリセットを含めるか。</param>
    /// <returns>統合結果と、読み込み中に発生したエラー。</returns>
    public static PresetCatalogResult Build(string? directory, bool includeBuiltIn = true)
    {
        var map = new Dictionary<string, DanmakuPreset>(StringComparer.Ordinal);
        var order = new List<string>();
        var errors = new List<string>();

        void Put(DanmakuPreset preset)
        {
            if (!map.ContainsKey(preset.Name)) order.Add(preset.Name);
            map[preset.Name] = preset;
        }

        if (includeBuiltIn)
        {
            foreach (var preset in PresetLibrary.BuiltIn) Put(preset);
        }

        foreach (var file in EnumerateFiles(directory, errors))
        {
            try
            {
                var presets = PresetLibrary.Load(file);

                if (presets.Count == 0)
                {
                    AddError(errors, $"{Path.GetFileName(file)}: プリセットとして読み取れませんでした。");
                    continue;
                }

                foreach (var preset in presets) Put(preset);
            }
            catch (Exception ex) when (IsRecoverable(ex))
            {
                AddError(errors, $"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        return new PresetCatalogResult([.. order.Select(name => map[name])], errors);
    }

    /// <summary>フォルダ内のプリセットファイルをファイル名順に列挙する。</summary>
    public static IReadOnlyList<string> EnumerateFiles(string? directory) => EnumerateFiles(directory, null);

    /// <summary>
    /// 既存ファイルと衝突しないパスを返す。衝突する場合は " (2)", " (3)" … を付ける。
    /// </summary>
    public static string MakeUniquePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path)) return path;

        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        for (var i = 2; i < 1000; i++)
        {
            var candidate = Path.Combine(directory, $"{name} ({i}){extension}");
            if (!File.Exists(candidate)) return candidate;
        }

        // 1000 個も衝突するのは異常なので、上書きされるのを承知で元のパスを返す
        return path;
    }

    /// <summary>
    /// Windows のファイル名に使えない文字。
    /// <para>
    /// <see cref="Path.GetInvalidFileNameChars"/> は<b>プラットフォーム依存</b>で、
    /// Linux では '/' と '\0' しか返さない。本プラグインは Windows (YMM4) 向けであり、
    /// また Linux の CI 上でも同じ結果になってほしいので、明示的に列挙する。
    /// </para>
    /// </summary>
    private static readonly char[] InvalidFileNameChars =
    [
        '"', '<', '>', '|', ':', '*', '?', '\\', '/',
        // 制御文字 (0x00-0x1F)
        '\u0000', '\u0001', '\u0002', '\u0003', '\u0004', '\u0005', '\u0006', '\u0007',
        '\u0008', '\u0009', '\u000A', '\u000B', '\u000C', '\u000D', '\u000E', '\u000F',
        '\u0010', '\u0011', '\u0012', '\u0013', '\u0014', '\u0015', '\u0016', '\u0017',
        '\u0018', '\u0019', '\u001A', '\u001B', '\u001C', '\u001D', '\u001E', '\u001F',
    ];

    /// <summary>
    /// Windows の予約デバイス名。これらはファイル名として使えないため接尾辞を付ける。
    /// </summary>
    private static readonly string[] ReservedNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    ];

    /// <summary>
    /// ファイル名に使えない文字を '_' へ置き換える。
    /// 空になってしまう場合や予約名の場合は安全な名前へ差し替える。
    /// </summary>
    public static string SanitizeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "preset";

        var buffer = new char[name.Length];

        for (var i = 0; i < name.Length; i++)
        {
            buffer[i] = Array.IndexOf(InvalidFileNameChars, name[i]) >= 0 ? '_' : name[i];
        }

        // 末尾の '.' や空白は Windows で問題を起こすので削る
        var result = new string(buffer).Trim().TrimEnd('.').Trim();
        if (string.IsNullOrEmpty(result)) return "preset";

        // CON.json のような予約名は開けないので回避する
        if (ReservedNames.Contains(result, StringComparer.OrdinalIgnoreCase)) return result + "_";

        return result;
    }

    /// <summary>プリセット名からフォルダ内の保存先パスを組み立てる。</summary>
    public static string BuildPath(string directory, string? presetName) =>
        Path.Combine(directory, SanitizeFileName(presetName) + Extension);

    // -----------------------------------------------------------------------
    // 内部
    // -----------------------------------------------------------------------

    private static IReadOnlyList<string> EnumerateFiles(string? directory, List<string>? errors)
    {
        if (string.IsNullOrWhiteSpace(directory)) return [];

        try
        {
            if (!Directory.Exists(directory)) return [];

            var files = Directory.GetFiles(directory, "*" + Extension, SearchOption.TopDirectoryOnly);

            // 表示順を安定させるためファイル名でソートする
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            return files;
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            if (errors is not null) AddError(errors, $"プリセットフォルダを読めませんでした: {ex.Message}");
            return [];
        }
    }

    private static void AddError(List<string> errors, string message)
    {
        if (errors.Count >= MaxErrors || errors.Contains(message)) return;
        errors.Add(message);
    }

    private static bool IsRecoverable(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException;
}

/// <summary>プリセット一覧の構築結果。</summary>
/// <param name="Presets">統合されたプリセット一覧。</param>
/// <param name="Errors">読み込み中に発生したエラーメッセージ。</param>
public sealed record PresetCatalogResult(
    IReadOnlyList<DanmakuPreset> Presets,
    IReadOnlyList<string> Errors)
{
    /// <summary>プリセット名の一覧。</summary>
    public IReadOnlyList<string> Names => [.. Presets.Select(p => p.Name)];

    /// <summary>名前でプリセットを検索する。完全一致 → 大文字小文字無視の順で探す。</summary>
    public DanmakuPreset? Find(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        return Presets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal))
            ?? Presets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
