using Ymm4DanmakuPlugin.Core.Engine;
using Ymm4DanmakuPlugin.Core.Mathematics;
using Ymm4DanmakuPlugin.Core.Model;
using Ymm4DanmakuPlugin.Core.Scripting.Lua;

namespace Ymm4DanmakuPlugin.Core.Importers;

/// <summary>
/// Lua サブセットスクリプトから弾幕を生成するインポーター。
/// <para>
/// スクリプトはインポート時に 1 度だけ実行され、結果は
/// 「時刻付き発射命令の列」(<see cref="ScriptedShotProgram"/>) として展開される。
/// これによりタイムライン上を自由にシークしても常に同じ弾幕が再現される。
/// </para>
/// <para>提供される関数・変数:</para>
/// <list type="bullet">
///   <item><description><c>fire{ angle=, speed=, way=, spread=, aim=, sprite=, color=, scale=, lifetime=, accel=, turn=, offsetx=, offsety=, sound=, homing= }</c></description></item>
///   <item><description><c>fire(angle, speed)</c> — 簡易記法</description></item>
///   <item><description><c>wait(frames)</c> / <c>waitsec(seconds)</c> — 時刻を進める</description></item>
///   <item><description><c>time()</c> / <c>settime(seconds)</c> — 現在時刻の取得・設定</description></item>
///   <item><description><c>loop(seconds)</c> — スクリプト全体のループ周期を指定</description></item>
///   <item><description><c>rand()</c> / <c>randrange(min, max)</c> — 決定論的乱数</description></item>
///   <item><description><c>fps</c> — 1 秒あたりのフレーム数 (既定 60)</description></item>
///   <item><description><c>math.*</c>, <c>print</c>, <c>tostring</c>, <c>type</c> など安全な標準関数</description></item>
/// </list>
/// </summary>
public sealed class LuaDanmakuImporter : IDanmakuImporter
{
    /// <summary>1 スクリプトで生成できる発射命令の上限。</summary>
    public const int MaxShots = 200_000;

    public string Name => "Lua";

    public IReadOnlyList<string> SupportedExtensions { get; } = [".lua"];

    /// <summary>スクリプト内 <c>rand()</c> のシード。</summary>
    public int Seed { get; init; } = 12345;

    public bool CanImport(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var trimmed = text.TrimStart();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('<')) return false;

        return text.Contains("fire", StringComparison.Ordinal) ||
               text.Contains("function", StringComparison.Ordinal) ||
               text.Contains("local ", StringComparison.Ordinal) ||
               text.Contains("--", StringComparison.Ordinal);
    }

    public DanmakuImportResult Import(string text)
    {
        var shots = new List<ScriptedShot>();
        var warnings = new List<string>();
        var random = new DeterministicRandom(Seed);

        var currentTime = 0.0;
        var loopDuration = 0.0;
        const double fps = 60.0;

        var interpreter = new LuaInterpreter();
        interpreter.SetGlobal("fps", fps);

        interpreter.RegisterFunction("wait", args =>
        {
            var frames = LuaOps.ToNumberOrDefault(args.Length > 0 ? args[0] : null, 1);
            currentTime += Math.Max(0, frames) / fps;
            return null;
        });

        interpreter.RegisterFunction("waitsec", args =>
        {
            var seconds = LuaOps.ToNumberOrDefault(args.Length > 0 ? args[0] : null);
            currentTime += Math.Max(0, seconds);
            return null;
        });

        interpreter.RegisterFunction("time", _ => currentTime);

        interpreter.RegisterFunction("settime", args =>
        {
            currentTime = Math.Max(0, LuaOps.ToNumberOrDefault(args.Length > 0 ? args[0] : null));
            return null;
        });

        interpreter.RegisterFunction("loop", args =>
        {
            loopDuration = Math.Max(0, LuaOps.ToNumberOrDefault(args.Length > 0 ? args[0] : null));
            return null;
        });

        interpreter.RegisterFunction("rand", _ => random.NextDouble());

        interpreter.RegisterFunction("randrange", args =>
        {
            var min = LuaOps.ToNumberOrDefault(args.Length > 0 ? args[0] : null);
            var max = LuaOps.ToNumberOrDefault(args.Length > 1 ? args[1] : null, 1);
            return random.NextDouble(Math.Min(min, max), Math.Max(min, max));
        });

        interpreter.RegisterFunction("fire", args =>
        {
            if (shots.Count >= MaxShots)
                throw new LuaRuntimeException($"発射命令が上限 ({MaxShots}) を超えました。");

            shots.Add(CreateShot(args, currentTime));
            return null;
        });

        try
        {
            interpreter.Execute(text);
        }
        catch (LuaSyntaxException e)
        {
            return DanmakuImportResult.Failure($"Lua の構文エラー: {e.Message}");
        }
        catch (LuaRuntimeException e)
        {
            return DanmakuImportResult.Failure($"Lua の実行エラー: {e.Message}");
        }

        if (shots.Count == 0)
            warnings.Add("fire() が 1 度も呼ばれていないため、弾は発射されません。");

        foreach (var line in interpreter.Output)
            warnings.Add($"print: {line}");

        return new DanmakuImportResult
        {
            Shots = new ScriptedShotProgram(shots, loopDuration),
            Warnings = warnings,
        };
    }

    private static ScriptedShot CreateShot(object?[] args, double time)
    {
        // fire{ ... } 形式
        if (args.Length > 0 && args[0] is LuaTable table)
        {
            var colorText = table.GetString("color");
            return new ScriptedShot
            {
                Time = time,
                Angle = table.GetNumber("angle"),
                AimAtTarget = table.GetBoolean("aim"),
                Way = Math.Max(1, (int)table.GetNumber("way", 1)),
                Spread = table.Has("spread") ? table.GetNumber("spread") : 360,
                Speed = table.GetNumber("speed", 200),
                Acceleration = table.GetNumber("accel"),
                AngularVelocity = table.GetNumber("turn"),
                Lifetime = table.GetNumber("lifetime"),
                SpriteIndex = table.Has("sprite") ? (int)table.GetNumber("sprite") : -1,
                Color = string.IsNullOrWhiteSpace(colorText) ? null : BulletColor.FromHex(colorText),
                ScaleFactor = table.GetNumber("scale", 1.0),
                OffsetX = table.GetNumber("offsetx"),
                OffsetY = table.GetNumber("offsety"),
                PlaySound = table.GetBoolean("sound", true),
                Homing = table.Has("homing") ? table.GetBoolean("homing") : null,
            };
        }

        // fire(angle, speed) 形式
        return new ScriptedShot
        {
            Time = time,
            Angle = LuaOps.ToNumberOrDefault(args.Length > 0 ? args[0] : null),
            Speed = LuaOps.ToNumberOrDefault(args.Length > 1 ? args[1] : null, 200),
            Way = args.Length > 2 ? Math.Max(1, (int)LuaOps.ToNumberOrDefault(args[2], 1)) : 1,
        };
    }
}
