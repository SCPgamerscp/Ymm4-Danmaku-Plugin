using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Importers;

namespace Ymm4DanmakuPlugin.Core.Engine;

/// <summary>
/// エミッター設定から <see cref="IEmitterBehavior"/> を組み立てるファクトリ。
/// 3 階層 (パターン / JSON / BulletML / Lua) の切り替えを一手に引き受ける。
/// </summary>
public static class DanmakuBehaviorFactory
{
    /// <summary>外部データの読み込み結果をキャッシュする (同じ内容を毎フレーム再パースしない)。</summary>
    private static readonly Dictionary<string, DanmakuImportResult> ImportCache = new(StringComparer.Ordinal);

    private const int MaxCacheEntries = 64;

    /// <summary>設定からすべてのエミッター挙動を作る。</summary>
    public static List<IEmitterBehavior> CreateAll(DanmakuSettings settings, ICollection<string>? warnings = null)
    {
        var behaviors = new List<IEmitterBehavior>(settings.Emitters.Length);
        foreach (var emitter in settings.Emitters)
            behaviors.Add(Create(emitter, warnings));
        return behaviors;
    }

    /// <summary>エミッター 1 つぶんの挙動を作る。読み込みに失敗した場合はパターン生成へフォールバックする。</summary>
    public static IEmitterBehavior Create(EmitterSettings emitter, ICollection<string>? warnings = null)
    {
        if (emitter.SourceMode == DanmakuSourceMode.Pattern)
            return new PatternEmitterBehavior(emitter);

        var text = ResolveSourceText(emitter, warnings);
        if (string.IsNullOrWhiteSpace(text))
            return new PatternEmitterBehavior(emitter);

        var result = ImportWithCache(emitter.SourceMode, text);

        if (!result.IsSuccess)
        {
            warnings?.Add($"[{emitter.Name}] {result.Error}");
            return new PatternEmitterBehavior(emitter);
        }

        foreach (var warning in result.Warnings)
            warnings?.Add($"[{emitter.Name}] {warning}");

        if (result.BulletMl is not null)
            return new BulletMlEmitterBehavior(emitter, result.BulletMl);

        if (result.Shots is not null)
            return new ScriptedShotEmitterBehavior(emitter, result.Shots);

        if (result.Preset is not null)
            return new PatternEmitterBehavior(result.Preset.ApplyTo(emitter));

        warnings?.Add($"[{emitter.Name}] 読み込めた弾幕データがありませんでした。");
        return new PatternEmitterBehavior(emitter);
    }

    private static string? ResolveSourceText(EmitterSettings emitter, ICollection<string>? warnings)
    {
        if (!string.IsNullOrWhiteSpace(emitter.SourceText))
            return emitter.SourceText;

        if (string.IsNullOrWhiteSpace(emitter.SourcePath))
        {
            warnings?.Add($"[{emitter.Name}] 外部データのファイルが指定されていません。");
            return null;
        }

        try
        {
            if (!File.Exists(emitter.SourcePath))
            {
                warnings?.Add($"[{emitter.Name}] ファイルが見つかりません: {emitter.SourcePath}");
                return null;
            }

            return File.ReadAllText(emitter.SourcePath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            warnings?.Add($"[{emitter.Name}] ファイルを読み込めません: {e.Message}");
            return null;
        }
    }

    private static DanmakuImportResult ImportWithCache(DanmakuSourceMode mode, string text)
    {
        var key = $"{mode}:{text.GetHashCode()}:{text.Length}";

        lock (ImportCache)
        {
            if (ImportCache.TryGetValue(key, out var cached)) return cached;
        }

        var importer = mode switch
        {
            DanmakuSourceMode.Json => (IDanmakuImporter)new JsonDanmakuImporter(),
            DanmakuSourceMode.BulletMl => new BulletMlDanmakuImporter(),
            DanmakuSourceMode.Lua => new LuaDanmakuImporter(),
            _ => new JsonDanmakuImporter(),
        };

        var result = importer.CanImport(text)
            ? importer.Import(text)
            : DanmakuImporters.ImportText(text);

        lock (ImportCache)
        {
            if (ImportCache.Count >= MaxCacheEntries) ImportCache.Clear();
            ImportCache[key] = result;
        }

        return result;
    }

    /// <summary>キャッシュを破棄する (ファイルを編集した際などに呼ぶ)。</summary>
    public static void ClearCache()
    {
        lock (ImportCache) ImportCache.Clear();
    }
}
