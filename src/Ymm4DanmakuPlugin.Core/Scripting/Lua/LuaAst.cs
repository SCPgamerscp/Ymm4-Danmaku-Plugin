namespace Ymm4DanmakuPlugin.Core.Scripting.Lua;

// ---------------------------------------------------------------------------
// 式
// ---------------------------------------------------------------------------

internal abstract class LuaExpr
{
    public int Line { get; init; }
}

internal sealed class LuaLiteral : LuaExpr
{
    public required object? Value { get; init; }
}

internal sealed class LuaVarargExpr : LuaExpr;

internal sealed class LuaNameExpr : LuaExpr
{
    public required string Name { get; init; }
}

internal sealed class LuaIndexExpr : LuaExpr
{
    public required LuaExpr Target { get; init; }
    public required LuaExpr Key { get; init; }
}

internal sealed class LuaBinaryExpr : LuaExpr
{
    public required string Operator { get; init; }
    public required LuaExpr Left { get; init; }
    public required LuaExpr Right { get; init; }
}

internal sealed class LuaUnaryExpr : LuaExpr
{
    public required string Operator { get; init; }
    public required LuaExpr Operand { get; init; }
}

internal sealed class LuaCallExpr : LuaExpr
{
    public required LuaExpr Target { get; init; }
    public required List<LuaExpr> Arguments { get; init; }
}

internal sealed class LuaFunctionExpr : LuaExpr
{
    public required List<string> Parameters { get; init; }
    public required LuaBlock Body { get; init; }
    public string? Name { get; init; }
}

internal sealed class LuaTableExpr : LuaExpr
{
    /// <summary>配列部の要素。</summary>
    public required List<LuaExpr> ArrayItems { get; init; }

    /// <summary>ハッシュ部の要素 (キー式, 値式)。</summary>
    public required List<(LuaExpr Key, LuaExpr Value)> HashItems { get; init; }
}

// ---------------------------------------------------------------------------
// 文
// ---------------------------------------------------------------------------

internal abstract class LuaStat
{
    public int Line { get; init; }
}

internal sealed class LuaBlock
{
    public required List<LuaStat> Statements { get; init; }
}

internal sealed class LuaLocalStat : LuaStat
{
    public required List<string> Names { get; init; }
    public required List<LuaExpr> Values { get; init; }
}

internal sealed class LuaAssignStat : LuaStat
{
    public required List<LuaExpr> Targets { get; init; }
    public required List<LuaExpr> Values { get; init; }
}

internal sealed class LuaCallStat : LuaStat
{
    public required LuaCallExpr Call { get; init; }
}

internal sealed class LuaIfStat : LuaStat
{
    public required List<(LuaExpr Condition, LuaBlock Body)> Branches { get; init; }
    public LuaBlock? ElseBody { get; init; }
}

internal sealed class LuaWhileStat : LuaStat
{
    public required LuaExpr Condition { get; init; }
    public required LuaBlock Body { get; init; }
}

internal sealed class LuaRepeatStat : LuaStat
{
    public required LuaBlock Body { get; init; }
    public required LuaExpr Condition { get; init; }
}

internal sealed class LuaNumericForStat : LuaStat
{
    public required string Variable { get; init; }
    public required LuaExpr Start { get; init; }
    public required LuaExpr Limit { get; init; }
    public LuaExpr? Step { get; init; }
    public required LuaBlock Body { get; init; }
}

internal sealed class LuaGenericForStat : LuaStat
{
    public required List<string> Variables { get; init; }
    public required LuaExpr Iterable { get; init; }
    public required LuaBlock Body { get; init; }
}

internal sealed class LuaDoStat : LuaStat
{
    public required LuaBlock Body { get; init; }
}

internal sealed class LuaReturnStat : LuaStat
{
    public required List<LuaExpr> Values { get; init; }
}

internal sealed class LuaBreakStat : LuaStat;
