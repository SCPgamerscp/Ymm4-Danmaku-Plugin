using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Presets;

namespace Ymm4DanmakuPlugin.Core.Tests;

/// <summary>
/// プリセット一覧の統合ロジックのテスト。
/// 開発計画書の「プリセット管理 (保存 / インポート / エクスポート)」に対応する。
/// </summary>
public class PresetCatalogTests : IDisposable
{
    /// <summary>テストごとに独立した一時フォルダを使う。</summary>
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "ymm4-danmaku-tests-" + Guid.NewGuid().ToString("N"));

    public PresetCatalogTests() => Directory.CreateDirectory(directory);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // 後始末に失敗してもテスト結果には影響させない
        }

        GC.SuppressFinalize(this);
    }

    // =====================================================================
    // 一覧の構築
    // =====================================================================

    [Fact]
    public void フォルダが空なら組み込みプリセットだけが並ぶ()
    {
        var result = PresetCatalog.Build(directory);

        Assert.Equal(PresetLibrary.BuiltIn.Count, result.Presets.Count);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void フォルダが存在しなくてもエラーにならない()
    {
        var result = PresetCatalog.Build(Path.Combine(directory, "存在しない"));

        Assert.Equal(PresetLibrary.BuiltIn.Count, result.Presets.Count);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void フォルダ指定がnullでも組み込みだけ返る()
    {
        var result = PresetCatalog.Build(null);

        Assert.Equal(PresetLibrary.BuiltIn.Count, result.Presets.Count);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void 組み込みを除外できる()
    {
        Write("ユーザー弾", new DanmakuPreset { Name = "ユーザー弾" });

        var result = PresetCatalog.Build(directory, includeBuiltIn: false);

        Assert.Single(result.Presets);
        Assert.Equal("ユーザー弾", result.Presets[0].Name);
    }

    [Fact]
    public void ユーザープリセットが組み込みの後ろに追加される()
    {
        Write("わたしの弾幕", new DanmakuPreset { Name = "わたしの弾幕" });

        var result = PresetCatalog.Build(directory);

        Assert.Equal(PresetLibrary.BuiltIn.Count + 1, result.Presets.Count);
        Assert.Equal("わたしの弾幕", result.Presets[^1].Name);
    }

    [Fact]
    public void 同名のユーザープリセットは組み込みを上書きする()
    {
        Write("全方位リング", new DanmakuPreset
        {
            Name = "全方位リング",
            Description = "差し替え版",
            Pattern = new PatternSettings { Way = 99 },
        });

        var result = PresetCatalog.Build(directory);

        // 件数は増えない
        Assert.Equal(PresetLibrary.BuiltIn.Count, result.Presets.Count);

        var replaced = result.Find("全方位リング");
        Assert.NotNull(replaced);
        Assert.Equal("差し替え版", replaced!.Description);
        Assert.Equal(99, replaced.Pattern.Way);
    }

    [Fact]
    public void 上書きしても組み込みの並び順は保たれる()
    {
        Write("虹輪", new DanmakuPreset { Name = "虹輪", Description = "差し替え" });

        var result = PresetCatalog.Build(directory);
        var expected = PresetLibrary.BuiltIn.Select(p => p.Name).ToArray();

        Assert.Equal(expected, result.Names);
    }

    [Fact]
    public void ユーザープリセットはファイル名順に並ぶ()
    {
        Write("c", new DanmakuPreset { Name = "C弾" });
        Write("a", new DanmakuPreset { Name = "A弾" });
        Write("b", new DanmakuPreset { Name = "B弾" });

        var result = PresetCatalog.Build(directory, includeBuiltIn: false);

        Assert.Equal(["A弾", "B弾", "C弾"], result.Names);
    }

    [Fact]
    public void コレクション形式のファイルから複数まとめて読める()
    {
        var path = Path.Combine(directory, "collection.json");
        PresetLibrary.SaveCollection(new DanmakuPresetCollection
        {
            Name = "自作集",
            Presets = [new DanmakuPreset { Name = "甲" }, new DanmakuPreset { Name = "乙" }],
        }, path);

        var result = PresetCatalog.Build(directory, includeBuiltIn: false);

        Assert.Equal(["甲", "乙"], result.Names);
    }

    [Fact]
    public void 組み込みエクスポートしたファイルを読み戻せる()
    {
        var path = Path.Combine(directory, "sample.json");
        PresetLibrary.ExportBuiltIn(path);

        var result = PresetCatalog.Build(directory, includeBuiltIn: false);

        Assert.Equal(PresetLibrary.BuiltIn.Select(p => p.Name), result.Names);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void 壊れたJSONはエラーとして報告され他は読み込まれる()
    {
        File.WriteAllText(Path.Combine(directory, "broken.json"), "{ これは JSON ではない ");
        Write("zzz", new DanmakuPreset { Name = "健全な弾" });

        var result = PresetCatalog.Build(directory, includeBuiltIn: false);

        Assert.Single(result.Presets);
        Assert.Equal("健全な弾", result.Presets[0].Name);
        Assert.Single(result.Errors);
        Assert.Contains("broken.json", result.Errors[0]);
    }

    [Fact]
    public void エラーは重複せず上限を超えない()
    {
        for (var i = 0; i < PresetCatalog.MaxErrors + 10; i++)
        {
            File.WriteAllText(Path.Combine(directory, $"broken{i:D3}.json"), "{ 壊れている ");
        }

        var result = PresetCatalog.Build(directory, includeBuiltIn: false);

        Assert.Empty(result.Presets);
        Assert.Equal(PresetCatalog.MaxErrors, result.Errors.Count);
        Assert.Equal(result.Errors.Count, result.Errors.Distinct().Count());
    }

    [Fact]
    public void json以外の拡張子は無視される()
    {
        File.WriteAllText(Path.Combine(directory, "メモ.txt"), "これはプリセットではない");
        File.WriteAllText(Path.Combine(directory, "data.xml"), "<root/>");

        var result = PresetCatalog.Build(directory, includeBuiltIn: false);

        Assert.Empty(result.Presets);
        Assert.Empty(result.Errors);
    }

    // =====================================================================
    // 検索
    // =====================================================================

    [Fact]
    public void 名前で検索できる()
    {
        var result = PresetCatalog.Build(null);

        Assert.NotNull(result.Find("螺旋乱舞"));
        Assert.Null(result.Find("存在しない弾幕"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void 空の名前で検索するとnullが返る(string? name)
    {
        var result = PresetCatalog.Build(null);

        Assert.Null(result.Find(name));
    }

    [Fact]
    public void 大文字小文字を無視して検索できる()
    {
        Write("ascii", new DanmakuPreset { Name = "MyPattern" });

        var result = PresetCatalog.Build(directory, includeBuiltIn: false);

        Assert.NotNull(result.Find("mypattern"));
        Assert.Equal("MyPattern", result.Find("MYPATTERN")!.Name);
    }

    [Fact]
    public void 完全一致が大文字小文字無視より優先される()
    {
        Write("a", new DanmakuPreset { Name = "Abc", Description = "小文字混在" });
        Write("b", new DanmakuPreset { Name = "ABC", Description = "全部大文字" });

        var result = PresetCatalog.Build(directory, includeBuiltIn: false);

        Assert.Equal("全部大文字", result.Find("ABC")!.Description);
        Assert.Equal("小文字混在", result.Find("Abc")!.Description);
    }

    // =====================================================================
    // ファイル名の整形
    // =====================================================================

    [Theory]
    [InlineData("普通の名前", "普通の名前")]
    [InlineData("斜線/入り", "斜線_入り")]
    [InlineData("コロン:入り", "コロン_入り")]
    [InlineData("  前後に空白  ", "前後に空白")]
    [InlineData("疑問符?と星*", "疑問符_と星_")]
    [InlineData("縦棒|と山括弧<>", "縦棒_と山括弧__")]
    [InlineData("末尾のドット...", "末尾のドット")]
    [InlineData("改行\n入り", "改行_入り")]
    public void ファイル名を安全な形に整形する(string input, string expected)
    {
        Assert.Equal(expected, PresetCatalog.SanitizeFileName(input));
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("nul")]
    [InlineData("Com1")]
    [InlineData("LPT9")]
    public void Windowsの予約デバイス名は回避される(string input)
    {
        var sanitized = PresetCatalog.SanitizeFileName(input);

        Assert.NotEqual(input, sanitized);
        Assert.StartsWith(input, sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void 整形はプラットフォームに依存しない()
    {
        // Path.GetInvalidFileNameChars() は Linux では '/' しか返さないため、
        // それに依存していると CI と Windows で結果が食い違ってしまう。
        Assert.Equal("a_b_c_d_e", PresetCatalog.SanitizeFileName("a:b*c?d|e"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void 空の名前は既定のファイル名になる(string? input)
    {
        Assert.Equal("preset", PresetCatalog.SanitizeFileName(input));
    }

    [Fact]
    public void 記号だけの名前でも空文字にはならない()
    {
        var sanitized = PresetCatalog.SanitizeFileName("...");

        Assert.False(string.IsNullOrWhiteSpace(sanitized));
        Assert.False(sanitized.EndsWith('.'));
    }

    [Fact]
    public void 整形後のファイル名は実際にファイル作成できる()
    {
        var path = PresetCatalog.BuildPath(directory, "危険/な:名前*です?");

        PresetLibrary.Save(new DanmakuPreset { Name = "危険/な:名前*です?" }, path);

        Assert.True(File.Exists(path));
        Assert.EndsWith(".json", path);
    }

    // =====================================================================
    // 重複しないパスの採番
    // =====================================================================

    [Fact]
    public void 既存ファイルが無ければそのままのパスを返す()
    {
        var path = Path.Combine(directory, "新規.json");

        Assert.Equal(path, PresetCatalog.MakeUniquePath(path));
    }

    [Fact]
    public void 既存ファイルがあれば連番を振る()
    {
        var path = Path.Combine(directory, "重複.json");
        File.WriteAllText(path, "{}");

        var unique = PresetCatalog.MakeUniquePath(path);

        Assert.Equal(Path.Combine(directory, "重複 (2).json"), unique);
    }

    [Fact]
    public void 連番も埋まっていれば次の番号へ進む()
    {
        File.WriteAllText(Path.Combine(directory, "重複.json"), "{}");
        File.WriteAllText(Path.Combine(directory, "重複 (2).json"), "{}");
        File.WriteAllText(Path.Combine(directory, "重複 (3).json"), "{}");

        var unique = PresetCatalog.MakeUniquePath(Path.Combine(directory, "重複.json"));

        Assert.Equal(Path.Combine(directory, "重複 (4).json"), unique);
    }

    [Fact]
    public void 同名インポートを繰り返しても上書きされない()
    {
        var source = new DanmakuPreset { Name = "取り込み弾" };

        for (var i = 0; i < 3; i++)
        {
            var path = PresetCatalog.MakeUniquePath(PresetCatalog.BuildPath(directory, source.Name));
            PresetLibrary.Save(source, path);
        }

        Assert.Equal(3, Directory.GetFiles(directory, "*.json").Length);
    }

    // =====================================================================
    // ファイル列挙
    // =====================================================================

    [Fact]
    public void ファイル列挙はソート済みで返る()
    {
        Write("c", new DanmakuPreset());
        Write("a", new DanmakuPreset());
        Write("b", new DanmakuPreset());

        var files = PresetCatalog.EnumerateFiles(directory)
            .Select(path => Path.GetFileName(path) ?? string.Empty)
            .ToArray();

        Assert.Equal(["a.json", "b.json", "c.json"], files);
    }

    [Fact]
    public void 存在しないフォルダの列挙は空になる()
    {
        Assert.Empty(PresetCatalog.EnumerateFiles(Path.Combine(directory, "無い")));
        Assert.Empty(PresetCatalog.EnumerateFiles(null));
        Assert.Empty(PresetCatalog.EnumerateFiles("  "));
    }

    // =====================================================================
    // 保存 → 読み込みの往復
    // =====================================================================

    [Fact]
    public void 保存したプリセットを読み戻すと内容が一致する()
    {
        var original = new DanmakuPreset
        {
            Name = "往復テスト",
            Description = "保存と読み込みの一致確認",
            Author = "テスト",
            Tags = ["検証"],
            Pattern = new PatternSettings { Kind = PatternKind.Rose, Way = 13, BaseAngle = 42 },
            Physics = new BulletPhysics { Speed = 321, Acceleration = -12 },
            SplitDelay = 1.25,
        };

        var path = PresetCatalog.BuildPath(directory, original.Name);
        PresetLibrary.Save(original, path);

        var loaded = PresetCatalog.Build(directory, includeBuiltIn: false).Find("往復テスト");

        Assert.NotNull(loaded);
        Assert.Equal(original.Description, loaded!.Description);
        Assert.Equal(original.Author, loaded.Author);
        Assert.Equal(PatternKind.Rose, loaded.Pattern.Kind);
        Assert.Equal(13, loaded.Pattern.Way);
        Assert.Equal(42, loaded.Pattern.BaseAngle);
        Assert.Equal(321, loaded.Physics.Speed);
        Assert.Equal(-12, loaded.Physics.Acceleration);
        Assert.Equal(1.25, loaded.SplitDelay);
    }

    [Fact]
    public void 保存で必要なフォルダが自動作成される()
    {
        var nested = Path.Combine(directory, "深い", "階層");
        var path = Path.Combine(nested, "preset.json");

        PresetLibrary.Save(new DanmakuPreset { Name = "自動作成" }, path);

        Assert.True(File.Exists(path));
    }

    // -----------------------------------------------------------------------

    private void Write(string fileName, DanmakuPreset preset) =>
        PresetLibrary.Save(preset, Path.Combine(directory, fileName + ".json"));
}
