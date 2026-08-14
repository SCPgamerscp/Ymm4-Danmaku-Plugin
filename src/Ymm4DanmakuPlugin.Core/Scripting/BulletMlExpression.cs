using System.Globalization;
using Ymm4DanmakuPlugin.Core.Mathematics;

namespace Ymm4DanmakuPlugin.Core.Scripting;

/// <summary>BulletML の式を評価するときに参照される変数群。</summary>
public readonly struct BulletMlVariables(double[] parameters, double rank, double loopIndex, DeterministicRandom random)
{
    /// <summary>$1〜$9 に対応するパラメータ。</summary>
    public double[] Parameters { get; } = parameters;

    /// <summary>$rank (難易度、0〜1)。</summary>
    public double Rank { get; } = rank;

    /// <summary>$i / $loop.index (repeat のループ回数)。</summary>
    public double LoopIndex { get; } = loopIndex;

    /// <summary>$rand に使用する乱数生成器。</summary>
    public DeterministicRandom Random { get; } = random;
}

/// <summary>
/// BulletML の数式。<c>$rand * 30 + 90</c> のような式を評価する。
/// <para>
/// パース済みの構文木を保持するため、毎フレームの評価コストは小さい。
/// </para>
/// </summary>
public sealed class BulletMlExpression
{
    private readonly Node root;

    /// <summary>元の式文字列。</summary>
    public string Source { get; }

    /// <summary>定数式の場合の値 (定数でなければ null)。</summary>
    public double? ConstantValue { get; }

    private BulletMlExpression(Node root, string source)
    {
        this.root = root;
        Source = source;
        ConstantValue = root is NumberNode n ? n.Value : null;
    }

    /// <summary>定数 0 の式。</summary>
    public static BulletMlExpression Zero { get; } = Constant(0);

    public static BulletMlExpression Constant(double value) =>
        new(new NumberNode(value), value.ToString(CultureInfo.InvariantCulture));

    /// <summary>式をパースする。空文字列の場合は既定値の定数式を返す。</summary>
    public static BulletMlExpression Parse(string? text, double defaultValue = 0)
    {
        if (string.IsNullOrWhiteSpace(text)) return Constant(defaultValue);

        var parser = new Parser(text);
        var node = parser.ParseExpression();
        parser.SkipWhitespace();
        if (!parser.AtEnd)
            throw new BulletMlParseException($"式を解釈できません: '{text}' ({parser.Position} 文字目以降)");

        return new BulletMlExpression(Fold(node), text);
    }

    /// <summary>パースに失敗しても例外を投げず、既定値の式を返す。</summary>
    public static BulletMlExpression ParseSafe(string? text, double defaultValue = 0)
    {
        try
        {
            return Parse(text, defaultValue);
        }
        catch (BulletMlParseException)
        {
            return Constant(defaultValue);
        }
    }

    public double Evaluate(in BulletMlVariables variables) => root.Evaluate(in variables);

    public override string ToString() => Source;

    /// <summary>定数畳み込み。</summary>
    private static Node Fold(Node node)
    {
        if (node is BinaryNode b)
        {
            var left = Fold(b.Left);
            var right = Fold(b.Right);
            if (left is NumberNode ln && right is NumberNode rn)
            {
                var dummy = new BulletMlVariables([], 0, 0, new DeterministicRandom(0));
                return new NumberNode(new BinaryNode(b.Op, ln, rn).Evaluate(in dummy));
            }

            return new BinaryNode(b.Op, left, right);
        }

        return node;
    }

    #region 構文木

    private abstract class Node
    {
        public abstract double Evaluate(in BulletMlVariables variables);
    }

    private sealed class NumberNode(double value) : Node
    {
        public double Value { get; } = value;
        public override double Evaluate(in BulletMlVariables variables) => Value;
    }

    private sealed class ParameterNode(int index) : Node
    {
        public override double Evaluate(in BulletMlVariables variables) =>
            index >= 1 && index <= variables.Parameters.Length ? variables.Parameters[index - 1] : 0;
    }

    private sealed class RandomNode : Node
    {
        public override double Evaluate(in BulletMlVariables variables) => variables.Random.NextDouble();
    }

    private sealed class RankNode : Node
    {
        public override double Evaluate(in BulletMlVariables variables) => variables.Rank;
    }

    private sealed class LoopIndexNode : Node
    {
        public override double Evaluate(in BulletMlVariables variables) => variables.LoopIndex;
    }

    private sealed class BinaryNode(char op, Node left, Node right) : Node
    {
        public char Op { get; } = op;
        public Node Left { get; } = left;
        public Node Right { get; } = right;

        public override double Evaluate(in BulletMlVariables variables)
        {
            var l = Left.Evaluate(in variables);
            var r = Right.Evaluate(in variables);
            return Op switch
            {
                '+' => l + r,
                '-' => l - r,
                '*' => l * r,
                '/' => Math.Abs(r) < 1e-12 ? 0 : l / r,
                '%' => Math.Abs(r) < 1e-12 ? 0 : l % r,
                _ => 0,
            };
        }
    }

    private sealed class NegateNode(Node inner) : Node
    {
        public override double Evaluate(in BulletMlVariables variables) => -inner.Evaluate(in variables);
    }

    #endregion

    #region パーサー

    private sealed class Parser(string text)
    {
        private readonly string text = text;
        private int position;

        public int Position => position;
        public bool AtEnd => position >= text.Length;

        public void SkipWhitespace()
        {
            while (position < text.Length && char.IsWhiteSpace(text[position])) position++;
        }

        public Node ParseExpression()
        {
            var left = ParseTerm();
            while (true)
            {
                SkipWhitespace();
                if (AtEnd) return left;
                var c = text[position];
                if (c is not ('+' or '-')) return left;
                position++;
                var right = ParseTerm();
                left = new BinaryNode(c, left, right);
            }
        }

        private Node ParseTerm()
        {
            var left = ParseUnary();
            while (true)
            {
                SkipWhitespace();
                if (AtEnd) return left;
                var c = text[position];
                if (c is not ('*' or '/' or '%')) return left;
                position++;
                var right = ParseUnary();
                left = new BinaryNode(c, left, right);
            }
        }

        private Node ParseUnary()
        {
            SkipWhitespace();
            if (!AtEnd && text[position] == '-')
            {
                position++;
                return new NegateNode(ParseUnary());
            }

            if (!AtEnd && text[position] == '+')
            {
                position++;
                return ParseUnary();
            }

            return ParsePrimary();
        }

        private Node ParsePrimary()
        {
            SkipWhitespace();
            if (AtEnd) throw new BulletMlParseException("式が途中で終了しています。");

            var c = text[position];

            if (c == '(')
            {
                position++;
                var inner = ParseExpression();
                SkipWhitespace();
                if (AtEnd || text[position] != ')')
                    throw new BulletMlParseException("')' が見つかりません。");
                position++;
                return inner;
            }

            if (c == '$') return ParseVariable();

            if (char.IsDigit(c) || c == '.') return ParseNumber();

            throw new BulletMlParseException($"予期しない文字 '{c}' です。");
        }

        private Node ParseNumber()
        {
            var start = position;
            while (position < text.Length && (char.IsDigit(text[position]) || text[position] == '.')) position++;
            var slice = text.AsSpan(start, position - start);
            if (!double.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                throw new BulletMlParseException($"数値を解釈できません: '{slice.ToString()}'");
            return new NumberNode(value);
        }

        private Node ParseVariable()
        {
            position++; // '$'
            var start = position;
            while (position < text.Length &&
                   (char.IsLetterOrDigit(text[position]) || text[position] == '.' || text[position] == '_'))
            {
                position++;
            }

            var name = text[start..position];
            if (name.Length == 0) throw new BulletMlParseException("'$' の後に変数名がありません。");

            if (int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
                return new ParameterNode(index);

            return name.ToLowerInvariant() switch
            {
                "rand" => new RandomNode(),
                "rank" => new RankNode(),
                "i" or "loop.index" or "loopindex" => new LoopIndexNode(),
                _ => throw new BulletMlParseException($"未知の変数です: '${name}'"),
            };
        }
    }

    #endregion
}

/// <summary>BulletML の解析エラー。</summary>
public sealed class BulletMlParseException(string message, Exception? innerException = null)
    : Exception(message, innerException);
