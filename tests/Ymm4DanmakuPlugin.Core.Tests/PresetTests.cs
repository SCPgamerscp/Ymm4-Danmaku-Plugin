using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Model;
using Ymm4DanmakuPlugin.Core.Presets;
using Ymm4DanmakuPlugin.Core.Serialization;

namespace Ymm4DanmakuPlugin.Core.Tests;

/// <summary>
/// プリセット管理のテスト。
/// 開発計画書の「プリセット保存・読み込み・エクスポート」「東方風サンプル同梱」に対応する。
/// </summary>
public class PresetLibraryTests
{
    /// <summary>開発計画書で同梱を約束した東方風サンプルプリセット。</summary>
    private static readonly string[] ExpectedPresetNames =
    [
        "全方位リング",
        "螺旋乱舞",
        "自機狙い扇",
        "花弁",
        "雨弾",
        "追尾蝶",
        "多段分裂星",
        "疑似レーザー扇",
        "乱れ撃ち",
        "虹輪",
    ];

    [Fact]
    public void 組み込みプリセットが10種類ある()
    {
        Assert.Equal(10, PresetLibrary.BuiltIn.Count);
    }

    [Fact]
    public void 期待するプリセット名が揃っている()
    {
        var names = PresetLibrary.BuiltIn.Select(p => p.Name).ToArray();
        Assert.Equal(ExpectedPresetNames, names);
    }

    [Fact]
    public void プリセット名は重複しない()
    {
        var names = PresetLibrary.BuiltIn.Select(p => p.Name).ToArray();
        Assert.Equal(names.Length, names.Distinct().Count());
    }

    [Fact]
    public void BuiltInは同一インスタンスをキャッシュする()
    {
        Assert.Same(PresetLibrary.BuiltIn, PresetLibrary.BuiltIn);
    }

    [Theory]
    [InlineData("全方位リング")]
    [InlineData("螺旋乱舞")]
    [InlineData("虹輪")]
    public void 名前でプリセットを検索できる(string name)
    {
        var preset = PresetLibrary.Find(name);
        Assert.NotNull(preset);
        Assert.Equal(name, preset!.Name);
    }

    [Fact]
    public void 存在しない名前はnullになる()
    {
        Assert.Null(PresetLibrary.Find("存在しないプリセット"));
        Assert.Null(PresetLibrary.Find(""));
    }

    [Fact]
    public void すべてのプリセットが妥当な設定値を持つ()
    {
        foreach (var preset in PresetLibrary.BuiltIn)
        {
            Assert.False(string.IsNullOrWhiteSpace(preset.Name), "名前が空のプリセットがある");
            Assert.False(string.IsNullOrWhiteSpace(preset.Description), $"{preset.Name}: 説明が空");
            Assert.NotEmpty(preset.Tags);
            Assert.Equal(1, preset.Version);

            // パターン
            Assert.True(preset.Pattern.Way >= 1, $"{preset.Name}: Way が 1 未満");
            Assert.True(preset.Pattern.Stack >= 1, $"{preset.Name}: Stack が 1 未満");
            Assert.True(preset.Pattern.FireInterval > 0, $"{preset.Name}: FireInterval が 0 以下");
            Assert.True(preset.Pattern.BurstCount >= 1, $"{preset.Name}: BurstCount が 1 未満");

            // 物理
            Assert.True(preset.Physics.Lifetime > 0, $"{preset.Name}: Lifetime が 0 以下");
            Assert.True(preset.Physics.MaxSpeed > 0, $"{preset.Name}: MaxSpeed が 0 以下");
            Assert.True(preset.Physics.Damping is > 0 and <= 2.0, $"{preset.Name}: Damping が異常");

            // 見た目
            Assert.True(preset.Appearance.Scale > 0, $"{preset.Name}: Scale が 0 以下");
            Assert.InRange(preset.Appearance.Opacity, 0.0, 1.0);
            Assert.InRange(preset.Appearance.TrailLength, 0, Bullet.MaxTrailLength);
        }
    }

    [Fact]
    public void 追尾蝶はホーミングが有効()
    {
        var preset = PresetLibrary.Find("追尾蝶");
        Assert.NotNull(preset);
        Assert.True(preset!.Physics.HomingEnabled);
        Assert.True(preset.Physics.HomingTurnRate > 0);
    }

    [Fact]
    public void 多段分裂星は分裂設定を持つ()
    {
        var preset = PresetLibrary.Find("多段分裂星");
        Assert.NotNull(preset);
        Assert.NotNull(preset!.Split);
        Assert.True(preset.Split!.Count >= 2);
        Assert.True(preset.SplitDelay > 0);
    }

    [Fact]
    public void 自機狙い扇はターゲットを狙う()
    {
        var preset = PresetLibrary.Find("自機狙い扇");
        Assert.NotNull(preset);
        Assert.True(preset!.Pattern.AimAtTarget);
        Assert.Equal(PatternKind.Aimed, preset.Pattern.Kind);
    }

    [Fact]
    public void 虹輪は虹色モードを使う()
    {
        var preset = PresetLibrary.Find("虹輪");
        Assert.NotNull(preset);
        Assert.True(preset!.Appearance.ColorMode is ColorMode.Rainbow or ColorMode.Gradient or ColorMode.Palette);
    }

    [Fact]
    public void 疑似レーザー扇はレーザー種別()
    {
        var preset = PresetLibrary.Find("疑似レーザー扇");
        Assert.NotNull(preset);
        Assert.Equal(PatternKind.Laser, preset!.Pattern.Kind);
    }
}

/// <summary>プリセットの JSON 入出力・エミッターへの適用のテスト。</summary>
public class PresetSerializationTests
{
    /// <summary>
    /// 色は "#AARRGGBB" (チャンネル 8bit) として保存されるため、
    /// float の値は往復で 1/255 未満の量子化誤差を持つ。
    /// 人に読みやすい形式を優先した意図的な仕様。
    /// </summary>
    private const float ColorTolerance = 1f / 255f + 1e-6f;

    private static void AssertColorClose(BulletColor expected, BulletColor actual)
    {
        Assert.True(Math.Abs(expected.R - actual.R) <= ColorTolerance, $"R: {expected.R} vs {actual.R}");
        Assert.True(Math.Abs(expected.G - actual.G) <= ColorTolerance, $"G: {expected.G} vs {actual.G}");
        Assert.True(Math.Abs(expected.B - actual.B) <= ColorTolerance, $"B: {expected.B} vs {actual.B}");
        Assert.True(Math.Abs(expected.A - actual.A) <= ColorTolerance, $"A: {expected.A} vs {actual.A}");
    }

    /// <summary>色以外の項目を比較できるよう、色を既定値へ丸めたコピーを返す。</summary>
    private static BulletAppearance WithoutColors(BulletAppearance appearance) => appearance with
    {
        PrimaryColor = BulletColor.White,
        SecondaryColor = BulletColor.White,
    };

    [Fact]
    public void JSONへ書き出して読み戻せる()
    {
        foreach (var original in PresetLibrary.BuiltIn)
        {
            var json = original.ToJson();
            var restored = DanmakuPreset.FromJson(json);

            Assert.NotNull(restored);
            Assert.Equal(original.Name, restored!.Name);
            Assert.Equal(original.Description, restored.Description);
            Assert.Equal(original.Tags, restored.Tags);
            Assert.Equal(original.Pattern, restored.Pattern);
            Assert.Equal(original.Physics, restored.Physics);
            Assert.Equal(original.Split, restored.Split);
            Assert.Equal(original.SplitDelay, restored.SplitDelay);

            // 色は 8bit 量子化されるため、色以外を厳密比較してから色を許容誤差付きで比較する
            Assert.Equal(WithoutColors(original.Appearance), WithoutColors(restored.Appearance));
            AssertColorClose(original.Appearance.PrimaryColor, restored.Appearance.PrimaryColor);
            AssertColorClose(original.Appearance.SecondaryColor, restored.Appearance.SecondaryColor);
        }
    }

    [Fact]
    public void 色の往復誤差は8bit量子化の範囲に収まる()
    {
        var original = new BulletColor(0.123f, 0.456f, 0.789f, 0.321f);
        var preset = new DanmakuPreset
        {
            Name = "色テスト",
            Appearance = new BulletAppearance { PrimaryColor = original },
        };

        var restored = DanmakuPreset.FromJson(preset.ToJson())!;

        AssertColorClose(original, restored.Appearance.PrimaryColor);
        // 完全一致ではないことも明示しておく (意図的な仕様)
        Assert.NotEqual(original, restored.Appearance.PrimaryColor);
    }

    [Fact]
    public void JSONはキャメルケースで出力される()
    {
        var json = PresetLibrary.BuiltIn[0].ToJson();

        Assert.Contains("\"name\"", json);
        Assert.Contains("\"pattern\"", json);
        Assert.Contains("\"fireInterval\"", json);
        Assert.DoesNotContain("\"Name\"", json);
        Assert.DoesNotContain("\"FireInterval\"", json);
    }

    [Fact]
    public void 列挙型は文字列で出力される()
    {
        var json = PresetLibrary.Find("自機狙い扇")!.ToJson();

        Assert.Contains("\"aimed\"", json);      // PatternKind.Aimed
        Assert.DoesNotContain("\"kind\": 2", json);
    }

    [Fact]
    public void 色は16進文字列で出力される()
    {
        var preset = PresetLibrary.BuiltIn[0] with
        {
            Appearance = new BulletAppearance { PrimaryColor = new BulletColor(1f, 0f, 0f, 1f) },
        };

        var json = preset.ToJson();

        Assert.Contains("#FFFF0000", json); // AARRGGBB
    }

    [Fact]
    public void 色の16進文字列を読み戻せる()
    {
        var json = """
            {
              "name": "テスト",
              "pattern": { "way": 4 },
              "appearance": { "primaryColor": "#80FF8000" }
            }
            """;

        var preset = DanmakuPreset.FromJson(json);
        Assert.NotNull(preset);

        var color = preset!.Appearance.PrimaryColor;
        Assert.Equal(0x80 / 255f, color.A, 3);
        Assert.Equal(1f, color.R, 3);
        Assert.Equal(0x80 / 255f, color.G, 3);
        Assert.Equal(0f, color.B, 3);
    }

    [Fact]
    public void 色はオブジェクト形式でも読める()
    {
        var json = """
            {
              "name": "テスト",
              "appearance": { "primaryColor": { "r": 0.5, "g": 0.25, "b": 0.75, "a": 1.0 } }
            }
            """;

        var color = DanmakuPreset.FromJson(json)!.Appearance.PrimaryColor;
        Assert.Equal(0.5f, color.R, 4);
        Assert.Equal(0.25f, color.G, 4);
        Assert.Equal(0.75f, color.B, 4);
    }

    [Fact]
    public void 部分的なJSONは既定値で補完される()
    {
        var preset = DanmakuPreset.FromJson("""{ "name": "最小構成" }""");

        Assert.NotNull(preset);
        Assert.Equal("最小構成", preset!.Name);
        Assert.Equal(new PatternSettings(), preset.Pattern);
        Assert.Equal(new BulletPhysics(), preset.Physics);
        Assert.Equal(new BulletAppearance(), preset.Appearance);
        Assert.Null(preset.Split);
    }

    [Fact]
    public void コメントと末尾カンマを許容する()
    {
        var preset = DanmakuPreset.FromJson("""
            {
              // 名前
              "name": "コメント入り",
              "pattern": { "way": 12, },
            }
            """);

        Assert.NotNull(preset);
        Assert.Equal("コメント入り", preset!.Name);
        Assert.Equal(12, preset.Pattern.Way);
    }

    [Fact]
    public void プリセット集を書き出して読み戻せる()
    {
        var collection = new DanmakuPresetCollection
        {
            Name = "テスト集",
            Presets = [.. PresetLibrary.BuiltIn],
        };

        var restored = DanmakuPresetCollection.FromJson(collection.ToJson());

        Assert.NotNull(restored);
        Assert.Equal("テスト集", restored!.Name);
        Assert.Equal(10, restored.Presets.Length);
        Assert.Equal(collection.Presets.Select(p => p.Name), restored.Presets.Select(p => p.Name));
    }

    [Fact]
    public void エミッター設定へ適用できる()
    {
        var preset = PresetLibrary.Find("螺旋乱舞")!;
        var emitter = new EmitterSettings
        {
            SourceMode = DanmakuSourceMode.BulletMl,  // 別モードから
            Name = "元のエミッター",
            X = 100,                                  // 位置は保たれるべき
            Y = -50,
            SeedOffset = 7,
        };

        var applied = preset.ApplyTo(emitter);

        Assert.Equal(DanmakuSourceMode.Pattern, applied.SourceMode);
        Assert.Equal(preset.Pattern, applied.Pattern);
        Assert.Equal(preset.Physics, applied.Physics);
        Assert.Equal(preset.Appearance, applied.Appearance);
        Assert.Equal(preset.Split, applied.Split);
        Assert.Equal(preset.SplitDelay, applied.SplitDelay);

        // プリセットが持たない項目は元の設定を維持する
        Assert.Equal("元のエミッター", applied.Name);
        Assert.Equal(100, applied.X);
        Assert.Equal(-50, applied.Y);
        Assert.Equal(7, applied.SeedOffset);
    }

    [Fact]
    public void エミッター設定からプリセットを作れる()
    {
        var emitter = new EmitterSettings
        {
            Pattern = new PatternSettings { Way = 21, FireInterval = 0.33 },
            Physics = new BulletPhysics { Speed = 333 },
            Appearance = new BulletAppearance { TrailLength = 8 },
            Split = new SplitSpec { Count = 5 },
            SplitDelay = 0.9,
        };

        var preset = DanmakuPreset.FromEmitter(emitter, "自作弾幕", "説明文");

        Assert.Equal("自作弾幕", preset.Name);
        Assert.Equal("説明文", preset.Description);
        Assert.Equal(21, preset.Pattern.Way);
        Assert.Equal(333, preset.Physics.Speed);
        Assert.Equal(8, preset.Appearance.TrailLength);
        Assert.Equal(5, preset.Split!.Count);
        Assert.Equal(0.9, preset.SplitDelay);
    }

    [Fact]
    public void エミッターとプリセットの往復で設定が保たれる()
    {
        var original = PresetLibrary.Find("多段分裂星")!;
        var emitter = original.ApplyTo(new EmitterSettings());
        var roundTripped = DanmakuPreset.FromEmitter(emitter, original.Name, original.Description);

        Assert.Equal(original.Pattern, roundTripped.Pattern);
        Assert.Equal(original.Physics, roundTripped.Physics);
        Assert.Equal(original.Appearance, roundTripped.Appearance);
        Assert.Equal(original.Split, roundTripped.Split);
        Assert.Equal(original.SplitDelay, roundTripped.SplitDelay);
    }

    [Fact]
    public void 不正なJSONは例外になる()
    {
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => DanmakuPreset.FromJson("{ broken"));
    }
}

/// <summary>プリセットのファイル保存・読み込みのテスト。</summary>
public class PresetFileTests
{
    /// <summary>テスト用の一時ディレクトリを使い、後片付けする。</summary>
    private static void InTempDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "ymm4danmaku-preset-" + Guid.NewGuid().ToString("N"));
        try
        {
            action(directory);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void 単体プリセットを保存して読み込める()
    {
        InTempDirectory(directory =>
        {
            var path = Path.Combine(directory, "sub", "preset.json");
            var original = PresetLibrary.Find("花弁")!;

            PresetLibrary.Save(original, path);

            Assert.True(File.Exists(path));           // 中間ディレクトリも作られる
            var loaded = PresetLibrary.Load(path);
            Assert.Single(loaded);

            // 色は 8bit 量子化されるので、色以外の項目を厳密に比較する
            Assert.Equal(original.Name, loaded[0].Name);
            Assert.Equal(original.Description, loaded[0].Description);
            Assert.Equal(original.Pattern, loaded[0].Pattern);
            Assert.Equal(original.Physics, loaded[0].Physics);
            Assert.Equal(original.Split, loaded[0].Split);
            Assert.Equal(original.Appearance.TrailLength, loaded[0].Appearance.TrailLength);
            Assert.Equal(original.Appearance.ColorMode, loaded[0].Appearance.ColorMode);
        });
    }

    [Fact]
    public void プリセット集を保存して読み込める()
    {
        InTempDirectory(directory =>
        {
            var path = Path.Combine(directory, "collection.json");
            var collection = new DanmakuPresetCollection
            {
                Name = "まとめ",
                Presets = [PresetLibrary.BuiltIn[0], PresetLibrary.BuiltIn[1], PresetLibrary.BuiltIn[2]],
            };

            PresetLibrary.SaveCollection(collection, path);
            var loaded = PresetLibrary.Load(path);

            Assert.Equal(3, loaded.Count);
            Assert.Equal(collection.Presets.Select(p => p.Name), loaded.Select(p => p.Name));
        });
    }

    [Fact]
    public void 組み込みプリセットを一括エクスポートできる()
    {
        InTempDirectory(directory =>
        {
            var path = Path.Combine(directory, "builtin.json");

            PresetLibrary.ExportBuiltIn(path);

            Assert.True(File.Exists(path));

            var collection = DanmakuPresetCollection.FromJson(File.ReadAllText(path));
            Assert.NotNull(collection);
            Assert.Equal(10, collection!.Presets.Length);
            Assert.Contains("東方", collection.Name);

            // 読み込み経路でも 10 件揃う
            Assert.Equal(10, PresetLibrary.Load(path).Count);
        });
    }

    [Fact]
    public void 存在しないファイルは空リストになる()
    {
        var path = Path.Combine(Path.GetTempPath(), $"no-such-preset-{Guid.NewGuid():N}.json");
        Assert.Empty(PresetLibrary.Load(path));
    }

    [Fact]
    public void 壊れたファイルは空リストになる()
    {
        InTempDirectory(directory =>
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "broken.json");
            File.WriteAllText(path, "{ これは JSON ではない");

            Assert.Empty(PresetLibrary.Load(path));
        });
    }

    [Fact]
    public void 空のpresets配列は単体プリセットとして解釈される()
    {
        InTempDirectory(directory =>
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "empty.json");
            File.WriteAllText(path, """{ "name": "空集合", "presets": [] }""");

            var loaded = PresetLibrary.Load(path);

            // presets が空なので単体プリセットとして読み直される
            Assert.Single(loaded);
            Assert.Equal("空集合", loaded[0].Name);
        });
    }
}

/// <summary>JSON シリアライズ設定そのもののテスト。</summary>
public class DanmakuJsonTests
{
    private sealed record Sample
    {
        public string Name { get; init; } = string.Empty;
        public PatternKind Kind { get; init; }
        public BulletColor Color { get; init; } = BulletColor.White;
        public string? Optional { get; init; }
    }

    [Fact]
    public void Optionsは整形出力_CompactOptionsは非整形()
    {
        Assert.True(DanmakuJson.Options.WriteIndented);
        Assert.False(DanmakuJson.CompactOptions.WriteIndented);
    }

    [Fact]
    public void SerializeとDeserializeが往復する()
    {
        var sample = new Sample
        {
            Name = "テスト",
            Kind = PatternKind.Spiral,
            Color = new BulletColor(0.5f, 0.5f, 1f, 1f),
        };

        var restored = DanmakuJson.Deserialize<Sample>(DanmakuJson.Serialize(sample));

        Assert.NotNull(restored);
        Assert.Equal(sample.Name, restored!.Name);
        Assert.Equal(sample.Kind, restored.Kind);
        Assert.Equal(sample.Color.R, restored.Color.R, 2);
    }

    [Fact]
    public void nullプロパティは出力されない()
    {
        var json = DanmakuJson.Serialize(new Sample { Name = "x" });
        Assert.DoesNotContain("optional", json);
    }

    [Fact]
    public void 日本語はエスケープされずに出力される()
    {
        var json = DanmakuJson.Serialize(new Sample { Name = "全方位リング" });
        Assert.Contains("全方位リング", json);
        Assert.DoesNotContain("\\u", json);
    }

    [Fact]
    public void プロパティ名の大文字小文字を無視して読める()
    {
        var restored = DanmakuJson.Deserialize<Sample>("""{ "NAME": "大文字", "KIND": "spiral" }""");

        Assert.NotNull(restored);
        Assert.Equal("大文字", restored!.Name);
        Assert.Equal(PatternKind.Spiral, restored.Kind);
    }

    [Fact]
    public void 列挙型は数値でも読める()
    {
        var restored = DanmakuJson.Deserialize<Sample>("""{ "kind": 1 }""");
        Assert.NotNull(restored);
        Assert.Equal((PatternKind)1, restored!.Kind);
    }

    [Fact]
    public void プリセットに画像パスを保持して往復できる()
    {
        var preset = new DanmakuPreset
        {
            Name = "画像テスト",
            ImagePath = "C:/images/bullet.png",
        };

        var json = preset.ToJson();
        Assert.Contains("bullet.png", json);

        var restored = DanmakuPreset.FromJson(json);
        Assert.NotNull(restored);
        Assert.Equal("C:/images/bullet.png", restored!.ImagePath);

        var emitter = preset.ApplyTo(new EmitterSettings());
        Assert.Equal("C:/images/bullet.png", emitter.ImagePath);

        var fromEmitter = DanmakuPreset.FromEmitter(emitter, "復元");
        Assert.Equal("C:/images/bullet.png", fromEmitter.ImagePath);
    }
}
