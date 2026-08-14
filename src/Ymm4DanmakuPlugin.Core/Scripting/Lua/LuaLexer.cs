using System.Globalization;
using System.Text;

namespace Ymm4DanmakuPlugin.Core.Scripting.Lua;

internal enum LuaTokenType
{
    Eof,
    Name,
    Number,
    String,
    Keyword,
    Symbol,
}

internal readonly record struct LuaToken(LuaTokenType Type, string Text, double Number, int Line)
{
    public bool Is(LuaTokenType type, string text) =>
        Type == type && string.Equals(Text, text, StringComparison.Ordinal);

    public override string ToString() => Type == LuaTokenType.Eof ? "<eof>" : Text;
}

/// <summary>Lua サブセットの字句解析器。</summary>
internal sealed class LuaLexer(string source)
{
    private static readonly HashSet<string> Keywords =
    [
        "and", "break", "do", "else", "elseif", "end", "false", "for", "function", "if",
        "in", "local", "nil", "not", "or", "repeat", "return", "then", "true", "until", "while",
    ];

    private static readonly string[] ThreeCharSymbols = ["..."];

    private static readonly string[] TwoCharSymbols = ["==", "~=", "<=", ">=", ".."];

    private const string SingleCharSymbols = "+-*/%^#=<>(){}[];:,.";

    private readonly string source = source;
    private int position;
    private int line = 1;

    public List<LuaToken> Tokenize()
    {
        var tokens = new List<LuaToken>();
        while (true)
        {
            var token = Next();
            tokens.Add(token);
            if (token.Type == LuaTokenType.Eof) break;
        }

        return tokens;
    }

    private LuaToken Next()
    {
        SkipTrivia();
        if (position >= source.Length) return new LuaToken(LuaTokenType.Eof, string.Empty, 0, line);

        var c = source[position];

        if (char.IsLetter(c) || c == '_') return ReadName();
        if (char.IsDigit(c) || (c == '.' && position + 1 < source.Length && char.IsDigit(source[position + 1])))
            return ReadNumber();
        if (c is '"' or '\'') return ReadString(c);
        if (c == '[' && position + 1 < source.Length && source[position + 1] == '[') return ReadLongString();

        foreach (var symbol in ThreeCharSymbols)
        {
            if (Match(symbol)) return new LuaToken(LuaTokenType.Symbol, symbol, 0, line);
        }

        foreach (var symbol in TwoCharSymbols)
        {
            if (Match(symbol)) return new LuaToken(LuaTokenType.Symbol, symbol, 0, line);
        }

        if (SingleCharSymbols.Contains(c))
        {
            position++;
            return new LuaToken(LuaTokenType.Symbol, c.ToString(), 0, line);
        }

        throw new LuaSyntaxException($"予期しない文字 '{c}' です。", line);
    }

    private bool Match(string text)
    {
        if (position + text.Length > source.Length) return false;
        if (string.CompareOrdinal(source, position, text, 0, text.Length) != 0) return false;
        position += text.Length;
        return true;
    }

    private void SkipTrivia()
    {
        while (position < source.Length)
        {
            var c = source[position];

            if (c == '\n')
            {
                line++;
                position++;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                position++;
                continue;
            }

            // コメント
            if (c == '-' && position + 1 < source.Length && source[position + 1] == '-')
            {
                position += 2;

                // 長コメント --[[ ... ]]
                if (position + 1 < source.Length && source[position] == '[' && source[position + 1] == '[')
                {
                    position += 2;
                    while (position < source.Length &&
                           !(source[position] == ']' && position + 1 < source.Length && source[position + 1] == ']'))
                    {
                        if (source[position] == '\n') line++;
                        position++;
                    }

                    position = Math.Min(source.Length, position + 2);
                    continue;
                }

                while (position < source.Length && source[position] != '\n') position++;
                continue;
            }

            return;
        }
    }

    private LuaToken ReadName()
    {
        var start = position;
        while (position < source.Length && (char.IsLetterOrDigit(source[position]) || source[position] == '_'))
            position++;

        var text = source[start..position];
        return new LuaToken(Keywords.Contains(text) ? LuaTokenType.Keyword : LuaTokenType.Name, text, 0, line);
    }

    private LuaToken ReadNumber()
    {
        var start = position;

        if (source[position] == '0' && position + 1 < source.Length && (source[position + 1] is 'x' or 'X'))
        {
            position += 2;
            while (position < source.Length && Uri.IsHexDigit(source[position])) position++;
            var hex = source[(start + 2)..position];
            return new LuaToken(LuaTokenType.Number, source[start..position],
                (double)Convert.ToInt64(hex, 16), line);
        }

        while (position < source.Length && (char.IsDigit(source[position]) || source[position] == '.')) position++;

        if (position < source.Length && (source[position] is 'e' or 'E'))
        {
            position++;
            if (position < source.Length && (source[position] is '+' or '-')) position++;
            while (position < source.Length && char.IsDigit(source[position])) position++;
        }

        var text = source[start..position];
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            throw new LuaSyntaxException($"数値を解釈できません: '{text}'", line);

        return new LuaToken(LuaTokenType.Number, text, value, line);
    }

    private LuaToken ReadString(char quote)
    {
        var startLine = line;
        position++; // 開始クォート
        var builder = new StringBuilder();

        while (true)
        {
            if (position >= source.Length)
                throw new LuaSyntaxException("文字列が閉じられていません。", startLine);

            var c = source[position++];
            if (c == quote) break;

            if (c == '\n')
                throw new LuaSyntaxException("文字列の途中で改行されています。", startLine);

            if (c != '\\')
            {
                builder.Append(c);
                continue;
            }

            if (position >= source.Length)
                throw new LuaSyntaxException("エスケープシーケンスが不正です。", startLine);

            var escape = source[position++];
            builder.Append(escape switch
            {
                'n' => '\n',
                't' => '\t',
                'r' => '\r',
                'a' => '\a',
                'b' => '\b',
                'f' => '\f',
                'v' => '\v',
                '\\' => '\\',
                '"' => '"',
                '\'' => '\'',
                _ => escape,
            });
        }

        return new LuaToken(LuaTokenType.String, builder.ToString(), 0, startLine);
    }

    private LuaToken ReadLongString()
    {
        var startLine = line;
        position += 2;
        var start = position;

        while (position < source.Length &&
               !(source[position] == ']' && position + 1 < source.Length && source[position + 1] == ']'))
        {
            if (source[position] == '\n') line++;
            position++;
        }

        if (position >= source.Length)
            throw new LuaSyntaxException("長文字列が閉じられていません。", startLine);

        var text = source[start..position];
        position += 2;
        return new LuaToken(LuaTokenType.String, text, 0, startLine);
    }
}
