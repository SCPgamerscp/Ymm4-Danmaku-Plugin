using Ymm4DanmakuPlugin.Core.Presets;
using Ymm4DanmakuPlugin.Parameters;
using Ymm4DanmakuPlugin.Settings;

namespace Ymm4DanmakuPlugin.Presets;

/// <summary>
/// プリセットの一覧管理と、エミッター編集項目との相互変換を担う。
/// <para>
/// 走査・統合・ファイル名整形といったロジック自体は
/// <see cref="PresetCatalog"/> (コア側) にあり、ここはその結果をキャッシュして
/// YMM4 の設定 (保存先フォルダ) と編集 UI に繋ぐ薄い層。
/// </para>
/// <para>
/// ファイル I/O は編集 UI から呼ばれるだけで描画ループからは呼ばれないため
/// 同期 I/O で問題ないが、一覧はキャッシュして <see cref="Refresh"/> 時のみ読み直す。
/// </para>
/// </summary>
public static class DanmakuPresetManager
{
    private const string BuiltInExportFileName = "東方風弾幕サンプル集.json";

    private static readonly object Gate = new();

    private static PresetCatalogResult? cache;
    private static string? cachedDirectory;

    /// <summary>ユーザープリセットの保存先フォルダ。</summary>
    public static string UserDirectory => DanmakuSoundSettings.Default.ResolvePresetDirectory();

    /// <summary>プリセット一覧 (組み込み + ユーザーフォルダ)。</summary>
    public static IReadOnlyList<DanmakuPreset> All => Catalog.Presets;

    /// <summary>プリセット名の一覧 (コンボボックス表示用)。</summary>
    public static IReadOnlyList<string> Names => Catalog.Names;

    /// <summary>読み込み時に発生したエラー (UI 表示用)。</summary>
    public static IReadOnlyList<string> LoadErrors => Catalog.Errors;

    private static PresetCatalogResult Catalog
    {
        get
        {
            lock (Gate)
            {
                var directory = UserDirectory;

                // 保存先フォルダが変更されたらキャッシュを捨てる
                if (cache is null || !string.Equals(cachedDirectory, directory, StringComparison.OrdinalIgnoreCase))
                {
                    cache = PresetCatalog.Build(directory);
                    cachedDirectory = directory;
                }

                return cache;
            }
        }
    }

    /// <summary>キャッシュを破棄して次回アクセス時に読み直す。</summary>
    public static void Refresh()
    {
        lock (Gate)
        {
            cache = null;
            cachedDirectory = null;
        }
    }

    /// <summary>名前でプリセットを検索する。見つからなければ null。</summary>
    public static DanmakuPreset? Find(string? name) => Catalog.Find(name);

    /// <summary>プリセットをエミッターへ適用する。適用できたら true。</summary>
    public static bool Apply(string? name, EmitterParameter emitter)
    {
        ArgumentNullException.ThrowIfNull(emitter);

        var preset = Find(name);
        if (preset is null) return false;

        emitter.ApplyPreset(preset);
        return true;
    }

    /// <summary>現在のエミッター設定をユーザーフォルダへ保存する。戻り値は保存先パス。</summary>
    public static string Save(EmitterParameter emitter, string name, string description = "")
    {
        ArgumentNullException.ThrowIfNull(emitter);

        var presetName = string.IsNullOrWhiteSpace(name) ? "新規プリセット" : name.Trim();
        var path = PresetCatalog.BuildPath(EnsureUserDirectory(), presetName);

        // 同名は上書きする (「保存」を繰り返したときに連番が増え続けないように)
        PresetLibrary.Save(emitter.ToPreset(presetName, description), path);
        Refresh();
        return path;
    }

    /// <summary>指定パスへプリセットを書き出す (単体)。</summary>
    public static void ExportTo(EmitterParameter emitter, string path, string name, string description = "")
    {
        ArgumentNullException.ThrowIfNull(emitter);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        PresetLibrary.Save(emitter.ToPreset(name, description), path);
    }

    /// <summary>
    /// ファイルからプリセットを読み込み、先頭のものをエミッターへ適用する。
    /// あわせてユーザーフォルダへコピーして一覧に載せる。
    /// </summary>
    public static DanmakuPreset? Import(string path, EmitterParameter? emitter = null, bool copyToUserDirectory = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var presets = PresetLibrary.Load(path);
        if (presets.Count == 0) return null;

        if (copyToUserDirectory)
        {
            var directory = EnsureUserDirectory();

            foreach (var preset in presets)
            {
                // 同名ファイルがあっても上書きせず、連番を振って共存させる
                var destination = PresetCatalog.MakeUniquePath(PresetCatalog.BuildPath(directory, preset.Name));
                PresetLibrary.Save(preset, destination);
            }

            Refresh();
        }

        emitter?.ApplyPreset(presets[0]);
        return presets[0];
    }

    /// <summary>組み込みプリセット集をユーザーフォルダへ書き出す。戻り値は書き出し先パス。</summary>
    public static string ExportBuiltIn(string? path = null)
    {
        var destination = string.IsNullOrWhiteSpace(path)
            ? Path.Combine(EnsureUserDirectory(), BuiltInExportFileName)
            : path;

        PresetLibrary.ExportBuiltIn(destination);
        Refresh();
        return destination;
    }

    /// <summary>ユーザーフォルダを作成して、そのパスを返す。</summary>
    public static string EnsureUserDirectory()
    {
        var directory = UserDirectory;
        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>ファイル名に使えない文字を '_' に置き換える。</summary>
    internal static string SanitizeFileName(string? name) => PresetCatalog.SanitizeFileName(name);
}
