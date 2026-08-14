using System.Text;

namespace Ymm4DanmakuPlugin.Core.Scripting.Lua;

/// <summary>変数スコープ。</summary>
internal sealed class LuaScope(LuaScope? parent)
{
    private readonly Dictionary<string, object?> variables = [];

    public LuaScope? Parent { get; } = parent;

    public bool TryGet(string name, out object? value)
    {
        for (var scope = this; scope is not null; scope = scope.Parent)
        {
            if (scope.variables.TryGetValue(name, out value)) return true;
        }

        value = null;
        return false;
    }

    /// <summary>既存の変数があればそこへ、なければ最上位 (グローバル) へ代入する。</summary>
    public void Assign(string name, object? value)
    {
        for (var scope = this; scope is not null; scope = scope.Parent)
        {
            if (!scope.variables.ContainsKey(name)) continue;
            scope.variables[name] = value;
            return;
        }

        var root = this;
        while (root.Parent is not null) root = root.Parent;
        root.variables[name] = value;
    }

    /// <summary>このスコープに新しいローカル変数を定義する。</summary>
    public void Declare(string name, object? value) => variables[name] = value;
}

/// <summary>ユーザー定義関数。</summary>
internal sealed class LuaClosure(LuaFunctionExpr definition, LuaScope closure, LuaInterpreter interpreter) : LuaFunction
{
    public override object? Call(object?[] arguments) => interpreter.CallClosure(this, arguments);

    internal LuaFunctionExpr Definition { get; } = definition;

    internal LuaScope Closure { get; } = closure;

    public override string ToString() => $"function: {Definition.Name ?? "anonymous"}";
}

/// <summary>
/// Lua サブセットのインタプリタ。
/// <para>
/// 弾幕定義スクリプトを安全に実行することが目的のため、
/// ファイル入出力・OS 操作・require などの危険な標準ライブラリは一切提供しない。
/// また実行ステップ数に上限を設け、無限ループがプレビューを固まらせないようにしている。
/// </para>
/// </summary>
public sealed class LuaInterpreter
{
    private enum Flow
    {
        Normal,
        Break,
        Return,
    }

    private readonly LuaScope globals = new(null);
    private object? returnValue;
    private long steps;

    /// <summary>実行できるステップ数の上限。</summary>
    public long MaxSteps { get; init; } = 5_000_000;

    /// <summary>print() の出力。</summary>
    public List<string> Output { get; } = [];

    public LuaInterpreter()
    {
        RegisterStandardLibrary();
    }

    /// <summary>グローバル変数を設定する。</summary>
    public void SetGlobal(string name, object? value) => globals.Declare(name, value);

    /// <summary>グローバル変数を取得する。</summary>
    public object? GetGlobal(string name) => globals.TryGet(name, out var value) ? value : null;

    /// <summary>組み込み関数を登録する。</summary>
    public void RegisterFunction(string name, Func<object?[], object?> implementation) =>
        globals.Declare(name, new LuaBuiltinFunction(name, implementation));

    /// <summary>スクリプトを実行する。</summary>
    public void Execute(string source)
    {
        var block = LuaParser.ParseSource(source);
        steps = 0;
        returnValue = null;
        ExecuteBlock(block, new LuaScope(globals));
    }

    internal object? CallClosure(LuaClosure closure, object?[] arguments)
    {
        var scope = new LuaScope(closure.Closure);
        var parameters = closure.Definition.Parameters;

        for (var i = 0; i < parameters.Count; i++)
            scope.Declare(parameters[i], i < arguments.Length ? arguments[i] : null);

        var varargs = new LuaTable();
        for (var i = parameters.Count; i < arguments.Length; i++) varargs.Add(arguments[i]);
        scope.Declare("...", varargs);

        var savedReturn = returnValue;
        returnValue = null;

        var flow = ExecuteBlock(closure.Definition.Body, scope);
        var result = flow == Flow.Return ? returnValue : null;

        returnValue = savedReturn;
        return result;
    }

    // -----------------------------------------------------------------------
    // 文の実行
    // -----------------------------------------------------------------------

    private Flow ExecuteBlock(LuaBlock block, LuaScope scope)
    {
        foreach (var statement in block.Statements)
        {
            var flow = ExecuteStatement(statement, scope);
            if (flow != Flow.Normal) return flow;
        }

        return Flow.Normal;
    }

    private Flow ExecuteStatement(LuaStat statement, LuaScope scope)
    {
        if (++steps > MaxSteps)
            throw new LuaRuntimeException($"実行ステップ数が上限 ({MaxSteps}) を超えました。無限ループの可能性があります。", statement.Line);

        switch (statement)
        {
            case LuaLocalStat local:
            {
                for (var i = 0; i < local.Names.Count; i++)
                {
                    var value = i < local.Values.Count ? Evaluate(local.Values[i], scope) : null;
                    scope.Declare(local.Names[i], value);
                }

                return Flow.Normal;
            }

            case LuaAssignStat assign:
            {
                for (var i = 0; i < assign.Targets.Count; i++)
                {
                    var value = i < assign.Values.Count ? Evaluate(assign.Values[i], scope) : null;
                    AssignTo(assign.Targets[i], value, scope);
                }

                return Flow.Normal;
            }

            case LuaCallStat call:
                Evaluate(call.Call, scope);
                return Flow.Normal;

            case LuaIfStat ifStatement:
            {
                foreach (var (condition, body) in ifStatement.Branches)
                {
                    if (!LuaOps.IsTruthy(Evaluate(condition, scope))) continue;
                    return ExecuteBlock(body, new LuaScope(scope));
                }

                return ifStatement.ElseBody is null
                    ? Flow.Normal
                    : ExecuteBlock(ifStatement.ElseBody, new LuaScope(scope));
            }

            case LuaWhileStat whileStatement:
            {
                while (LuaOps.IsTruthy(Evaluate(whileStatement.Condition, scope)))
                {
                    if (++steps > MaxSteps)
                        throw new LuaRuntimeException("while ループが上限ステップ数を超えました。", whileStatement.Line);

                    var flow = ExecuteBlock(whileStatement.Body, new LuaScope(scope));
                    if (flow == Flow.Break) break;
                    if (flow == Flow.Return) return flow;
                }

                return Flow.Normal;
            }

            case LuaRepeatStat repeatStatement:
            {
                while (true)
                {
                    if (++steps > MaxSteps)
                        throw new LuaRuntimeException("repeat ループが上限ステップ数を超えました。", repeatStatement.Line);

                    var body = new LuaScope(scope);
                    var flow = ExecuteBlock(repeatStatement.Body, body);
                    if (flow == Flow.Break) break;
                    if (flow == Flow.Return) return flow;
                    if (LuaOps.IsTruthy(Evaluate(repeatStatement.Condition, body))) break;
                }

                return Flow.Normal;
            }

            case LuaNumericForStat forStatement:
                return ExecuteNumericFor(forStatement, scope);

            case LuaGenericForStat genericFor:
                return ExecuteGenericFor(genericFor, scope);

            case LuaDoStat doStatement:
                return ExecuteBlock(doStatement.Body, new LuaScope(scope));

            case LuaReturnStat returnStatement:
                returnValue = returnStatement.Values.Count > 0 ? Evaluate(returnStatement.Values[0], scope) : null;
                return Flow.Return;

            case LuaBreakStat:
                return Flow.Break;

            default:
                throw new LuaRuntimeException($"未対応の文です: {statement.GetType().Name}", statement.Line);
        }
    }

    private Flow ExecuteNumericFor(LuaNumericForStat statement, LuaScope scope)
    {
        var start = LuaOps.ToNumber(Evaluate(statement.Start, scope), statement.Line);
        var limit = LuaOps.ToNumber(Evaluate(statement.Limit, scope), statement.Line);
        var step = statement.Step is null ? 1.0 : LuaOps.ToNumber(Evaluate(statement.Step, scope), statement.Line);

        if (Math.Abs(step) < 1e-12)
            throw new LuaRuntimeException("for のステップに 0 は指定できません。", statement.Line);

        for (var i = start; step > 0 ? i <= limit + 1e-9 : i >= limit - 1e-9; i += step)
        {
            if (++steps > MaxSteps)
                throw new LuaRuntimeException("for ループが上限ステップ数を超えました。", statement.Line);

            var body = new LuaScope(scope);
            body.Declare(statement.Variable, i);

            var flow = ExecuteBlock(statement.Body, body);
            if (flow == Flow.Break) break;
            if (flow == Flow.Return) return flow;
        }

        return Flow.Normal;
    }

    private Flow ExecuteGenericFor(LuaGenericForStat statement, LuaScope scope)
    {
        // pairs(t) / ipairs(t) は「テーブルそのもの」を返す簡易実装
        var iterable = Evaluate(statement.Iterable, scope);
        if (iterable is not LuaTable table)
            throw new LuaRuntimeException("for ... in にはテーブル (pairs/ipairs) が必要です。", statement.Line);

        var keyName = statement.Variables[0];
        var valueName = statement.Variables.Count > 1 ? statement.Variables[1] : null;

        for (var i = 0; i < table.Length; i++)
        {
            if (++steps > MaxSteps)
                throw new LuaRuntimeException("for ループが上限ステップ数を超えました。", statement.Line);

            var body = new LuaScope(scope);
            body.Declare(keyName, (double)(i + 1));
            if (valueName is not null) body.Declare(valueName, table.ArrayPart[i]);

            var flow = ExecuteBlock(statement.Body, body);
            if (flow == Flow.Break) return Flow.Normal;
            if (flow == Flow.Return) return flow;
        }

        foreach (var pair in table.HashPart)
        {
            if (++steps > MaxSteps)
                throw new LuaRuntimeException("for ループが上限ステップ数を超えました。", statement.Line);

            var body = new LuaScope(scope);
            body.Declare(keyName, pair.Key);
            if (valueName is not null) body.Declare(valueName, pair.Value);

            var flow = ExecuteBlock(statement.Body, body);
            if (flow == Flow.Break) return Flow.Normal;
            if (flow == Flow.Return) return flow;
        }

        return Flow.Normal;
    }

    private void AssignTo(LuaExpr target, object? value, LuaScope scope)
    {
        switch (target)
        {
            case LuaNameExpr name:
                scope.Assign(name.Name, value);
                break;

            case LuaIndexExpr index:
            {
                var table = Evaluate(index.Target, scope);
                if (table is not LuaTable luaTable)
                    throw new LuaRuntimeException($"テーブルではない値へ代入しようとしました: {LuaOps.TypeName(table)}", index.Line);
                luaTable.Set(Evaluate(index.Key, scope), value);
                break;
            }

            default:
                throw new LuaRuntimeException("代入先として不正な式です。", target.Line);
        }
    }

    // -----------------------------------------------------------------------
    // 式の評価
    // -----------------------------------------------------------------------

    private object? Evaluate(LuaExpr expression, LuaScope scope)
    {
        if (++steps > MaxSteps)
            throw new LuaRuntimeException("実行ステップ数が上限を超えました。", expression.Line);

        switch (expression)
        {
            case LuaLiteral literal:
                return literal.Value;

            case LuaNameExpr name:
                return scope.TryGet(name.Name, out var value) ? value : null;

            case LuaVarargExpr:
                return scope.TryGet("...", out var varargs) ? varargs : new LuaTable();

            case LuaIndexExpr index:
            {
                var target = Evaluate(index.Target, scope);
                var key = Evaluate(index.Key, scope);
                return target switch
                {
                    LuaTable table => table.Get(key),
                    string s when key is string => null,
                    null => throw new LuaRuntimeException("nil のフィールドを参照しようとしました。", index.Line),
                    _ => null,
                };
            }

            case LuaUnaryExpr unary:
                return EvaluateUnary(unary, scope);

            case LuaBinaryExpr binary:
                return EvaluateBinary(binary, scope);

            case LuaFunctionExpr function:
                return new LuaClosure(function, scope, this);

            case LuaTableExpr tableExpr:
            {
                var table = new LuaTable();
                foreach (var item in tableExpr.ArrayItems) table.Add(Evaluate(item, scope));
                foreach (var (key, item) in tableExpr.HashItems)
                    table.Set(Evaluate(key, scope), Evaluate(item, scope));
                return table;
            }

            case LuaCallExpr call:
            {
                var target = Evaluate(call.Target, scope);
                if (target is not LuaFunction function)
                {
                    var name = call.Target is LuaNameExpr n ? n.Name : LuaOps.TypeName(target);
                    throw new LuaRuntimeException($"'{name}' は関数ではありません。", call.Line);
                }

                var arguments = new object?[call.Arguments.Count];
                for (var i = 0; i < arguments.Length; i++) arguments[i] = Evaluate(call.Arguments[i], scope);

                return function.Call(arguments);
            }

            default:
                throw new LuaRuntimeException($"未対応の式です: {expression.GetType().Name}", expression.Line);
        }
    }

    private object? EvaluateUnary(LuaUnaryExpr unary, LuaScope scope)
    {
        var operand = Evaluate(unary.Operand, scope);
        return unary.Operator switch
        {
            "-" => -LuaOps.ToNumber(operand, unary.Line),
            "not" => !LuaOps.IsTruthy(operand),
            "#" => operand switch
            {
                LuaTable table => (double)table.Length,
                string s => (double)s.Length,
                _ => throw new LuaRuntimeException("# 演算子はテーブルか文字列にのみ使用できます。", unary.Line),
            },
            _ => throw new LuaRuntimeException($"未対応の単項演算子です: {unary.Operator}", unary.Line),
        };
    }

    private object? EvaluateBinary(LuaBinaryExpr binary, LuaScope scope)
    {
        // and / or は短絡評価
        if (binary.Operator == "and")
        {
            var left = Evaluate(binary.Left, scope);
            return LuaOps.IsTruthy(left) ? Evaluate(binary.Right, scope) : left;
        }

        if (binary.Operator == "or")
        {
            var left = Evaluate(binary.Left, scope);
            return LuaOps.IsTruthy(left) ? left : Evaluate(binary.Right, scope);
        }

        var a = Evaluate(binary.Left, scope);
        var b = Evaluate(binary.Right, scope);

        switch (binary.Operator)
        {
            case "==": return LuaOps.AreEqual(a, b);
            case "~=": return !LuaOps.AreEqual(a, b);
            case "..": return LuaOps.ToDisplayString(a) + LuaOps.ToDisplayString(b);
        }

        var x = LuaOps.ToNumber(a, binary.Line);
        var y = LuaOps.ToNumber(b, binary.Line);

        return binary.Operator switch
        {
            "+" => x + y,
            "-" => x - y,
            "*" => x * y,
            "/" => y == 0 ? 0.0 : x / y,
            "%" => y == 0 ? 0.0 : x - Math.Floor(x / y) * y,
            "^" => Math.Pow(x, y),
            "<" => x < y,
            "<=" => x <= y,
            ">" => x > y,
            ">=" => x >= y,
            _ => throw new LuaRuntimeException($"未対応の二項演算子です: {binary.Operator}", binary.Line),
        };
    }

    // -----------------------------------------------------------------------
    // 標準ライブラリ (安全な範囲のみ)
    // -----------------------------------------------------------------------

    private void RegisterStandardLibrary()
    {
        var math = new LuaTable();
        math.Set("pi", Math.PI);
        math.Set("huge", double.PositiveInfinity);
        AddMathFunction(math, "sin", Math.Sin);
        AddMathFunction(math, "cos", Math.Cos);
        AddMathFunction(math, "tan", Math.Tan);
        AddMathFunction(math, "asin", Math.Asin);
        AddMathFunction(math, "acos", Math.Acos);
        AddMathFunction(math, "sqrt", v => v < 0 ? 0 : Math.Sqrt(v));
        AddMathFunction(math, "abs", Math.Abs);
        AddMathFunction(math, "floor", Math.Floor);
        AddMathFunction(math, "ceil", Math.Ceiling);
        AddMathFunction(math, "exp", Math.Exp);
        AddMathFunction(math, "rad", v => v * Math.PI / 180.0);
        AddMathFunction(math, "deg", v => v * 180.0 / Math.PI);

        math.Set("atan", new LuaBuiltinFunction("math.atan", args =>
            args.Length >= 2
                ? Math.Atan2(LuaOps.ToNumberOrDefault(args[0]), LuaOps.ToNumberOrDefault(args[1]))
                : Math.Atan(LuaOps.ToNumberOrDefault(args.Length > 0 ? args[0] : null))));

        math.Set("atan2", new LuaBuiltinFunction("math.atan2", args =>
            Math.Atan2(
                LuaOps.ToNumberOrDefault(args.Length > 0 ? args[0] : null),
                LuaOps.ToNumberOrDefault(args.Length > 1 ? args[1] : null))));

        math.Set("log", new LuaBuiltinFunction("math.log", args =>
        {
            var v = LuaOps.ToNumberOrDefault(args.Length > 0 ? args[0] : null);
            return v <= 0 ? 0.0 : Math.Log(v);
        }));

        math.Set("pow", new LuaBuiltinFunction("math.pow", args =>
            Math.Pow(
                LuaOps.ToNumberOrDefault(args.Length > 0 ? args[0] : null),
                LuaOps.ToNumberOrDefault(args.Length > 1 ? args[1] : null))));

        math.Set("min", new LuaBuiltinFunction("math.min", args =>
            args.Length == 0 ? 0.0 : args.Select(a => LuaOps.ToNumberOrDefault(a)).Min()));

        math.Set("max", new LuaBuiltinFunction("math.max", args =>
            args.Length == 0 ? 0.0 : args.Select(a => LuaOps.ToNumberOrDefault(a)).Max()));

        math.Set("fmod", new LuaBuiltinFunction("math.fmod", args =>
        {
            var x = LuaOps.ToNumberOrDefault(args.Length > 0 ? args[0] : null);
            var y = LuaOps.ToNumberOrDefault(args.Length > 1 ? args[1] : null);
            return y == 0 ? 0.0 : Math.IEEERemainder(x, y) is var r && Math.Sign(r) != Math.Sign(x) && r != 0
                ? x % y
                : x % y;
        }));

        globals.Declare("math", math);

        var stringLib = new LuaTable();
        stringLib.Set("format", new LuaBuiltinFunction("string.format", args =>
        {
            if (args.Length == 0) return string.Empty;
            var format = LuaOps.ToDisplayString(args[0]);
            var builder = new StringBuilder(format);
            for (var i = 1; i < args.Length; i++)
                builder.Replace("%s", LuaOps.ToDisplayString(args[i]), 0, builder.Length);
            return builder.ToString();
        }));
        stringLib.Set("len", new LuaBuiltinFunction("string.len", args =>
            (double)(args.Length > 0 ? LuaOps.ToDisplayString(args[0]).Length : 0)));
        globals.Declare("string", stringLib);

        var table = new LuaTable();
        table.Set("insert", new LuaBuiltinFunction("table.insert", args =>
        {
            if (args.Length >= 2 && args[0] is LuaTable t) t.Add(args[^1]);
            return null;
        }));
        table.Set("getn", new LuaBuiltinFunction("table.getn", args =>
            (double)(args.Length > 0 && args[0] is LuaTable t ? t.Length : 0)));
        globals.Declare("table", table);

        RegisterFunction("print", args =>
        {
            Output.Add(string.Join('\t', args.Select(LuaOps.ToDisplayString)));
            return null;
        });

        RegisterFunction("tostring", args => LuaOps.ToDisplayString(args.Length > 0 ? args[0] : null));

        RegisterFunction("tonumber", args =>
            args.Length > 0 && args[0] is not null ? LuaOps.ToNumberOrDefault(args[0]) : null);

        RegisterFunction("type", args => LuaOps.TypeName(args.Length > 0 ? args[0] : null));

        // pairs / ipairs はテーブルをそのまま返す (generic for 側で解釈する)
        RegisterFunction("pairs", args => args.Length > 0 ? args[0] : null);
        RegisterFunction("ipairs", args => args.Length > 0 ? args[0] : null);
    }

    private static void AddMathFunction(LuaTable math, string name, Func<double, double> implementation) =>
        math.Set(name, new LuaBuiltinFunction($"math.{name}", args =>
            implementation(LuaOps.ToNumberOrDefault(args.Length > 0 ? args[0] : null))));
}
