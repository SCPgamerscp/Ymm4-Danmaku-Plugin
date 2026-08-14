using Ymm4DanmakuPlugin.Core.Engine;
using Ymm4DanmakuPlugin.Core.Presets;
using Ymm4DanmakuPlugin.Core.Scripting;

namespace Ymm4DanmakuPlugin.Core.Importers;

/// <summary>外部データ読み込みの結果。</summary>
public sealed class DanmakuImportResult
{
    /// <summary>フラットな発射命令列 (JSON / Lua)。</summary>
    public ScriptedShotProgram? Shots { get; init; }

    /// <summary>BulletML プログラム。</summary>
    public BulletMlProgram? BulletMl { get; init; }

    /// <summary>プリセット定義 (JSON でパターン設定を記述した場合)。</summary>
    public DanmakuPreset? Preset { get; init; }

    /// <summary>致命的でない警告。</summary>
    public List<string> Warnings { get; init; } = [];

    /// <summary>失敗した場合のエラーメッセージ。</summary>
    public string? Error { get; init; }

    public bool IsSuccess => Error is null;

    public static DanmakuImportResult Failure(string message) => new() { Error = message };
}

/// <summary>外部弾幕データのインポーター。</summary>
public interface IDanmakuImporter
{
    /// <summary>表示名。</summary>
    string Name { get; }

    /// <summary>対応する拡張子 (先頭のドットを含む、小文字)。</summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>本文の内容からこのインポーターで扱えるか判定する。</summary>
    bool CanImport(string text);

    /// <summary>本文をインポートする。</summary>
    DanmakuImportResult Import(string text);
}

/// <summary>インポーターの登録簿。拡張子と内容から適切なインポーターを選ぶ。</summary>
public static class DanmakuImporters
{
    private static readonly IDanmakuImporter[] All =
    [
        new JsonDanmakuImporter(),
        new BulletMlDanmakuImporter(),
        new LuaDanmakuImporter(),
    ];

    public static IReadOnlyList<IDanmakuImporter> Importers => All;

    /// <summary>ファイル選択ダイアログ用のフィルター文字列。</summary>
    public const string FileDialogFilter =
        "弾幕データ (*.json;*.xml;*.bulletml;*.lua)|*.json;*.xml;*.bulletml;*.lua|" +
        "JSON (*.json)|*.json|" +
        "BulletML (*.xml;*.bulletml)|*.xml;*.bulletml|" +
        "Lua スクリプト (*.lua)|*.lua|" +
        "すべてのファイル (*.*)|*.*";

    /// <summary>拡張子からインポーターを選ぶ。</summary>
    public static IDanmakuImporter? ForExtension(string extension)
    {
        var ext = extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
        return All.FirstOrDefault(i => i.SupportedExtensions.Contains(ext));
    }

    /// <summary>本文の内容からインポーターを推測する。</summary>
    public static IDanmakuImporter? Detect(string text) => All.FirstOrDefault(i => i.CanImport(text));

    /// <summary>ファイルを読み込む。拡張子で判定し、失敗したら内容から推測する。</summary>
    public static DanmakuImportResult ImportFile(string path)
    {
        if (!File.Exists(path))
            return DanmakuImportResult.Failure($"ファイルが見つかりません: {path}");

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return DanmakuImportResult.Failure($"ファイルを読み込めません: {e.Message}");
        }

        var importer = ForExtension(Path.GetExtension(path)) ?? Detect(text);
        return importer is null
            ? DanmakuImportResult.Failure($"対応していないファイル形式です: {Path.GetExtension(path)}")
            : importer.Import(text);
    }

    /// <summary>本文をインポートする (形式は自動判定)。</summary>
    public static DanmakuImportResult ImportText(string text)
    {
        var importer = Detect(text);
        return importer is null
            ? DanmakuImportResult.Failure("弾幕データの形式を判別できませんでした。")
            : importer.Import(text);
    }
}
