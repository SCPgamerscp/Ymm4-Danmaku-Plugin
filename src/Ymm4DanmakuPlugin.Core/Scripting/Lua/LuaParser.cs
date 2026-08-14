namespace Ymm4DanmakuPlugin.Core.Scripting.Lua;

/// <summary>Lua サブセットの構文解析器 (再帰下降 + 演算子優先順位)。</summary>
internal sealed class LuaParser(List<LuaToken> tokens)
{
    private readonly List<LuaToken> tokens = tokens;
    private int index;

    private LuaToken Current => tokens[index];
    private LuaToken Peek(int offset = 1) => tokens[Math.Min(index + offset, tokens.Count - 1)];

    public static LuaBlock ParseSource(string source)
    {
        var lexer = new LuaLexer(source);
        var parser = new LuaParser(lexer.Tokenize());
        var block = parser.ParseBlock();
        if (parser.Current.Type != LuaTokenType.Eof)
            throw new LuaSyntaxException($"予期しないトークン '{parser.Current}' です。", parser.Current.Line);
        return block;
    }

    private LuaBlock ParseBlock()
    {
        var statements = new List<LuaStat>();
        while (!IsBlockEnd())
        {
            if (CheckSymbol(";"))
            {
                index++;
                continue;
            }

            var statement = ParseStatement();
            statements.Add(statement);

            if (statement is LuaReturnStat) break;
        }

        return new LuaBlock { Statements = statements };
    }

    private bool IsBlockEnd() =>
        Current.Type == LuaTokenType.Eof ||
        (Current.Type == LuaTokenType.Keyword && Current.Text is "end" or "else" or "elseif" or "until");

    private LuaStat ParseStatement()
    {
        var line = Current.Line;

        if (Current.Type == LuaTokenType.Keyword)
        {
            switch (Current.Text)
            {
                case "local": return ParseLocal();
                case "if": return ParseIf();
                case "while": return ParseWhile();
                case "for": return ParseFor();
                case "repeat": return ParseRepeat();
                case "function": return ParseFunctionStatement();
                case "do":
                    index++;
                    var body = ParseBlock();
                    ExpectKeyword("end");
                    return new LuaDoStat { Body = body, Line = line };
                case "return":
                {
                    index++;
                    var values = new List<LuaExpr>();
                    if (!IsBlockEnd() && !CheckSymbol(";"))
                    {
                        values.Add(ParseExpression());
                        while (CheckSymbol(",")) { index++; values.Add(ParseExpression()); }
                    }

                    return new LuaReturnStat { Values = values, Line = line };
                }

                case "break":
                    index++;
                    return new LuaBreakStat { Line = line };
            }
        }

        // 式文 (関数呼び出し) または代入
        var first = ParseSuffixedExpression();

        if (CheckSymbol("=") || CheckSymbol(","))
        {
            var targets = new List<LuaExpr> { first };
            while (CheckSymbol(","))
            {
                index++;
                targets.Add(ParseSuffixedExpression());
            }

            ExpectSymbol("=");

            var values = new List<LuaExpr> { ParseExpression() };
            while (CheckSymbol(",")) { index++; values.Add(ParseExpression()); }

            return new LuaAssignStat { Targets = targets, Values = values, Line = line };
        }

        if (first is LuaCallExpr call)
            return new LuaCallStat { Call = call, Line = line };

        throw new LuaSyntaxException("文として解釈できません。", line);
    }

    private LuaStat ParseLocal()
    {
        var line = Current.Line;
        index++; // local

        if (CheckKeyword("function"))
        {
            index++;
            var name = ExpectName();
            var function = ParseFunctionBody(name);
            return new LuaLocalStat
            {
                Names = [name],
                Values = [function],
                Line = line,
            };
        }

        var names = new List<string> { ExpectName() };
        while (CheckSymbol(",")) { index++; names.Add(ExpectName()); }

        var values = new List<LuaExpr>();
        if (CheckSymbol("="))
        {
            index++;
            values.Add(ParseExpression());
            while (CheckSymbol(",")) { index++; values.Add(ParseExpression()); }
        }

        return new LuaLocalStat { Names = names, Values = values, Line = line };
    }

    private LuaStat ParseFunctionStatement()
    {
        var line = Current.Line;
        index++; // function

        LuaExpr target = new LuaNameExpr { Name = ExpectName(), Line = line };
        var name = ((LuaNameExpr)target).Name;

        while (CheckSymbol("."))
        {
            index++;
            var key = ExpectName();
            name += "." + key;
            target = new LuaIndexExpr
            {
                Target = target,
                Key = new LuaLiteral { Value = key, Line = line },
                Line = line,
            };
        }

        var function = ParseFunctionBody(name);
        return new LuaAssignStat { Targets = [target], Values = [function], Line = line };
    }

    private LuaFunctionExpr ParseFunctionBody(string? name)
    {
        var line = Current.Line;
        ExpectSymbol("(");

        var parameters = new List<string>();
        if (!CheckSymbol(")"))
        {
            parameters.Add(ExpectName());
            while (CheckSymbol(",")) { index++; parameters.Add(ExpectName()); }
        }

        ExpectSymbol(")");
        var body = ParseBlock();
        ExpectKeyword("end");

        return new LuaFunctionExpr { Parameters = parameters, Body = body, Name = name, Line = line };
    }

    private LuaStat ParseIf()
    {
        var line = Current.Line;
        index++; // if

        var branches = new List<(LuaExpr, LuaBlock)>();
        var condition = ParseExpression();
        ExpectKeyword("then");
        branches.Add((condition, ParseBlock()));

        LuaBlock? elseBody = null;
        while (true)
        {
            if (CheckKeyword("elseif"))
            {
                index++;
                var elseIfCondition = ParseExpression();
                ExpectKeyword("then");
                branches.Add((elseIfCondition, ParseBlock()));
                continue;
            }

            if (CheckKeyword("else"))
            {
                index++;
                elseBody = ParseBlock();
            }

            break;
        }

        ExpectKeyword("end");
        return new LuaIfStat { Branches = branches, ElseBody = elseBody, Line = line };
    }

    private LuaStat ParseWhile()
    {
        var line = Current.Line;
        index++;
        var condition = ParseExpression();
        ExpectKeyword("do");
        var body = ParseBlock();
        ExpectKeyword("end");
        return new LuaWhileStat { Condition = condition, Body = body, Line = line };
    }

    private LuaStat ParseRepeat()
    {
        var line = Current.Line;
        index++;
        var body = ParseBlock();
        ExpectKeyword("until");
        var condition = ParseExpression();
        return new LuaRepeatStat { Body = body, Condition = condition, Line = line };
    }

    private LuaStat ParseFor()
    {
        var line = Current.Line;
        index++; // for

        var firstName = ExpectName();

        if (CheckSymbol("="))
        {
            index++;
            var start = ParseExpression();
            ExpectSymbol(",");
            var limit = ParseExpression();

            LuaExpr? step = null;
            if (CheckSymbol(",")) { index++; step = ParseExpression(); }

            ExpectKeyword("do");
            var body = ParseBlock();
            ExpectKeyword("end");

            return new LuaNumericForStat
            {
                Variable = firstName,
                Start = start,
                Limit = limit,
                Step = step,
                Body = body,
                Line = line,
            };
        }

        var names = new List<string> { firstName };
        while (CheckSymbol(",")) { index++; names.Add(ExpectName()); }

        ExpectKeyword("in");
        var iterable = ParseExpression();
        ExpectKeyword("do");
        var forBody = ParseBlock();
        ExpectKeyword("end");

        return new LuaGenericForStat { Variables = names, Iterable = iterable, Body = forBody, Line = line };
    }

    // ---- 式 ----

    private static int BinaryPriority(string op) => op switch
    {
        "or" => 1,
        "and" => 2,
        "<" or ">" or "<=" or ">=" or "~=" or "==" => 3,
        ".." => 4,
        "+" or "-" => 5,
        "*" or "/" or "%" => 6,
        "^" => 8,
        _ => 0,
    };

    /// <summary>^ は右結合、.. も右結合。</summary>
    private static bool IsRightAssociative(string op) => op is "^" or "..";

    private LuaExpr ParseExpression(int limit = 0)
    {
        LuaExpr left;

        if (CheckKeyword("not") || CheckSymbol("-") || CheckSymbol("#"))
        {
            var line = Current.Line;
            var op = Current.Text;
            index++;
            var operand = ParseExpression(7); // 単項演算子の優先度
            left = new LuaUnaryExpr { Operator = op, Operand = operand, Line = line };
        }
        else
        {
            left = ParseSimpleExpression();
        }

        while (true)
        {
            var op = Current.Text;
            if (Current.Type is not (LuaTokenType.Symbol or LuaTokenType.Keyword)) break;

            var priority = BinaryPriority(op);
            if (priority == 0 || priority <= limit) break;

            var line = Current.Line;
            index++;
            var right = ParseExpression(IsRightAssociative(op) ? priority - 1 : priority);
            left = new LuaBinaryExpr { Operator = op, Left = left, Right = right, Line = line };
        }

        return left;
    }

    private LuaExpr ParseSimpleExpression()
    {
        var line = Current.Line;

        switch (Current.Type)
        {
            case LuaTokenType.Number:
            {
                var value = Current.Number;
                index++;
                return new LuaLiteral { Value = value, Line = line };
            }

            case LuaTokenType.String:
            {
                var value = Current.Text;
                index++;
                return new LuaLiteral { Value = value, Line = line };
            }

            case LuaTokenType.Keyword when Current.Text == "nil":
                index++;
                return new LuaLiteral { Value = null, Line = line };

            case LuaTokenType.Keyword when Current.Text == "true":
                index++;
                return new LuaLiteral { Value = true, Line = line };

            case LuaTokenType.Keyword when Current.Text == "false":
                index++;
                return new LuaLiteral { Value = false, Line = line };

            case LuaTokenType.Keyword when Current.Text == "function":
                index++;
                return ParseFunctionBody(null);

            case LuaTokenType.Symbol when Current.Text == "{":
                return ParseTable();

            case LuaTokenType.Symbol when Current.Text == "...":
                index++;
                return new LuaVarargExpr { Line = line };

            default:
                return ParseSuffixedExpression();
        }
    }

    private LuaExpr ParsePrimaryExpression()
    {
        var line = Current.Line;

        if (Current.Type == LuaTokenType.Name)
        {
            var name = Current.Text;
            index++;
            return new LuaNameExpr { Name = name, Line = line };
        }

        if (CheckSymbol("("))
        {
            index++;
            var inner = ParseExpression();
            ExpectSymbol(")");
            return inner;
        }

        throw new LuaSyntaxException($"式が必要ですが '{Current}' が見つかりました。", line);
    }

    private LuaExpr ParseSuffixedExpression()
    {
        var expression = ParsePrimaryExpression();

        while (true)
        {
            var line = Current.Line;

            if (CheckSymbol("."))
            {
                index++;
                var key = ExpectName();
                expression = new LuaIndexExpr
                {
                    Target = expression,
                    Key = new LuaLiteral { Value = key, Line = line },
                    Line = line,
                };
                continue;
            }

            if (CheckSymbol("["))
            {
                index++;
                var key = ParseExpression();
                ExpectSymbol("]");
                expression = new LuaIndexExpr { Target = expression, Key = key, Line = line };
                continue;
            }

            if (CheckSymbol("("))
            {
                index++;
                var arguments = new List<LuaExpr>();
                if (!CheckSymbol(")"))
                {
                    arguments.Add(ParseExpression());
                    while (CheckSymbol(",")) { index++; arguments.Add(ParseExpression()); }
                }

                ExpectSymbol(")");
                expression = new LuaCallExpr { Target = expression, Arguments = arguments, Line = line };
                continue;
            }

            // f{...} / f"..." の糖衣構文
            if (CheckSymbol("{"))
            {
                var table = ParseTable();
                expression = new LuaCallExpr { Target = expression, Arguments = [table], Line = line };
                continue;
            }

            if (Current.Type == LuaTokenType.String)
            {
                var literal = new LuaLiteral { Value = Current.Text, Line = line };
                index++;
                expression = new LuaCallExpr { Target = expression, Arguments = [literal], Line = line };
                continue;
            }

            return expression;
        }
    }

    private LuaExpr ParseTable()
    {
        var line = Current.Line;
        ExpectSymbol("{");

        var arrayItems = new List<LuaExpr>();
        var hashItems = new List<(LuaExpr, LuaExpr)>();

        while (!CheckSymbol("}"))
        {
            if (Current.Type == LuaTokenType.Eof)
                throw new LuaSyntaxException("テーブルが閉じられていません。", line);

            if (CheckSymbol("["))
            {
                index++;
                var key = ParseExpression();
                ExpectSymbol("]");
                ExpectSymbol("=");
                hashItems.Add((key, ParseExpression()));
            }
            else if (Current.Type == LuaTokenType.Name && Peek().Is(LuaTokenType.Symbol, "="))
            {
                var key = new LuaLiteral { Value = Current.Text, Line = Current.Line };
                index += 2;
                hashItems.Add((key, ParseExpression()));
            }
            else
            {
                arrayItems.Add(ParseExpression());
            }

            if (CheckSymbol(",") || CheckSymbol(";")) index++;
            else break;
        }

        ExpectSymbol("}");
        return new LuaTableExpr { ArrayItems = arrayItems, HashItems = hashItems, Line = line };
    }

    // ---- ヘルパー ----

    private bool CheckSymbol(string text) => Current.Is(LuaTokenType.Symbol, text);

    private bool CheckKeyword(string text) => Current.Is(LuaTokenType.Keyword, text);

    private void ExpectSymbol(string text)
    {
        if (!CheckSymbol(text))
            throw new LuaSyntaxException($"'{text}' が必要ですが '{Current}' が見つかりました。", Current.Line);
        index++;
    }

    private void ExpectKeyword(string text)
    {
        if (!CheckKeyword(text))
            throw new LuaSyntaxException($"'{text}' が必要ですが '{Current}' が見つかりました。", Current.Line);
        index++;
    }

    private string ExpectName()
    {
        if (Current.Type != LuaTokenType.Name)
            throw new LuaSyntaxException($"識別子が必要ですが '{Current}' が見つかりました。", Current.Line);
        var name = Current.Text;
        index++;
        return name;
    }
}
