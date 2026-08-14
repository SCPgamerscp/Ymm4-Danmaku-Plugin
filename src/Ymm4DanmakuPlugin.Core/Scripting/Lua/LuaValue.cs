using System.Globalization;

namespace Ymm4DanmakuPlugin.Core.Scripting.Lua;

/// <summary>Lua スクリプトの実行時エラー。</summary>
public sealed class LuaRuntimeException(string message, int line = 0)
    : Exception(line > 0 ? $"{line} 行目: {message}" : message)
{
    public int Line { get; } = line;
}

/// <summary>Lua スクリプトの構文エラー。</summary>
public sealed class LuaSyntaxException(string message, int line = 0)
    : Exception(line > 0 ? $"{line} 行目: {message}" : message)
{
    public int Line { get; } = line;
}

/// <summary>
/// Lua のテーブル。配列部 (1 始まり) とハッシュ部を持つ簡易実装。
/// </summary>
public sealed class LuaTable
{
    private readonly List<object?> array = [];
    private readonly Dictionary<object, object?> hash = [];

    /// <summary>配列部の長さ (Lua の # 演算子に対応)。</summary>
    public int Length => array.Count;

    /// <summary>配列部の要素。</summary>
    public IReadOnlyList<object?> ArrayPart => array;

    /// <summary>ハッシュ部の要素。</summary>
    public IReadOnlyDictionary<object, object?> HashPart => hash;

    public object? Get(object? key)
    {
        switch (key)
        {
            case null:
                return null;
            case double d when IsArrayIndex(d, out var index):
                return index <= array.Count ? array[index - 1] : null;
            default:
                return hash.TryGetValue(NormalizeKey(key), out var value) ? value : null;
        }
    }

    public object? Get(string key) => hash.TryGetValue(key, out var value) ? value : null;

    public void Set(object? key, object? value)
    {
        if (key is null) throw new LuaRuntimeException("テーブルのキーに nil は使えません。");

        if (key is double d && IsArrayIndex(d, out var index))
        {
            if (index <= array.Count)
            {
                array[index - 1] = value;
                if (value is null && index == array.Count) array.RemoveAt(array.Count - 1);
                return;
            }

            if (index == array.Count + 1)
            {
                if (value is not null) array.Add(value);
                return;
            }
        }

        var normalized = NormalizeKey(key);
        if (value is null) hash.Remove(normalized);
        else hash[normalized] = value;
    }

    public void Set(string key, object? value) => Set((object)key, value);

    /// <summary>配列部の末尾へ追加する。</summary>
    public void Add(object? value) => array.Add(value);

    /// <summary>数値として取得する。存在しない場合は既定値。</summary>
    public double GetNumber(string key, double defaultValue = 0) =>
        Get(key) is double d ? d : defaultValue;

    /// <summary>真偽値として取得する。</summary>
    public bool GetBoolean(string key, bool defaultValue = false) =>
        Get(key) switch
        {
            bool b => b,
            null => defaultValue,
            _ => true,
        };

    /// <summary>文字列として取得する。</summary>
    public string? GetString(string key) => Get(key) as string;

    /// <summary>キーが存在するか。</summary>
    public bool Has(string key) => Get(key) is not null;

    private static bool IsArrayIndex(double value, out int index)
    {
        index = (int)value;
        return index >= 1 && Math.Abs(value - index) < 1e-9;
    }

    private static object NormalizeKey(object key) => key is double d && Math.Abs(d % 1) < 1e-12 ? d : key;
}

/// <summary>Lua の関数 (組み込み / ユーザー定義の共通基底)。</summary>
public abstract class LuaFunction
{
    public abstract object? Call(object?[] arguments);
}

/// <summary>ホスト (C#) 側で実装した組み込み関数。</summary>
public sealed class LuaBuiltinFunction(string name, Func<object?[], object?> implementation) : LuaFunction
{
    public string Name { get; } = name;

    public override object? Call(object?[] arguments) => implementation(arguments);

    public override string ToString() => $"function: {Name}";
}

/// <summary>Lua 値に関するユーティリティ。</summary>
public static class LuaOps
{
    /// <summary>Lua の真偽判定 (nil と false のみ偽)。</summary>
    public static bool IsTruthy(object? value) => value switch
    {
        null => false,
        bool b => b,
        _ => true,
    };

    /// <summary>数値へ変換する。変換できない場合は例外。</summary>
    public static double ToNumber(object? value, int line = 0) => value switch
    {
        double d => d,
        bool b => b ? 1 : 0,
        string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => throw new LuaRuntimeException($"数値として扱えない値です: {TypeName(value)}", line),
    };

    /// <summary>数値へ変換する。変換できない場合は既定値。</summary>
    public static double ToNumberOrDefault(object? value, double defaultValue = 0) => value switch
    {
        double d => d,
        bool b => b ? 1 : 0,
        string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => defaultValue,
    };

    /// <summary>Lua の tostring 相当。</summary>
    public static string ToDisplayString(object? value) => value switch
    {
        null => "nil",
        bool b => b ? "true" : "false",
        double d => Math.Abs(d % 1) < 1e-12
            ? ((long)d).ToString(CultureInfo.InvariantCulture)
            : d.ToString("G14", CultureInfo.InvariantCulture),
        string s => s,
        LuaTable => "table",
        LuaFunction f => f.ToString() ?? "function",
        _ => value.ToString() ?? "?",
    };

    /// <summary>Lua の type() 相当。</summary>
    public static string TypeName(object? value) => value switch
    {
        null => "nil",
        bool => "boolean",
        double => "number",
        string => "string",
        LuaTable => "table",
        LuaFunction => "function",
        _ => "userdata",
    };

    /// <summary>Lua の == 相当。</summary>
    public static bool AreEqual(object? left, object? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        if (left is double a && right is double b) return Math.Abs(a - b) < 1e-12;
        if (left is string sa && right is string sb) return string.Equals(sa, sb, StringComparison.Ordinal);
        if (left is bool ba && right is bool bb) return ba == bb;
        return ReferenceEquals(left, right);
    }
}
