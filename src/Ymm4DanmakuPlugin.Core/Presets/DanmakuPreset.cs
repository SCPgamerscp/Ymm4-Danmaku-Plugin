using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Model;
using Ymm4DanmakuPlugin.Core.Serialization;

namespace Ymm4DanmakuPlugin.Core.Presets;

/// <summary>
/// 弾幕プリセット。1 つのエミッター設定 (形状・物理・見た目・分裂) をひとまとめにしたもの。
/// JSON でインポート/エクスポートできる。
/// </summary>
public sealed record DanmakuPreset
{
    /// <summary>プリセットのファイル形式バージョン。</summary>
    public int Version { get; init; } = 1;

    public string Name { get; init; } = "新規プリセット";

    public string Description { get; init; } = string.Empty;

    public string Author { get; init; } = string.Empty;

    /// <summary>分類タグ (「全方位」「レーザー」など)。</summary>
    public string[] Tags { get; init; } = [];

    public PatternSettings Pattern { get; init; } = new();

    public BulletPhysics Physics { get; init; } = new();

    public BulletAppearance Appearance { get; init; } = new();

    public SplitSpec? Split { get; init; }

    public double SplitDelay { get; init; } = 0.6;

    /// <summary>このプリセットの内容をエミッター設定へ適用する。</summary>
    public EmitterSettings ApplyTo(EmitterSettings emitter) => emitter with
    {
        SourceMode = DanmakuSourceMode.Pattern,
        Pattern = Pattern,
        Physics = Physics,
        Appearance = Appearance,
        Split = Split,
        SplitDelay = SplitDelay,
    };

    /// <summary>エミッター設定からプリセットを作る。</summary>
    public static DanmakuPreset FromEmitter(EmitterSettings emitter, string name, string description = "") => new()
    {
        Name = name,
        Description = description,
        Pattern = emitter.Pattern,
        Physics = emitter.Physics,
        Appearance = emitter.Appearance,
        Split = emitter.Split,
        SplitDelay = emitter.SplitDelay,
    };

    public string ToJson() => DanmakuJson.Serialize(this);

    public static DanmakuPreset? FromJson(string json) => DanmakuJson.Deserialize<DanmakuPreset>(json);
}

/// <summary>複数プリセットをまとめたファイル形式。</summary>
public sealed record DanmakuPresetCollection
{
    public int Version { get; init; } = 1;

    public string Name { get; init; } = "プリセット集";

    public DanmakuPreset[] Presets { get; init; } = [];

    public string ToJson() => DanmakuJson.Serialize(this);

    public static DanmakuPresetCollection? FromJson(string json) =>
        DanmakuJson.Deserialize<DanmakuPresetCollection>(json);
}
