using System.Text.Json;
using System.Text.Json.Serialization;
using Ymm4DanmakuPlugin.Core.Model;

namespace Ymm4DanmakuPlugin.Core.Serialization;

/// <summary><see cref="BulletColor"/> を "#RRGGBB" / "#AARRGGBB" 形式で読み書きするコンバーター。</summary>
public sealed class BulletColorJsonConverter : JsonConverter<BulletColor>
{
    public override BulletColor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return BulletColor.FromHex(reader.GetString());

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            float r = 1, g = 1, b = 1, a = 1;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName) continue;
                var name = reader.GetString();
                if (!reader.Read()) break;
                var value = reader.TokenType == JsonTokenType.Number ? reader.GetSingle() : 0f;
                switch (name?.ToLowerInvariant())
                {
                    case "r": r = value; break;
                    case "g": g = value; break;
                    case "b": b = value; break;
                    case "a": a = value; break;
                }
            }

            return new BulletColor(r, g, b, a);
        }

        return BulletColor.White;
    }

    public override void Write(Utf8JsonWriter writer, BulletColor value, JsonSerializerOptions options)
    {
        static int Channel(float v) => (int)Math.Round(Math.Clamp(v, 0f, 1f) * 255f);
        writer.WriteStringValue(
            $"#{Channel(value.A):X2}{Channel(value.R):X2}{Channel(value.G):X2}{Channel(value.B):X2}");
    }
}

/// <summary>プラグイン共通の JSON 設定。</summary>
public static class DanmakuJson
{
    /// <summary>読み書き共通のオプション (整形あり)。</summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions(indented: true);

    /// <summary>コンパクト出力用のオプション。</summary>
    public static JsonSerializerOptions CompactOptions { get; } = CreateOptions(indented: false);

    private static JsonSerializerOptions CreateOptions(bool indented)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = indented,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true));
        options.Converters.Add(new BulletColorJsonConverter());
        return options;
    }

    public static string Serialize<T>(T value, bool indented = true) =>
        JsonSerializer.Serialize(value, indented ? Options : CompactOptions);

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
}
