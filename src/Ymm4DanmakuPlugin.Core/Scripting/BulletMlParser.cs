using System.Xml;
using System.Xml.Linq;

namespace Ymm4DanmakuPlugin.Core.Scripting;

/// <summary>
/// BulletML (XML) を <see cref="BulletMlProgram"/> へ変換するパーサー。
/// <para>
/// ABA Games の BulletML 仕様のうち、実用上ほぼすべてで使われる以下の要素に対応する。
/// bulletml / action / actionRef / fire / fireRef / bullet / bulletRef /
/// changeDirection / changeSpeed / accel / wait / vanish / repeat / param /
/// direction / speed / horizontal / vertical / term
/// </para>
/// </summary>
public static class BulletMlParser
{
    /// <summary>XML 文字列をパースする。</summary>
    public static BulletMlProgram Parse(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            throw new BulletMlParseException("BulletML が空です。");

        XDocument document;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,  // 外部 DTD は読み込まない (セキュリティ対策)
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreWhitespace = true,
            };
            using var stringReader = new StringReader(xml);
            using var reader = XmlReader.Create(stringReader, settings);
            document = XDocument.Load(reader);
        }
        catch (XmlException e)
        {
            throw new BulletMlParseException($"BulletML の XML 解析に失敗しました: {e.Message}", e);
        }

        var root = document.Root ?? throw new BulletMlParseException("ルート要素がありません。");
        if (!NameEquals(root, "bulletml"))
            throw new BulletMlParseException($"ルート要素が <bulletml> ではありません: <{root.Name.LocalName}>");

        var actions = new Dictionary<string, BulletMlAction>(StringComparer.Ordinal);
        var bullets = new Dictionary<string, BulletMlBullet>(StringComparer.Ordinal);
        var fires = new Dictionary<string, BulletMlFire>(StringComparer.Ordinal);
        var topActions = new List<BulletMlAction>();

        foreach (var element in root.Elements())
        {
            if (NameEquals(element, "action"))
            {
                var action = ParseAction(element);
                if (action.Label is { Length: > 0 } label)
                {
                    actions[label] = action;
                    if (label.StartsWith("top", StringComparison.OrdinalIgnoreCase))
                        topActions.Add(action);
                }
            }
            else if (NameEquals(element, "bullet"))
            {
                var bullet = ParseBullet(element);
                if (bullet.Label is { Length: > 0 } label) bullets[label] = bullet;
            }
            else if (NameEquals(element, "fire"))
            {
                var fire = ParseFire(element);
                if (fire.Label is { Length: > 0 } label) fires[label] = fire;
            }
        }

        if (topActions.Count == 0 && actions.Count > 0)
            topActions.Add(actions.Values.First());

        return new BulletMlProgram
        {
            Actions = actions,
            Bullets = bullets,
            Fires = fires,
            TopActions = topActions,
            IsHorizontal = string.Equals(Attribute(root, "type"), "horizontal", StringComparison.OrdinalIgnoreCase),
        };
    }

    /// <summary>ファイルからパースする。</summary>
    public static BulletMlProgram ParseFile(string path) => Parse(File.ReadAllText(path));

    private static BulletMlAction ParseAction(XElement element)
    {
        var commands = new List<IBulletMlCommand>();

        foreach (var child in element.Elements())
        {
            if (NameEquals(child, "fire"))
                commands.Add(new BulletMlFireRef(null, ParseFire(child), []));
            else if (NameEquals(child, "fireRef"))
                commands.Add(new BulletMlFireRef(Attribute(child, "label"), null, ParseParams(child)));
            else if (NameEquals(child, "action"))
                commands.Add(new BulletMlActionRef(null, ParseAction(child), []));
            else if (NameEquals(child, "actionRef"))
                commands.Add(new BulletMlActionRef(Attribute(child, "label"), null, ParseParams(child)));
            else if (NameEquals(child, "wait"))
                commands.Add(new BulletMlWait(BulletMlExpression.Parse(child.Value, 0)));
            else if (NameEquals(child, "vanish"))
                commands.Add(new BulletMlVanish());
            else if (NameEquals(child, "repeat"))
                commands.Add(ParseRepeat(child));
            else if (NameEquals(child, "changeDirection"))
                commands.Add(ParseChangeDirection(child));
            else if (NameEquals(child, "changeSpeed"))
                commands.Add(ParseChangeSpeed(child));
            else if (NameEquals(child, "accel"))
                commands.Add(ParseAccel(child));
        }

        return new BulletMlAction(Attribute(element, "label"), commands);
    }

    private static BulletMlRepeat ParseRepeat(XElement element)
    {
        var timesElement = FindChild(element, "times");
        var times = BulletMlExpression.Parse(timesElement?.Value, 1);

        var actionElement = FindChild(element, "action");
        if (actionElement is not null)
            return new BulletMlRepeat(times, new BulletMlActionRef(null, ParseAction(actionElement), []));

        var actionRef = FindChild(element, "actionRef");
        if (actionRef is not null)
            return new BulletMlRepeat(times, new BulletMlActionRef(Attribute(actionRef, "label"), null, ParseParams(actionRef)));

        return new BulletMlRepeat(times, new BulletMlActionRef(null, new BulletMlAction(null, []), []));
    }

    private static BulletMlChangeDirection ParseChangeDirection(XElement element)
    {
        var directionElement = FindChild(element, "direction");
        var (expression, type) = ParseDirection(directionElement);
        var term = BulletMlExpression.Parse(FindChild(element, "term")?.Value, 1);
        return new BulletMlChangeDirection(expression ?? BulletMlExpression.Zero, type, term);
    }

    private static BulletMlChangeSpeed ParseChangeSpeed(XElement element)
    {
        var speedElement = FindChild(element, "speed");
        var (expression, type) = ParseSpeed(speedElement);
        var term = BulletMlExpression.Parse(FindChild(element, "term")?.Value, 1);
        return new BulletMlChangeSpeed(expression ?? BulletMlExpression.Zero, type, term);
    }

    private static BulletMlAccel ParseAccel(XElement element)
    {
        var horizontalElement = FindChild(element, "horizontal");
        var verticalElement = FindChild(element, "vertical");
        var term = BulletMlExpression.Parse(FindChild(element, "term")?.Value, 1);

        var (horizontal, horizontalType) = ParseSpeed(horizontalElement);
        var (vertical, verticalType) = ParseSpeed(verticalElement);

        return new BulletMlAccel(horizontal, horizontalType, vertical, verticalType, term);
    }

    private static BulletMlFire ParseFire(XElement element)
    {
        var (direction, directionType) = ParseDirection(FindChild(element, "direction"));
        var (speed, speedType) = ParseSpeed(FindChild(element, "speed"));

        var bulletElement = FindChild(element, "bullet");
        BulletMlBulletRef bulletRef;
        if (bulletElement is not null)
        {
            bulletRef = new BulletMlBulletRef(null, ParseBullet(bulletElement), []);
        }
        else
        {
            var bulletRefElement = FindChild(element, "bulletRef");
            bulletRef = bulletRefElement is not null
                ? new BulletMlBulletRef(Attribute(bulletRefElement, "label"), null, ParseParams(bulletRefElement))
                : new BulletMlBulletRef(null, new BulletMlBullet(null, null, BulletMlDirectionType.Aim, null,
                    BulletMlSpeedType.Absolute, []), []);
        }

        return new BulletMlFire(Attribute(element, "label"), direction, directionType, speed, speedType, bulletRef);
    }

    private static BulletMlBullet ParseBullet(XElement element)
    {
        var (direction, directionType) = ParseDirection(FindChild(element, "direction"));
        var (speed, speedType) = ParseSpeed(FindChild(element, "speed"));

        var actions = new List<BulletMlActionRef>();
        foreach (var child in element.Elements())
        {
            if (NameEquals(child, "action"))
                actions.Add(new BulletMlActionRef(null, ParseAction(child), []));
            else if (NameEquals(child, "actionRef"))
                actions.Add(new BulletMlActionRef(Attribute(child, "label"), null, ParseParams(child)));
        }

        return new BulletMlBullet(Attribute(element, "label"), direction, directionType, speed, speedType, actions);
    }

    private static (BulletMlExpression? Expression, BulletMlDirectionType Type) ParseDirection(XElement? element)
    {
        if (element is null) return (null, BulletMlDirectionType.Aim);

        var type = Attribute(element, "type")?.ToLowerInvariant() switch
        {
            "absolute" => BulletMlDirectionType.Absolute,
            "relative" => BulletMlDirectionType.Relative,
            "sequence" => BulletMlDirectionType.Sequence,
            _ => BulletMlDirectionType.Aim,
        };

        return (BulletMlExpression.Parse(element.Value, 0), type);
    }

    private static (BulletMlExpression? Expression, BulletMlSpeedType Type) ParseSpeed(XElement? element)
    {
        if (element is null) return (null, BulletMlSpeedType.Absolute);

        var type = Attribute(element, "type")?.ToLowerInvariant() switch
        {
            "relative" => BulletMlSpeedType.Relative,
            "sequence" => BulletMlSpeedType.Sequence,
            _ => BulletMlSpeedType.Absolute,
        };

        return (BulletMlExpression.Parse(element.Value, 0), type);
    }

    private static IReadOnlyList<BulletMlExpression> ParseParams(XElement element)
    {
        List<BulletMlExpression>? parameters = null;
        foreach (var child in element.Elements())
        {
            if (!NameEquals(child, "param")) continue;
            parameters ??= [];
            parameters.Add(BulletMlExpression.Parse(child.Value, 0));
        }

        return (IReadOnlyList<BulletMlExpression>?)parameters ?? [];
    }

    private static XElement? FindChild(XElement element, string localName) =>
        element.Elements().FirstOrDefault(e => NameEquals(e, localName));

    private static bool NameEquals(XElement element, string localName) =>
        string.Equals(element.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase);

    private static string? Attribute(XElement element, string name) =>
        element.Attributes().FirstOrDefault(a =>
            string.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))?.Value;
}
