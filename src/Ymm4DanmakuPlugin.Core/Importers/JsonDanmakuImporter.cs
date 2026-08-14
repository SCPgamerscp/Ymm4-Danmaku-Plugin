using System.Text.Json;
using System.Text.Json.Serialization;
using Ymm4DanmakuPlugin.Core.Engine;
using Ymm4DanmakuPlugin.Core.Model;
using Ymm4DanmakuPlugin.Core.Presets;
using Ymm4DanmakuPlugin.Core.Serialization;

namespace Ymm4DanmakuPlugin.Core.Importers;

/// <summary>
/// JSON 形式の弾幕データを読み込むインポーター。
/// <para>2 種類の形式に対応する。</para>
/// <list type="number">
///   <item><description>
///     <c>shots</c> 配列を持つ「タイムライン形式」。時刻付きの発射命令をそのまま列挙する。
///   </description></item>
///   <item><description>
///     <c>pattern</c> / <c>physics</c> / <c>appearance</c> を持つ「プリセット形式」。
///     GUI のスライダー設定と等価な内容を記述する。
///   </description></item>
/// </list>
/// </summary>
public sealed class JsonDanmakuImporter : IDanmakuImporter
{
    public string Name => "JSON";

    public IReadOnlyList<string> SupportedExtensions { get; } = [".json"];

    public bool CanImport(string text)
    {
        var trimmed = text.AsSpan().TrimStart();
        return trimmed.Length > 0 && trimmed[0] == '{';
    }

    public DanmakuImportResult Import(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return DanmakuImportResult.Failure("JSON が空です。");

        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return DanmakuImportResult.Failure("JSON のルートがオブジェクトではありません。");

            if (root.TryGetProperty("shots", out var shotsElement) && shotsElement.ValueKind == JsonValueKind.Array)
                return ImportTimeline(text);

            if (HasAnyProperty(root, "pattern", "physics", "appearance"))
                return ImportPreset(text);

            return DanmakuImportResult.Failure(
                "'shots' 配列も 'pattern' / 'physics' / 'appearance' も見つかりません。");
        }
        catch (JsonException e)
        {
            return DanmakuImportResult.Failure($"JSON の解析に失敗しました: {e.Message}");
        }
    }

    private static bool HasAnyProperty(JsonElement element, params string[] names) =>
        names.Any(name => element.TryGetProperty(name, out _));

    private static DanmakuImportResult ImportTimeline(string text)
    {
        var dto = JsonSerializer.Deserialize<TimelineDto>(text, DanmakuJson.Options);
        if (dto is null) return DanmakuImportResult.Failure("タイムライン JSON を読み込めませんでした。");

        var warnings = new List<string>();
        var shots = new List<ScriptedShot>(dto.Shots.Length);

        for (var i = 0; i < dto.Shots.Length; i++)
        {
            var s = dto.Shots[i];
            if (s.Way <= 0)
            {
                warnings.Add($"shots[{i}]: way が 0 以下のため 1 に補正しました。");
                s = s with { Way = 1 };
            }

            if (s.Time < 0)
            {
                warnings.Add($"shots[{i}]: time が負のため 0 に補正しました。");
                s = s with { Time = 0 };
            }

            shots.Add(new ScriptedShot
            {
                Time = s.Time,
                Angle = s.Angle,
                AimAtTarget = s.Aim,
                Way = s.Way,
                Spread = s.Spread,
                Speed = s.Speed,
                Acceleration = s.Accel,
                AngularVelocity = s.AngularVelocity,
                Lifetime = s.Lifetime,
                SpriteIndex = s.Sprite,
                Color = string.IsNullOrWhiteSpace(s.Color) ? null : BulletColor.FromHex(s.Color),
                ScaleFactor = s.Scale,
                OffsetX = s.OffsetX,
                OffsetY = s.OffsetY,
                PlaySound = s.Sound,
                Homing = s.Homing,
            });
        }

        return new DanmakuImportResult
        {
            Shots = new ScriptedShotProgram(shots, dto.LoopDuration),
            Warnings = warnings,
        };
    }

    private static DanmakuImportResult ImportPreset(string text)
    {
        var preset = DanmakuJson.Deserialize<DanmakuPreset>(text);
        return preset is null
            ? DanmakuImportResult.Failure("プリセット JSON を読み込めませんでした。")
            : new DanmakuImportResult { Preset = preset };
    }

    #region DTO

    private sealed record TimelineDto
    {
        public int Version { get; init; } = 1;
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("loopDuration")]
        public double LoopDuration { get; init; }

        public ShotDto[] Shots { get; init; } = [];
    }

    private sealed record ShotDto
    {
        public double Time { get; init; }
        public double Angle { get; init; }
        public bool Aim { get; init; }
        public int Way { get; init; } = 1;
        public double Spread { get; init; } = 360;
        public double Speed { get; init; } = 200;
        public double Accel { get; init; }

        [JsonPropertyName("angularVelocity")]
        public double AngularVelocity { get; init; }

        public double Lifetime { get; init; }
        public int Sprite { get; init; } = -1;
        public string? Color { get; init; }
        public double Scale { get; init; } = 1.0;

        [JsonPropertyName("offsetX")]
        public double OffsetX { get; init; }

        [JsonPropertyName("offsetY")]
        public double OffsetY { get; init; }

        public bool Sound { get; init; } = true;
        public bool? Homing { get; init; }
    }

    #endregion
}
