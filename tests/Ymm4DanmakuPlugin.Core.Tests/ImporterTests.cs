using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Importers;

namespace Ymm4DanmakuPlugin.Core.Tests;

/// <summary>JSON インポーターのテスト。</summary>
public class JsonImporterTests
{
    private static readonly JsonDanmakuImporter Importer = new();

    [Fact]
    public void 対応拡張子と名前を持つ()
    {
        Assert.Equal("JSON", Importer.Name);
        Assert.Contains(".json", Importer.SupportedExtensions);
    }

    [Theory]
    [InlineData("{}", true)]
    [InlineData("  \n { \"a\": 1 }", true)]
    [InlineData("<bulletml/>", false)]
    [InlineData("fire{}", false)]
    [InlineData("", false)]
    public void CanImportは中かっこ始まりを判定する(string text, bool expected)
    {
        Assert.Equal(expected, Importer.CanImport(text));
    }

    [Fact]
    public void タイムライン形式を読み込める()
    {
        var result = Importer.Import("""
            {
              "version": 1,
              "name": "テスト弾幕",
              "loopDuration": 4.0,
              "shots": [
                { "time": 0.0, "angle": 0,  "way": 8,  "speed": 220 },
                { "time": 0.5, "angle": 45, "way": 16, "speed": 180, "spread": 180 }
              ]
            }
            """);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.NotNull(result.Shots);

        var program = result.Shots!;
        Assert.Equal(2, program.Shots.Count);
        Assert.Equal(4.0, program.LoopDuration);
        Assert.Equal(0.5, program.Duration);

        Assert.Equal(0.0, program.Shots[0].Time);
        Assert.Equal(8, program.Shots[0].Way);
        Assert.Equal(220, program.Shots[0].Speed);
        Assert.Equal(360, program.Shots[0].Spread); // 省略時の既定値

        Assert.Equal(45, program.Shots[1].Angle);
        Assert.Equal(180, program.Shots[1].Spread);
    }

    [Fact]
    public void ショットは時刻昇順にソートされる()
    {
        var result = Importer.Import("""
            {
              "shots": [
                { "time": 2.0 },
                { "time": 0.5 },
                { "time": 1.0 }
              ]
            }
            """);

        var times = result.Shots!.Shots.Select(s => s.Time).ToArray();
        Assert.Equal([0.5, 1.0, 2.0], times);
    }

    [Fact]
    public void すべての属性を解釈できる()
    {
        var result = Importer.Import("""
            {
              "shots": [{
                "time": 1.25, "angle": 30, "aim": true, "way": 5, "spread": 60,
                "speed": 250, "accel": 40, "angularVelocity": 15, "lifetime": 3.5,
                "sprite": 2, "color": "#FF3366", "scale": 1.5,
                "offsetX": 10, "offsetY": -20, "sound": false, "homing": true
              }]
            }
            """);

        Assert.True(result.IsSuccess);
        var shot = result.Shots!.Shots[0];

        Assert.Equal(1.25, shot.Time);
        Assert.Equal(30, shot.Angle);
        Assert.True(shot.AimAtTarget);
        Assert.Equal(5, shot.Way);
        Assert.Equal(60, shot.Spread);
        Assert.Equal(250, shot.Speed);
        Assert.Equal(40, shot.Acceleration);
        Assert.Equal(15, shot.AngularVelocity);
        Assert.Equal(3.5, shot.Lifetime);
        Assert.Equal(2, shot.SpriteIndex);
        Assert.NotNull(shot.Color);
        Assert.Equal(1.5, shot.ScaleFactor);
        Assert.Equal(10, shot.OffsetX);
        Assert.Equal(-20, shot.OffsetY);
        Assert.False(shot.PlaySound);
        Assert.True(shot.Homing);
    }

    [Fact]
    public void 色は16進表記から解釈される()
    {
        var result = Importer.Import("""{ "shots": [{ "color": "#FF0000" }] }""");

        var color = result.Shots!.Shots[0].Color;
        Assert.NotNull(color);
        Assert.Equal(1f, color!.Value.R, 2);
        Assert.Equal(0f, color.Value.G, 2);
        Assert.Equal(0f, color.Value.B, 2);
    }

    [Fact]
    public void 色が空文字ならnullになる()
    {
        Assert.Null(Importer.Import("""{ "shots": [{ "color": "" }] }""").Shots!.Shots[0].Color);
        Assert.Null(Importer.Import("""{ "shots": [{ }] }""").Shots!.Shots[0].Color);
    }

    [Fact]
    public void wayが0以下なら1に補正して警告する()
    {
        var result = Importer.Import("""{ "shots": [{ "way": 0 }, { "way": -3 }] }""");

        Assert.True(result.IsSuccess);
        Assert.All(result.Shots!.Shots, s => Assert.Equal(1, s.Way));
        Assert.Equal(2, result.Warnings.Count);
        Assert.All(result.Warnings, w => Assert.Contains("way", w));
    }

    [Fact]
    public void 時刻が負なら0に補正して警告する()
    {
        var result = Importer.Import("""{ "shots": [{ "time": -1.5 }] }""");

        Assert.Equal(0.0, result.Shots!.Shots[0].Time);
        Assert.Single(result.Warnings);
        Assert.Contains("time", result.Warnings[0]);
    }

    [Fact]
    public void プリセット形式を読み込める()
    {
        var result = Importer.Import("""
            {
              "name": "全方位リング",
              "description": "テスト用",
              "pattern": { "kind": "Circle", "way": 24, "fireInterval": 0.25 },
              "physics": { "speed": 260, "lifetime": 5 },
              "appearance": { "additive": true }
            }
            """);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Preset);
        Assert.Null(result.Shots);

        var preset = result.Preset!;
        Assert.Equal("全方位リング", preset.Name);
        Assert.Equal(PatternKind.Circle, preset.Pattern.Kind);
        Assert.Equal(24, preset.Pattern.Way);
        Assert.Equal(0.25, preset.Pattern.FireInterval);
        Assert.Equal(260, preset.Physics.Speed);
        Assert.True(preset.Appearance.Additive);
    }

    [Fact]
    public void コメントと末尾カンマを許容する()
    {
        var result = Importer.Import("""
            {
              // コメント
              "shots": [
                { "time": 0, "way": 4, },
              ],
            }
            """);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Shots!.Shots);
    }

    [Fact]
    public void 空文字列は失敗する()
    {
        var result = Importer.Import("   ");
        Assert.False(result.IsSuccess);
        Assert.Contains("空", result.Error);
    }

    [Fact]
    public void 壊れたJSONは失敗する()
    {
        var result = Importer.Import("""{ "shots": [ """);
        Assert.False(result.IsSuccess);
        Assert.Contains("解析", result.Error);
    }

    [Fact]
    public void ルートが配列なら失敗する()
    {
        var result = Importer.Import("[1, 2, 3]");
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void shotsもpatternも無ければ失敗する()
    {
        var result = Importer.Import("""{ "hello": "world" }""");
        Assert.False(result.IsSuccess);
        Assert.Contains("shots", result.Error);
    }
}

/// <summary>BulletML インポーターのテスト。</summary>
public class BulletMlImporterTests
{
    private static readonly BulletMlDanmakuImporter Importer = new();

    [Fact]
    public void 対応拡張子と名前を持つ()
    {
        Assert.Equal("BulletML", Importer.Name);
        Assert.Contains(".xml", Importer.SupportedExtensions);
        Assert.Contains(".bulletml", Importer.SupportedExtensions);
        Assert.Contains(".bml", Importer.SupportedExtensions);
    }

    [Theory]
    [InlineData("<bulletml><action label=\"top\"/></bulletml>", true)]
    [InlineData("  <?xml version=\"1.0\"?><bulletml/>", true)]
    [InlineData("<BULLETML/>", true)]  // 大文字小文字を無視
    [InlineData("<root/>", false)]     // bulletml 要素が無い
    [InlineData("{ \"shots\": [] }", false)]
    [InlineData("", false)]
    public void CanImportはbulletml要素を判定する(string text, bool expected)
    {
        Assert.Equal(expected, Importer.CanImport(text));
    }

    [Fact]
    public void BulletMLプログラムを読み込める()
    {
        var result = Importer.Import("""
            <?xml version="1.0" ?>
            <bulletml type="vertical">
              <action label="top">
                <repeat>
                  <times>10</times>
                  <action>
                    <fire><direction type="sequence">37</direction><speed>2</speed><bullet/></fire>
                    <wait>3</wait>
                  </action>
                </repeat>
              </action>
            </bulletml>
            """);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.BulletMl);
        Assert.Single(result.BulletMl!.TopActions);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void topラベルが無い場合は先頭アクションが代用される()
    {
        // パーサーは寛容な設計で、top で始まるラベルが無ければ
        // 最初の <action> をエントリポイントとして採用する。
        var result = Importer.Import("""
            <bulletml><action label="sub"><wait>1</wait></action></bulletml>
            """);

        Assert.True(result.IsSuccess);
        Assert.Single(result.BulletMl!.TopActions);
        Assert.Equal("sub", result.BulletMl.TopActions[0].Label);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void アクションが1つも無ければ警告する()
    {
        var result = Importer.Import("""
            <bulletml><bullet label="b"><speed>2</speed></bullet></bulletml>
            """);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.BulletMl!.TopActions);
        Assert.Single(result.Warnings);
        Assert.Contains("top", result.Warnings[0]);
    }

    [Fact]
    public void 未解決のactionRefは警告になる()
    {
        var result = Importer.Import("""
            <bulletml>
              <action label="top"><actionRef label="missing"/></action>
            </bulletml>
            """);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Warnings, w => w.Contains("missing"));
    }

    [Fact]
    public void 未解決のfireRefは警告になる()
    {
        var result = Importer.Import("""
            <bulletml>
              <action label="top"><fireRef label="nofire"/></action>
            </bulletml>
            """);

        Assert.Contains(result.Warnings, w => w.Contains("nofire"));
    }

    [Fact]
    public void 未解決のrepeat内actionRefは警告になる()
    {
        var result = Importer.Import("""
            <bulletml>
              <action label="top">
                <repeat><times>3</times><actionRef label="ghost"/></repeat>
              </action>
            </bulletml>
            """);

        Assert.Contains(result.Warnings, w => w.Contains("ghost"));
    }

    [Fact]
    public void 解決済みの参照は警告にならない()
    {
        var result = Importer.Import("""
            <bulletml>
              <action label="top"><actionRef label="shoot"/></action>
              <action label="shoot"><fire><bullet/></fire></action>
            </bulletml>
            """);

        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void 壊れたXMLは失敗する()
    {
        var result = Importer.Import("<bulletml><action>");
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }
}

/// <summary>Lua インポーターのテスト。</summary>
public class LuaImporterTests
{
    private static readonly LuaDanmakuImporter Importer = new();

    [Fact]
    public void 対応拡張子と名前を持つ()
    {
        Assert.Equal("Lua", Importer.Name);
        Assert.Contains(".lua", Importer.SupportedExtensions);
    }

    [Theory]
    [InlineData("fire(0, 200)", true)]
    [InlineData("function shoot() end", true)]
    [InlineData("local way = 8", true)]
    [InlineData("-- コメントだけ", true)]
    [InlineData("{ \"shots\": [] }", false)]  // JSON は除外
    [InlineData("<bulletml/>", false)]        // XML は除外
    [InlineData("", false)]
    public void CanImportはLuaらしさを判定する(string text, bool expected)
    {
        Assert.Equal(expected, Importer.CanImport(text));
    }

    [Fact]
    public void fireテーブル記法で発射命令を作れる()
    {
        var result = Importer.Import("""
            fire{ angle = 90, speed = 300, way = 8, spread = 180 }
            """);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Shots);

        var shot = result.Shots!.Shots[0];
        Assert.Equal(0.0, shot.Time);
        Assert.Equal(90, shot.Angle);
        Assert.Equal(300, shot.Speed);
        Assert.Equal(8, shot.Way);
        Assert.Equal(180, shot.Spread);
    }

    [Fact]
    public void fire簡易記法で発射命令を作れる()
    {
        var result = Importer.Import("fire(45, 250, 4)");

        var shot = result.Shots!.Shots[0];
        Assert.Equal(45, shot.Angle);
        Assert.Equal(250, shot.Speed);
        Assert.Equal(4, shot.Way);
    }

    [Fact]
    public void fire簡易記法の省略引数は既定値になる()
    {
        var shot = Importer.Import("fire(30)").Shots!.Shots[0];

        Assert.Equal(30, shot.Angle);
        Assert.Equal(200, shot.Speed); // 既定速度
        Assert.Equal(1, shot.Way);
    }

    [Fact]
    public void waitでフレーム単位に時刻が進む()
    {
        var result = Importer.Import("""
            fire(0)
            wait(30)
            fire(0)
            wait(30)
            fire(0)
            """);

        var times = result.Shots!.Shots.Select(s => s.Time).ToArray();
        Assert.Equal(3, times.Length);
        Assert.Equal(0.0, times[0], 6);
        Assert.Equal(0.5, times[1], 6);  // 30 / 60fps
        Assert.Equal(1.0, times[2], 6);
    }

    [Fact]
    public void waitsecで秒単位に時刻が進む()
    {
        var result = Importer.Import("""
            fire(0)
            waitsec(1.5)
            fire(0)
            """);

        Assert.Equal(1.5, result.Shots!.Shots[1].Time, 6);
    }

    [Fact]
    public void timeとsettimeで時刻を取得_設定できる()
    {
        var result = Importer.Import("""
            settime(2.0)
            fire{ angle = time() * 10 }
            """);

        var shot = result.Shots!.Shots[0];
        Assert.Equal(2.0, shot.Time, 6);
        Assert.Equal(20.0, shot.Angle, 6);
    }

    [Fact]
    public void loopでループ周期を指定できる()
    {
        var result = Importer.Import("""
            loop(3.5)
            fire(0)
            """);

        Assert.Equal(3.5, result.Shots!.LoopDuration, 6);
    }

    [Fact]
    public void randとrandrangeは決定論的()
    {
        const string script = """
            for i = 1, 20 do
              fire{ angle = randrange(0, 360), speed = 100 + rand() * 100 }
            end
            """;

        var a = new LuaDanmakuImporter { Seed = 999 }.Import(script);
        var b = new LuaDanmakuImporter { Seed = 999 }.Import(script);
        var c = new LuaDanmakuImporter { Seed = 1000 }.Import(script);

        var anglesA = a.Shots!.Shots.Select(s => s.Angle).ToArray();
        var anglesB = b.Shots!.Shots.Select(s => s.Angle).ToArray();
        var anglesC = c.Shots!.Shots.Select(s => s.Angle).ToArray();

        Assert.Equal(anglesA, anglesB);           // 同じシードなら同じ結果
        Assert.NotEqual(anglesA, anglesC);        // 別のシードなら別の結果
        Assert.All(anglesA, v => Assert.InRange(v, 0.0, 360.0));
    }

    [Fact]
    public void fpsグローバル変数が使える()
    {
        var result = Importer.Import("fire{ angle = fps }");
        Assert.Equal(60.0, result.Shots!.Shots[0].Angle, 6);
    }

    [Fact]
    public void ループで大量の弾幕を生成できる()
    {
        var result = Importer.Import("""
            local frame = 0
            for shot = 1, 20 do
              for i = 0, 15 do
                fire{ angle = i * 22.5 + shot * 7, speed = 240, sprite = 1 }
              end
              wait(6)
            end
            """);

        Assert.True(result.IsSuccess);
        Assert.Equal(20 * 16, result.Shots!.Shots.Count);
        Assert.Equal(19 * 6 / 60.0, result.Shots.Duration, 6);
    }

    [Fact]
    public void 全属性をテーブルで指定できる()
    {
        var result = Importer.Import("""
            fire{
              angle = 15, speed = 275, way = 3, spread = 45, aim = true,
              sprite = 4, color = "#00FF88", scale = 2.0, lifetime = 4.5,
              accel = 60, turn = -25, offsetx = 5, offsety = 7,
              sound = false, homing = true
            }
            """);

        var shot = result.Shots!.Shots[0];
        Assert.Equal(15, shot.Angle);
        Assert.Equal(275, shot.Speed);
        Assert.Equal(3, shot.Way);
        Assert.Equal(45, shot.Spread);
        Assert.True(shot.AimAtTarget);
        Assert.Equal(4, shot.SpriteIndex);
        Assert.NotNull(shot.Color);
        Assert.Equal(2.0, shot.ScaleFactor);
        Assert.Equal(4.5, shot.Lifetime);
        Assert.Equal(60, shot.Acceleration);
        Assert.Equal(-25, shot.AngularVelocity);
        Assert.Equal(5, shot.OffsetX);
        Assert.Equal(7, shot.OffsetY);
        Assert.False(shot.PlaySound);
        Assert.True(shot.Homing);
    }

    [Fact]
    public void 省略した属性は既定値になる()
    {
        var shot = Importer.Import("fire{}").Shots!.Shots[0];

        Assert.Equal(0, shot.Angle);
        Assert.Equal(200, shot.Speed);
        Assert.Equal(1, shot.Way);
        Assert.Equal(360, shot.Spread);
        Assert.Equal(-1, shot.SpriteIndex);
        Assert.Equal(1.0, shot.ScaleFactor);
        Assert.True(shot.PlaySound);
        Assert.Null(shot.Homing);
        Assert.Null(shot.Color);
    }

    [Fact]
    public void wayが0以下なら1に補正される()
    {
        Assert.Equal(1, Importer.Import("fire{ way = 0 }").Shots!.Shots[0].Way);
        Assert.Equal(1, Importer.Import("fire{ way = -5 }").Shots!.Shots[0].Way);
    }

    [Fact]
    public void 負のwaitは時刻を戻さない()
    {
        var result = Importer.Import("""
            fire(0)
            wait(-100)
            fire(0)
            """);

        Assert.Equal(0.0, result.Shots!.Shots[1].Time, 6);
    }

    [Fact]
    public void fireが呼ばれなければ警告する()
    {
        var result = Importer.Import("local x = 1 + 2");

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Warnings, w => w.Contains("fire"));
        Assert.Empty(result.Shots!.Shots);
    }

    [Fact]
    public void printの出力は警告として渡される()
    {
        var result = Importer.Import("""
            print("デバッグ出力")
            fire(0)
            """);

        Assert.Contains(result.Warnings, w => w.Contains("デバッグ出力"));
    }

    [Fact]
    public void 構文エラーは失敗する()
    {
        var result = Importer.Import("fire(");

        Assert.False(result.IsSuccess);
        Assert.Contains("構文エラー", result.Error);
    }

    [Fact]
    public void 無限ループは実行エラーになる()
    {
        var result = Importer.Import("while true do local x = 1 end");

        Assert.False(result.IsSuccess);
        Assert.Contains("実行エラー", result.Error);
    }

    [Fact]
    public void ファイルIOは提供されない()
    {
        // io / os / require / dofile / loadfile が無いことを確認 (サンドボックス性)
        foreach (var name in new[] { "io", "os", "require", "dofile", "loadfile", "load" })
        {
            var result = Importer.Import($"fire{{ angle = 0 }}\nlocal x = {name}");
            // 未定義グローバルは nil になり、参照自体は失敗しないが呼び出しは失敗する
            var call = Importer.Import($"{name}()");
            Assert.False(call.IsSuccess, $"{name}() が呼び出せてしまう");
            Assert.True(result.IsSuccess);
        }
    }
}

/// <summary>インポーター登録簿のテスト。</summary>
public class DanmakuImportersTests
{
    [Fact]
    public void インポーターが3種類登録されている()
    {
        Assert.Equal(3, DanmakuImporters.Importers.Count);
        Assert.Contains(DanmakuImporters.Importers, i => i.Name == "JSON");
        Assert.Contains(DanmakuImporters.Importers, i => i.Name == "BulletML");
        Assert.Contains(DanmakuImporters.Importers, i => i.Name == "Lua");
    }

    [Theory]
    [InlineData(".json", "JSON")]
    [InlineData("json", "JSON")]      // ドット無しでも解決
    [InlineData(".JSON", "JSON")]     // 大文字でも解決
    [InlineData(".xml", "BulletML")]
    [InlineData(".bulletml", "BulletML")]
    [InlineData(".bml", "BulletML")]
    [InlineData(".lua", "Lua")]
    public void 拡張子からインポーターを選べる(string extension, string expectedName)
    {
        var importer = DanmakuImporters.ForExtension(extension);
        Assert.NotNull(importer);
        Assert.Equal(expectedName, importer!.Name);
    }

    [Fact]
    public void 未対応拡張子はnullになる()
    {
        Assert.Null(DanmakuImporters.ForExtension(".txt"));
        Assert.Null(DanmakuImporters.ForExtension(".png"));
    }

    [Theory]
    [InlineData("""{ "shots": [] }""", "JSON")]
    [InlineData("<bulletml><action label=\"top\"/></bulletml>", "BulletML")]
    [InlineData("fire(0, 200)", "Lua")]
    public void 内容からインポーターを推測できる(string text, string expectedName)
    {
        var importer = DanmakuImporters.Detect(text);
        Assert.NotNull(importer);
        Assert.Equal(expectedName, importer!.Name);
    }

    [Fact]
    public void 判別できない内容はnullになる()
    {
        Assert.Null(DanmakuImporters.Detect("!!!"));
        Assert.Null(DanmakuImporters.Detect(""));
    }

    [Fact]
    public void ImportTextは形式を自動判定する()
    {
        Assert.NotNull(DanmakuImporters.ImportText("""{ "shots": [{ "way": 4 }] }""").Shots);
        Assert.NotNull(DanmakuImporters.ImportText("<bulletml><action label=\"top\"/></bulletml>").BulletMl);
        Assert.NotNull(DanmakuImporters.ImportText("fire(0)").Shots);
    }

    [Fact]
    public void ImportTextは判別失敗時にエラーを返す()
    {
        var result = DanmakuImporters.ImportText("###");
        Assert.False(result.IsSuccess);
        Assert.Contains("判別", result.Error);
    }

    [Fact]
    public void ImportFileは拡張子で判定する()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ymm4danmaku-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var jsonPath = Path.Combine(directory, "sample.json");
            File.WriteAllText(jsonPath, """{ "shots": [{ "time": 0, "way": 6 }] }""");
            var jsonResult = DanmakuImporters.ImportFile(jsonPath);
            Assert.True(jsonResult.IsSuccess);
            Assert.Equal(6, jsonResult.Shots!.Shots[0].Way);

            var bmlPath = Path.Combine(directory, "sample.bulletml");
            File.WriteAllText(bmlPath, """<bulletml><action label="top"><fire><bullet/></fire></action></bulletml>""");
            var bmlResult = DanmakuImporters.ImportFile(bmlPath);
            Assert.True(bmlResult.IsSuccess);
            Assert.NotNull(bmlResult.BulletMl);

            var luaPath = Path.Combine(directory, "sample.lua");
            File.WriteAllText(luaPath, "fire{ angle = 12, speed = 210 }");
            var luaResult = DanmakuImporters.ImportFile(luaPath);
            Assert.True(luaResult.IsSuccess);
            Assert.Equal(12, luaResult.Shots!.Shots[0].Angle);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ImportFileは未知拡張子でも内容から推測する()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ymm4danmaku-{Guid.NewGuid():N}.dat");
        File.WriteAllText(path, """{ "shots": [{ "way": 3 }] }""");
        try
        {
            var result = DanmakuImporters.ImportFile(path);
            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Shots!.Shots[0].Way);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void 存在しないファイルはエラーになる()
    {
        var result = DanmakuImporters.ImportFile(Path.Combine(Path.GetTempPath(), "no-such-file-12345.json"));
        Assert.False(result.IsSuccess);
        Assert.Contains("見つかりません", result.Error);
    }

    [Fact]
    public void ダイアログフィルターに全形式が含まれる()
    {
        var filter = DanmakuImporters.FileDialogFilter;
        Assert.Contains("*.json", filter);
        Assert.Contains("*.xml", filter);
        Assert.Contains("*.bulletml", filter);
        Assert.Contains("*.lua", filter);

        // "説明|パターン" の組が偶数個になっている
        Assert.Equal(0, filter.Split('|').Length % 2);
    }
}
