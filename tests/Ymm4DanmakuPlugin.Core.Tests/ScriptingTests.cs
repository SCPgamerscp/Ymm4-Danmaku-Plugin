using Ymm4DanmakuPlugin.Core.Mathematics;
using Ymm4DanmakuPlugin.Core.Scripting;
using Ymm4DanmakuPlugin.Core.Scripting.Lua;

namespace Ymm4DanmakuPlugin.Core.Tests;

/// <summary>BulletML の数式評価のテスト。</summary>
public class BulletMlExpressionTests
{
    private static BulletMlVariables Vars(
        double[]? parameters = null,
        double rank = 0.5,
        double loopIndex = 0,
        int seed = 1)
        => new(parameters ?? [], rank, loopIndex, new DeterministicRandom(seed));

    [Theory]
    [InlineData("1", 1)]
    [InlineData("1+2", 3)]
    [InlineData("10-3-2", 5)]        // 左結合
    [InlineData("2+3*4", 14)]        // 乗算が優先
    [InlineData("(2+3)*4", 20)]
    [InlineData("7/2", 3.5)]
    [InlineData("7%3", 1)]
    [InlineData("-5+2", -3)]
    [InlineData("--5", 5)]           // 二重の単項マイナス
    [InlineData("+3", 3)]
    [InlineData("1.5*2", 3)]
    [InlineData(" 90 + 10 ", 100)]   // 空白は無視
    public void 四則演算を評価できる(string source, double expected)
    {
        var expression = BulletMlExpression.Parse(source);
        Assert.Equal(expected, expression.Evaluate(Vars()), 9);
    }

    [Fact]
    public void ゼロ除算は0を返す()
    {
        Assert.Equal(0, BulletMlExpression.Parse("5/0").Evaluate(Vars()), 9);
        Assert.Equal(0, BulletMlExpression.Parse("5%0").Evaluate(Vars()), 9);
    }

    [Fact]
    public void 定数式は畳み込まれる()
    {
        var expression = BulletMlExpression.Parse("2*3+4");
        Assert.Equal(10, expression.ConstantValue);
    }

    [Fact]
    public void 変数を含む式は定数扱いされない()
    {
        Assert.Null(BulletMlExpression.Parse("$rand*360").ConstantValue);
        Assert.Null(BulletMlExpression.Parse("$1+1").ConstantValue);
        Assert.Null(BulletMlExpression.Parse("$rank").ConstantValue);
        Assert.Null(BulletMlExpression.Parse("$i").ConstantValue);
    }

    [Fact]
    public void パラメータ変数を参照できる()
    {
        var expression = BulletMlExpression.Parse("$1*100+$2");
        Assert.Equal(2 * 100 + 30, expression.Evaluate(Vars([2, 30])), 9);
    }

    [Fact]
    public void 範囲外のパラメータは0になる()
    {
        Assert.Equal(0, BulletMlExpression.Parse("$5").Evaluate(Vars([1, 2])), 9);
        Assert.Equal(0, BulletMlExpression.Parse("$9").Evaluate(Vars()), 9);
    }

    [Fact]
    public void rankを参照できる()
    {
        var expression = BulletMlExpression.Parse("2+$rank*4");
        Assert.Equal(2 + 0.75 * 4, expression.Evaluate(Vars(rank: 0.75)), 9);
    }

    [Theory]
    [InlineData("$i")]
    [InlineData("$loop.index")]
    [InlineData("$loopIndex")]
    public void ループインデックスの別名を解釈できる(string source)
    {
        var expression = BulletMlExpression.Parse(source);
        Assert.Equal(7, expression.Evaluate(Vars(loopIndex: 7)), 9);
    }

    [Fact]
    public void randは0以上1未満で決定論的()
    {
        var expression = BulletMlExpression.Parse("$rand");

        var first = Enumerable.Range(0, 100)
            .Select(_ => expression.Evaluate(Vars(seed: 42)))
            .ToArray();

        // 同じ乱数器を渡し続けた場合は毎回異なる値になる
        var random = new DeterministicRandom(42);
        var variables = new BulletMlVariables([], 0, 0, random);
        var sequence = Enumerable.Range(0, 100).Select(_ => expression.Evaluate(in variables)).ToArray();

        Assert.All(sequence, v => Assert.InRange(v, 0.0, 1.0));
        Assert.True(sequence.Distinct().Count() > 90, "$rand が実質的に定数になっている");

        // 同じシードで作り直した乱数器なら同じ値を返す (決定論性)
        Assert.All(first, v => Assert.Equal(first[0], v, 12));
    }

    [Fact]
    public void 空文字列は既定値の定数式になる()
    {
        Assert.Equal(3.0, BulletMlExpression.Parse(null, 3).ConstantValue);
        Assert.Equal(3.0, BulletMlExpression.Parse("", 3).ConstantValue);
        Assert.Equal(3.0, BulletMlExpression.Parse("   ", 3).ConstantValue);
    }

    [Theory]
    [InlineData("1+")]
    [InlineData("(1+2")]
    [InlineData("$")]
    [InlineData("$unknown")]
    [InlineData("1 2")]
    [InlineData("abc")]
    public void 不正な式は例外になる(string source)
    {
        Assert.Throws<BulletMlParseException>(() => BulletMlExpression.Parse(source));
    }

    [Fact]
    public void ParseSafeは失敗時に既定値を返す()
    {
        var expression = BulletMlExpression.ParseSafe("1+", defaultValue: 12);
        Assert.Equal(12.0, expression.ConstantValue);
    }

    [Fact]
    public void Zeroは定数0()
    {
        Assert.Equal(0.0, BulletMlExpression.Zero.ConstantValue);
    }
}

/// <summary>BulletML XML パーサーのテスト。</summary>
public class BulletMlParserTests
{
    private const string SimpleXml = """
        <?xml version="1.0" ?>
        <bulletml type="vertical">
          <action label="top">
            <fire>
              <direction type="absolute">90</direction>
              <speed>2</speed>
              <bulletRef label="mine"/>
            </fire>
            <wait>10</wait>
          </action>
          <bullet label="mine">
            <speed>3</speed>
          </bullet>
        </bulletml>
        """;

    [Fact]
    public void ラベル付きのaction_bulletを収集する()
    {
        var program = BulletMlParser.Parse(SimpleXml);

        Assert.True(program.Actions.ContainsKey("top"));
        Assert.True(program.Bullets.ContainsKey("mine"));
        Assert.Single(program.TopActions);
        Assert.False(program.IsHorizontal);
    }

    [Fact]
    public void horizontal型を判別する()
    {
        var xml = SimpleXml.Replace("type=\"vertical\"", "type=\"horizontal\"");
        Assert.True(BulletMlParser.Parse(xml).IsHorizontal);
    }

    [Fact]
    public void topで始まる複数のアクションがエントリポイントになる()
    {
        var xml = """
            <bulletml>
              <action label="top1"><wait>1</wait></action>
              <action label="top2"><wait>1</wait></action>
              <action label="sub"><wait>1</wait></action>
            </bulletml>
            """;

        var program = BulletMlParser.Parse(xml);
        Assert.Equal(2, program.TopActions.Count);
        Assert.Equal(3, program.Actions.Count);
    }

    [Fact]
    public void 命令列の順序が保持される()
    {
        var xml = """
            <bulletml>
              <action label="top">
                <changeSpeed><speed>5</speed><term>10</term></changeSpeed>
                <wait>3</wait>
                <vanish/>
              </action>
            </bulletml>
            """;

        var commands = BulletMlParser.Parse(xml).Actions["top"].Commands;

        Assert.Collection(
            commands,
            c => Assert.IsType<BulletMlChangeSpeed>(c),
            c => Assert.IsType<BulletMlWait>(c),
            c => Assert.IsType<BulletMlVanish>(c));
    }

    [Fact]
    public void repeatはtimesとactionを持つ()
    {
        var xml = """
            <bulletml>
              <action label="top">
                <repeat>
                  <times>8</times>
                  <action><fire><direction type="sequence">45</direction><bullet/></fire></action>
                </repeat>
              </action>
            </bulletml>
            """;

        var repeat = Assert.IsType<BulletMlRepeat>(BulletMlParser.Parse(xml).Actions["top"].Commands[0]);
        Assert.Equal(8.0, repeat.Times.ConstantValue);
        Assert.NotNull(repeat.Action.Inline);
    }

    [Fact]
    public void directionのtype属性を解釈する()
    {
        var xml = """
            <bulletml>
              <action label="top">
                <fire><direction type="absolute">0</direction><bullet/></fire>
                <fire><direction type="relative">0</direction><bullet/></fire>
                <fire><direction type="sequence">0</direction><bullet/></fire>
                <fire><direction type="aim">0</direction><bullet/></fire>
                <fire><direction>0</direction><bullet/></fire>
              </action>
            </bulletml>
            """;

        var fires = BulletMlParser.Parse(xml).Actions["top"].Commands
            .OfType<BulletMlFireRef>()
            .Select(f => f.Inline!.DirectionType)
            .ToArray();

        Assert.Equal(
        [
            BulletMlDirectionType.Absolute,
            BulletMlDirectionType.Relative,
            BulletMlDirectionType.Sequence,
            BulletMlDirectionType.Aim,
            BulletMlDirectionType.Aim, // 省略時は aim
        ], fires);
    }

    [Fact]
    public void actionRefのパラメータを解釈する()
    {
        var xml = """
            <bulletml>
              <action label="top">
                <actionRef label="shoot">
                  <param>90</param>
                  <param>2+1</param>
                </actionRef>
              </action>
              <action label="shoot"><wait>$1</wait></action>
            </bulletml>
            """;

        var reference = Assert.IsType<BulletMlActionRef>(BulletMlParser.Parse(xml).Actions["top"].Commands[0]);
        Assert.Equal("shoot", reference.Label);
        Assert.Equal(2, reference.Parameters.Count);
        Assert.Equal(90.0, reference.Parameters[0].ConstantValue);
        Assert.Equal(3.0, reference.Parameters[1].ConstantValue);
    }

    [Fact]
    public void 参照は解決できる()
    {
        var program = BulletMlParser.Parse(SimpleXml);
        var fire = Assert.IsType<BulletMlFireRef>(program.Actions["top"].Commands[0]);

        var bullet = program.ResolveBullet(fire.Inline!.Bullet);
        Assert.NotNull(bullet);
        Assert.Equal("mine", bullet!.Label);
    }

    [Fact]
    public void 未知のラベル参照はnullになる()
    {
        var program = BulletMlParser.Parse(SimpleXml);
        Assert.Null(program.ResolveAction(new BulletMlActionRef("nope", null, [])));
        Assert.Null(program.ResolveBullet(new BulletMlBulletRef("nope", null, [])));
        Assert.Null(program.ResolveFire(new BulletMlFireRef("nope", null, [])));
    }

    [Fact]
    public void 壊れたXMLは例外になる()
    {
        Assert.Throws<BulletMlParseException>(() => BulletMlParser.Parse("<bulletml><action>"));
    }

    [Fact]
    public void bulletml要素が無い場合は例外になる()
    {
        Assert.Throws<BulletMlParseException>(() => BulletMlParser.Parse("<root><action label=\"top\"/></root>"));
    }

    [Fact]
    public void Emptyプログラムは空()
    {
        Assert.Empty(BulletMlProgram.Empty.Actions);
        Assert.Empty(BulletMlProgram.Empty.TopActions);
    }
}

/// <summary>BulletML ランナー (ミニ VM) のテスト。</summary>
public class BulletMlRunnerTests
{
    /// <summary>テスト用のホスト実装。発射内容を記録する。</summary>
    private sealed class TestHost : IBulletMlHost
    {
        public Vec2 SelfPosition { get; set; }
        public double SelfDirection { get; set; }
        public double SelfSpeed { get; set; } = 1.0;
        public Vec2 TargetPosition { get; set; } = new(0, 100); // 真下 (エンジン角 90 度)
        public double Rank { get; set; } = 0.5;
        public DeterministicRandom Random { get; } = new(12345);

        public List<(double Direction, double Speed, BulletMlBullet? Definition, BulletMlRunner? Runner)> Fired { get; } = [];
        public int VanishCount { get; private set; }
        public int ChangeCount { get; private set; }
        public Vec2 AccumulatedVelocityDelta { get; private set; }

        public void Fire(double direction, double speed, BulletMlBullet? definition, BulletMlRunner? runner) =>
            Fired.Add((direction, speed, definition, runner));

        public void Vanish() => VanishCount++;

        public void ApplyVelocityDelta(double deltaVx, double deltaVy) =>
            AccumulatedVelocityDelta += new Vec2(deltaVx, deltaVy);

        public void NotifyChange() => ChangeCount++;
    }

    private static BulletMlRunner Runner(string xml, bool loop = false)
    {
        var program = BulletMlParser.Parse(xml);
        return new BulletMlRunner(program, program.TopActions[0]) { Loop = loop };
    }

    private static string Top(string body) => $"""
        <bulletml>
          <action label="top">{body}</action>
        </bulletml>
        """;

    [Fact]
    public void 単発のfireで1発だけ発射される()
    {
        var host = new TestHost();
        var runner = Runner(Top("""<fire><direction type="absolute">0</direction><speed>2</speed><bullet/></fire>"""));

        runner.StepFrame(host);

        Assert.Single(host.Fired);
        // BulletML の絶対角 0 は「上方向」→ エンジン角 -90 度
        Assert.Equal(-90, host.Fired[0].Direction, 6);
        Assert.Equal(2, host.Fired[0].Speed, 6);
    }

    [Fact]
    public void 絶対角はBulletML基準からエンジン基準へ変換される()
    {
        var host = new TestHost();
        var runner = Runner(Top("""
            <fire><direction type="absolute">0</direction><bullet/></fire>
            <fire><direction type="absolute">90</direction><bullet/></fire>
            <fire><direction type="absolute">180</direction><bullet/></fire>
            <fire><direction type="absolute">270</direction><bullet/></fire>
            """));

        runner.StepFrame(host);

        var angles = host.Fired.Select(f => DanmakuMath.NormalizeAngle360(f.Direction)).ToArray();
        Assert.Equal([270, 0, 90, 180], angles.Select(a => Math.Round(a)).ToArray());
    }

    [Fact]
    public void aim指定はターゲット方向を基準にする()
    {
        var host = new TestHost { TargetPosition = new Vec2(100, 0) }; // 真右 = エンジン角 0
        var runner = Runner(Top("""
            <fire><direction type="aim">0</direction><bullet/></fire>
            <fire><direction type="aim">30</direction><bullet/></fire>
            """));

        runner.StepFrame(host);

        Assert.Equal(0, host.Fired[0].Direction, 6);
        Assert.Equal(30, host.Fired[1].Direction, 6);
    }

    [Fact]
    public void relative指定は自弾の進行方向を基準にする()
    {
        var host = new TestHost { SelfDirection = 45 };
        var runner = Runner(Top("""<fire><direction type="relative">30</direction><bullet/></fire>"""));

        runner.StepFrame(host);

        Assert.Equal(75, host.Fired[0].Direction, 6);
    }

    [Fact]
    public void sequence指定は直前の発射方向へ加算される()
    {
        var host = new TestHost();
        var runner = Runner(Top("""
            <fire><direction type="absolute">0</direction><bullet/></fire>
            <fire><direction type="sequence">30</direction><bullet/></fire>
            <fire><direction type="sequence">30</direction><bullet/></fire>
            """));

        runner.StepFrame(host);

        Assert.Equal(-90, host.Fired[0].Direction, 6);
        Assert.Equal(-60, host.Fired[1].Direction, 6);
        Assert.Equal(-30, host.Fired[2].Direction, 6);
    }

    [Fact]
    public void waitは指定フレーム数だけ実行を止める()
    {
        var host = new TestHost();
        var runner = Runner(Top("""
            <fire><direction type="absolute">0</direction><bullet/></fire>
            <wait>3</wait>
            <fire><direction type="absolute">0</direction><bullet/></fire>
            """));

        runner.StepFrame(host);
        Assert.Single(host.Fired);

        runner.StepFrame(host); // wait 消化 1
        runner.StepFrame(host); // wait 消化 2
        Assert.Single(host.Fired);

        runner.StepFrame(host); // wait 明け → 2 発目
        Assert.Equal(2, host.Fired.Count);
    }

    [Fact]
    public void repeatで指定回数繰り返す()
    {
        var host = new TestHost();
        var runner = Runner(Top("""
            <repeat>
              <times>8</times>
              <action><fire><direction type="sequence">45</direction><bullet/></fire></action>
            </repeat>
            """));

        runner.StepFrame(host);

        Assert.Equal(8, host.Fired.Count);
    }

    [Fact]
    public void repeat内で_iがループ回数になる()
    {
        var host = new TestHost();
        var runner = Runner(Top("""
            <repeat>
              <times>4</times>
              <action><fire><direction type="absolute">$i*90</direction><bullet/></fire></action>
            </repeat>
            """));

        runner.StepFrame(host);

        var angles = host.Fired.Select(f => Math.Round(DanmakuMath.NormalizeAngle360(f.Direction))).ToArray();
        Assert.Equal([270, 0, 90, 180], angles);
    }

    [Fact]
    public void timesが0以下なら何も起こらない()
    {
        var host = new TestHost();
        var runner = Runner(Top("""
            <repeat><times>0</times><action><fire><bullet/></fire></action></repeat>
            """));

        runner.StepFrame(host);

        Assert.Empty(host.Fired);
    }

    [Fact]
    public void vanishでホストが消滅し実行が終わる()
    {
        var host = new TestHost();
        var runner = Runner(Top("""
            <vanish/>
            <fire><bullet/></fire>
            """));

        runner.StepFrame(host);

        Assert.Equal(1, host.VanishCount);
        Assert.Empty(host.Fired);
        Assert.True(runner.IsFinished);
    }

    [Fact]
    public void changeDirectionはtermフレームかけて向きを変える()
    {
        var host = new TestHost { SelfDirection = 0 };
        var runner = Runner(Top("""
            <changeDirection><direction type="absolute">180</direction><term>10</term></changeDirection>
            <wait>20</wait>
            """));

        // absolute 180 → エンジン角 90。0 度から term=10 フレームで 90 度へ。
        // 漸次変化はフレーム先頭で適用されるため、命令を実行したフレームの
        // 次のフレームから効き始める。よって term + 1 フレーム進める。
        for (var i = 0; i < 11; i++) runner.StepFrame(host);

        Assert.Equal(90, host.SelfDirection, 4);
        Assert.Equal(1, host.ChangeCount);
    }

    [Fact]
    public void changeSpeedはtermフレームかけて速度を変える()
    {
        var host = new TestHost { SelfSpeed = 1.0 };
        var runner = Runner(Top("""
            <changeSpeed><speed>5</speed><term>4</term></changeSpeed>
            <wait>20</wait>
            """));

        // term=4。命令実行フレーム + 4 フレームで到達する。
        for (var i = 0; i < 5; i++) runner.StepFrame(host);

        Assert.Equal(5.0, host.SelfSpeed, 6);
    }

    [Fact]
    public void changeSpeedのrelativeは現在速度への加算になる()
    {
        var host = new TestHost { SelfSpeed = 2.0 };
        var runner = Runner(Top("""
            <changeSpeed><speed type="relative">3</speed><term>3</term></changeSpeed>
            <wait>20</wait>
            """));

        for (var i = 0; i < 4; i++) runner.StepFrame(host);

        Assert.Equal(5.0, host.SelfSpeed, 6);
    }

    [Fact]
    public void accelは速度ベクトルへ増分を加える()
    {
        var host = new TestHost { SelfDirection = 0, SelfSpeed = 0 };
        var runner = Runner(Top("""
            <accel>
              <horizontal type="relative">4</horizontal>
              <vertical type="relative">2</vertical>
              <term>4</term>
            </accel>
            <wait>20</wait>
            """));

        for (var i = 0; i < 5; i++) runner.StepFrame(host);

        // 1 フレームあたり (1, 0.5) が term=4 フレームぶん加算される
        Assert.Equal(4.0, host.AccumulatedVelocityDelta.X, 6);
        Assert.Equal(2.0, host.AccumulatedVelocityDelta.Y, 6);
    }

    [Fact]
    public void Loopが真なら終了後に先頭から繰り返す()
    {
        var host = new TestHost();
        var runner = Runner(Top("""
            <fire><direction type="absolute">0</direction><bullet/></fire>
            <wait>2</wait>
            """), loop: true);

        for (var i = 0; i < 6; i++) runner.StepFrame(host);

        Assert.True(host.Fired.Count >= 2, $"ループしていない (Fired={host.Fired.Count})");
    }

    [Fact]
    public void Loopが偽なら一度で終わる()
    {
        var host = new TestHost();
        var runner = Runner(Top("""<fire><direction type="absolute">0</direction><bullet/></fire>"""));

        for (var i = 0; i < 10; i++) runner.StepFrame(host);

        Assert.Single(host.Fired);
        Assert.True(runner.IsFinished);
    }

    [Fact]
    public void UpdateはFrameRateに従ってフレームへ換算される()
    {
        var host = new TestHost();
        var program = BulletMlParser.Parse(Top("""
            <repeat>
              <times>100</times>
              <action><fire><direction type="absolute">0</direction><bullet/></fire><wait>1</wait></action>
            </repeat>
            """));
        var runner = new BulletMlRunner(program, program.TopActions[0]) { FrameRate = 60 };

        runner.Update(host, 0.5); // 0.5 秒 = 30 フレーム

        Assert.InRange(host.Fired.Count, 29, 31);
    }

    [Fact]
    public void bulletにactionがあると子ランナーが渡される()
    {
        var host = new TestHost();
        var runner = Runner("""
            <bulletml>
              <action label="top">
                <fire>
                  <direction type="absolute">0</direction>
                  <bullet>
                    <action><changeSpeed><speed>0</speed><term>30</term></changeSpeed></action>
                  </bullet>
                </fire>
              </action>
            </bulletml>
            """);

        runner.StepFrame(host);

        Assert.Single(host.Fired);
        Assert.NotNull(host.Fired[0].Runner);
    }

    [Fact]
    public void actionのないbulletには子ランナーが付かない()
    {
        var host = new TestHost();
        var runner = Runner(Top("""<fire><direction type="absolute">0</direction><bullet/></fire>"""));

        runner.StepFrame(host);

        Assert.Null(host.Fired[0].Runner);
    }

    [Fact]
    public void fireRefのパラメータが式へ渡る()
    {
        var host = new TestHost();
        var runner = Runner("""
            <bulletml>
              <action label="top">
                <fireRef label="shoot"><param>135</param><param>4</param></fireRef>
              </action>
              <fire label="shoot">
                <direction type="absolute">$1</direction>
                <speed>$2</speed>
                <bullet/>
              </fire>
            </bulletml>
            """);

        runner.StepFrame(host);

        Assert.Single(host.Fired);
        Assert.Equal(45, DanmakuMath.NormalizeAngle(host.Fired[0].Direction), 6); // 135-90
        Assert.Equal(4, host.Fired[0].Speed, 6);
    }

    [Fact]
    public void 速度省略時は1になる()
    {
        var host = new TestHost();
        var runner = Runner(Top("""<fire><direction type="absolute">0</direction><bullet/></fire>"""));

        runner.StepFrame(host);

        Assert.Equal(1.0, host.Fired[0].Speed, 6);
    }

    [Fact]
    public void bullet側のdirection_speedが使われる()
    {
        var host = new TestHost();
        var runner = Runner("""
            <bulletml>
              <action label="top"><fire><bulletRef label="b"/></fire></action>
              <bullet label="b">
                <direction type="absolute">180</direction>
                <speed>7</speed>
              </bullet>
            </bulletml>
            """);

        runner.StepFrame(host);

        Assert.Equal(90, DanmakuMath.NormalizeAngle(host.Fired[0].Direction), 6);
        Assert.Equal(7, host.Fired[0].Speed, 6);
    }

    [Fact]
    public void Resetで初期状態に戻る()
    {
        var host = new TestHost();
        var runner = Runner(Top("""
            <fire><direction type="absolute">0</direction><bullet/></fire>
            <wait>5</wait>
            """));

        runner.StepFrame(host);
        Assert.Single(host.Fired);

        runner.Reset();
        host.Fired.Clear();
        runner.StepFrame(host);

        Assert.Single(host.Fired);
    }

    [Fact]
    public void 無限ループでも1フレームの命令数が制限される()
    {
        var host = new TestHost();
        // wait のない repeat の入れ子。制限がなければ無限ループする。
        var runner = Runner(Top("""
            <repeat>
              <times>100000</times>
              <action><fire><direction type="sequence">1</direction><bullet/></fire></action>
            </repeat>
            """));

        runner.StepFrame(host); // ハングしなければ合格

        Assert.True(host.Fired.Count <= 512, $"命令数制限が効いていない ({host.Fired.Count})");
    }
}

/// <summary>Lua サブセットインタプリタのテスト。</summary>
public class LuaInterpreterTests
{
    private static LuaInterpreter Run(string source)
    {
        var interpreter = new LuaInterpreter();
        interpreter.Execute(source);
        return interpreter;
    }

    [Fact]
    public void ローカル変数とグローバル変数を扱える()
    {
        var lua = Run("""
            g = 10
            local l = 20
            g = g + l
            """);

        Assert.Equal(30.0, LuaOps.ToNumber(lua.GetGlobal("g")));
    }

    [Theory]
    [InlineData("x = 1+2*3", 7)]
    [InlineData("x = (1+2)*3", 9)]
    [InlineData("x = 2^10", 1024)]
    [InlineData("x = 7 % 3", 1)]
    [InlineData("x = -3 + 1", -2)]
    [InlineData("x = 10 / 4", 2.5)]
    public void 算術演算子を評価できる(string source, double expected)
    {
        Assert.Equal(expected, LuaOps.ToNumber(Run(source).GetGlobal("x")), 9);
    }

    [Theory]
    [InlineData("x = 1 < 2", true)]
    [InlineData("x = 2 <= 2", true)]
    [InlineData("x = 3 > 4", false)]
    [InlineData("x = 1 == 1", true)]
    [InlineData("x = 1 ~= 1", false)]
    [InlineData("x = nil == nil", true)]
    [InlineData("x = 'a' == 'a'", true)]
    [InlineData("x = true and false", false)]
    [InlineData("x = false or true", true)]
    [InlineData("x = not nil", true)]
    public void 比較_論理演算子を評価できる(string source, bool expected)
    {
        Assert.Equal(expected, LuaOps.IsTruthy(Run(source).GetGlobal("x")));
    }

    [Fact]
    public void 文字列連結ができる()
    {
        Assert.Equal("ab1", Run("x = 'a' .. \"b\" .. 1").GetGlobal("x"));
    }

    [Fact]
    public void if_elseif_elseを分岐できる()
    {
        const string template = """
            n = {0}
            if n < 0 then
              r = "minus"
            elseif n == 0 then
              r = "zero"
            else
              r = "plus"
            end
            """;

        Assert.Equal("minus", Run(template.Replace("{0}", "-1")).GetGlobal("r"));
        Assert.Equal("zero", Run(template.Replace("{0}", "0")).GetGlobal("r"));
        Assert.Equal("plus", Run(template.Replace("{0}", "5")).GetGlobal("r"));
    }

    [Fact]
    public void 数値forループを実行できる()
    {
        var lua = Run("""
            sum = 0
            for i = 1, 10 do sum = sum + i end
            """);

        Assert.Equal(55.0, LuaOps.ToNumber(lua.GetGlobal("sum")));
    }

    [Fact]
    public void forループのstepを指定できる()
    {
        var lua = Run("""
            n = 0
            for i = 10, 1, -2 do n = n + 1 end
            """);

        Assert.Equal(5.0, LuaOps.ToNumber(lua.GetGlobal("n")));
    }

    [Fact]
    public void whileループとbreakが動く()
    {
        var lua = Run("""
            i = 0
            while true do
              i = i + 1
              if i >= 4 then break end
            end
            """);

        Assert.Equal(4.0, LuaOps.ToNumber(lua.GetGlobal("i")));
    }

    [Fact]
    public void 関数を定義して呼び出せる()
    {
        var lua = Run("""
            function add(a, b)
              return a + b
            end
            x = add(3, 4)
            """);

        Assert.Equal(7.0, LuaOps.ToNumber(lua.GetGlobal("x")));
    }

    [Fact]
    public void 再帰関数が動く()
    {
        var lua = Run("""
            function fact(n)
              if n <= 1 then return 1 end
              return n * fact(n - 1)
            end
            x = fact(6)
            """);

        Assert.Equal(720.0, LuaOps.ToNumber(lua.GetGlobal("x")));
    }

    [Fact]
    public void テーブルの配列部と連想部を扱える()
    {
        var lua = Run("""
            t = { 10, 20, 30, name = "danmaku" }
            t[4] = 40
            t.extra = true
            """);

        var table = Assert.IsType<LuaTable>(lua.GetGlobal("t"));
        Assert.Equal(4, table.Length);
        Assert.Equal(10.0, LuaOps.ToNumber(table.Get(1.0)));
        Assert.Equal(40.0, LuaOps.ToNumber(table.Get(4.0)));
        Assert.Equal("danmaku", table.GetString("name"));
        Assert.True(table.GetBoolean("extra"));
    }

    [Fact]
    public void テーブルのGetNumberは既定値を返す()
    {
        var table = new LuaTable();
        table.Set("speed", 3.5);

        Assert.Equal(3.5, table.GetNumber("speed"));
        Assert.Equal(99.0, table.GetNumber("missing", 99));
        Assert.False(table.Has("missing"));
    }

    [Fact]
    public void math関数が使える()
    {
        var lua = Run("""
            a = math.floor(3.7)
            b = math.sqrt(16)
            c = math.abs(-5)
            d = math.max(1, 9, 4)
            e = math.min(1, 9, 4)
            f = math.rad(180)
            g = math.deg(math.pi)
            h = math.atan2(1, 1)
            """);

        Assert.Equal(3.0, LuaOps.ToNumber(lua.GetGlobal("a")));
        Assert.Equal(4.0, LuaOps.ToNumber(lua.GetGlobal("b")));
        Assert.Equal(5.0, LuaOps.ToNumber(lua.GetGlobal("c")));
        Assert.Equal(9.0, LuaOps.ToNumber(lua.GetGlobal("d")));
        Assert.Equal(1.0, LuaOps.ToNumber(lua.GetGlobal("e")));
        Assert.Equal(Math.PI, LuaOps.ToNumber(lua.GetGlobal("f")), 9);
        Assert.Equal(180.0, LuaOps.ToNumber(lua.GetGlobal("g")), 9);
        Assert.Equal(Math.PI / 4, LuaOps.ToNumber(lua.GetGlobal("h")), 9);
    }

    [Fact]
    public void table_insertとgetnが使える()
    {
        var lua = Run("""
            t = {}
            table.insert(t, "a")
            table.insert(t, "b")
            n = table.getn(t)
            """);

        Assert.Equal(2.0, LuaOps.ToNumber(lua.GetGlobal("n")));
        Assert.Equal(2, Assert.IsType<LuaTable>(lua.GetGlobal("t")).Length);
    }

    [Fact]
    public void printの出力が記録される()
    {
        var lua = Run("""
            print("hello", 42)
            print("world")
            """);

        Assert.Equal(2, lua.Output.Count);
        Assert.Equal("hello\t42", lua.Output[0]);
        Assert.Equal("world", lua.Output[1]);
    }

    [Fact]
    public void tostring_tonumber_typeが使える()
    {
        var lua = Run("""
            a = tostring(12)
            b = tonumber("3.5")
            c = type("x")
            d = type(1)
            e = type(nil)
            f = type({})
            """);

        Assert.Equal("12", lua.GetGlobal("a"));
        Assert.Equal(3.5, LuaOps.ToNumber(lua.GetGlobal("b")));
        Assert.Equal("string", lua.GetGlobal("c"));
        Assert.Equal("number", lua.GetGlobal("d"));
        Assert.Equal("nil", lua.GetGlobal("e"));
        Assert.Equal("table", lua.GetGlobal("f"));
    }

    [Fact]
    public void ホスト関数を登録して呼び出せる()
    {
        var calls = new List<double>();
        var interpreter = new LuaInterpreter();
        interpreter.RegisterFunction("record", args =>
        {
            calls.Add(LuaOps.ToNumberOrDefault(args.Length > 0 ? args[0] : null));
            return null;
        });

        interpreter.Execute("for i = 1, 3 do record(i * 10) end");

        Assert.Equal([10.0, 20.0, 30.0], calls);
    }

    [Fact]
    public void SetGlobalでホストから値を渡せる()
    {
        var interpreter = new LuaInterpreter();
        interpreter.SetGlobal("fps", 60.0);
        interpreter.Execute("frames = fps * 2");

        Assert.Equal(120.0, LuaOps.ToNumber(interpreter.GetGlobal("frames")));
    }

    [Fact]
    public void 無限ループはMaxStepsで打ち切られる()
    {
        var interpreter = new LuaInterpreter { MaxSteps = 10_000 };

        var exception = Assert.Throws<LuaRuntimeException>(() =>
            interpreter.Execute("while true do local x = 1 end"));

        Assert.Contains("上限", exception.Message);
    }

    [Theory]
    [InlineData("x = ")]
    [InlineData("if x then")]
    [InlineData("function f( end")]
    [InlineData("x = (1 + 2")]
    [InlineData("for i = 1 do end")]
    public void 構文エラーは例外になる(string source)
    {
        Assert.Throws<LuaSyntaxException>(() => Run(source));
    }

    [Fact]
    public void コメントは無視される()
    {
        var lua = Run("""
            -- 行コメント
            x = 1 -- 行末コメント
            --[[ ブロック
                 コメント ]]
            x = x + 1
            """);

        Assert.Equal(2.0, LuaOps.ToNumber(lua.GetGlobal("x")));
    }

    [Fact]
    public void LuaOpsの真偽判定はnilとfalseのみ偽()
    {
        Assert.False(LuaOps.IsTruthy(null));
        Assert.False(LuaOps.IsTruthy(false));
        Assert.True(LuaOps.IsTruthy(true));
        Assert.True(LuaOps.IsTruthy(0.0));   // Lua では 0 も真
        Assert.True(LuaOps.IsTruthy(""));    // 空文字列も真
        Assert.True(LuaOps.IsTruthy(new LuaTable()));
    }

    [Fact]
    public void 弾幕スクリプトらしい記述が動く()
    {
        var shots = new List<(double Angle, double Speed)>();
        var interpreter = new LuaInterpreter();
        interpreter.SetGlobal("fps", 60.0);
        interpreter.RegisterFunction("fire", args =>
        {
            if (args.Length > 0 && args[0] is LuaTable table)
                shots.Add((table.GetNumber("angle"), table.GetNumber("speed", 200)));
            return null;
        });

        interpreter.Execute("""
            local way = 16
            for i = 0, way - 1 do
              fire{ angle = i * (360 / way), speed = 220 }
            end
            """);

        Assert.Equal(16, shots.Count);
        Assert.Equal(0, shots[0].Angle, 6);
        Assert.Equal(22.5, shots[1].Angle, 6);
        Assert.All(shots, s => Assert.Equal(220, s.Speed, 6));
    }
}
