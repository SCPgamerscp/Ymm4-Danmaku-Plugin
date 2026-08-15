// YMM4 API スタブ (ビルド検証専用)。実装は空で、実行はできません。
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Audio.Effects;

namespace YukkuriMovieMaker.Plugin.Effects;

/// <summary>音声エフェクトであることを示す属性 (スタブ)。</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class AudioEffectAttribute(
    string name,
    string[] categories,
    string[] keywords,
    bool isAviUtlSupported = true) : Attribute
{
    public string Name { get; } = name;

    public string[] Categories { get; } = categories;

    public string[] Keywords { get; } = keywords;

    public bool IsAviUtlSupported { get; } = isAviUtlSupported;

    public Type? ResourceType { get; set; }

    public string GetName() => Name;

    public string[] GetCategories() => Categories;
}

/// <summary>音声エフェクトの基底クラス (スタブ)。</summary>
public abstract class AudioEffectBase : Animatable
{
    /// <summary>エフェクトの表示名。</summary>
    public abstract string Label { get; }

    /// <summary>有効かどうか。</summary>
    public bool IsEnabled { get => isEnabled; set => Set(ref isEnabled, value); }
    private bool isEnabled = true;

    /// <summary>備考欄。</summary>
    public string Remark { get => remark; set => Set(ref remark, value); }
    private string remark = string.Empty;

    /// <summary>音声エフェクト処理を作成する。</summary>
    public abstract IAudioEffectProcessor CreateAudioEffect(TimeSpan duration);

    /// <summary>exo の音声フィルタを生成する。</summary>
    public abstract IEnumerable<string> CreateExoAudioFilters(
        int keyFrameIndex,
        ExoOutputDescription exoOutputDescription);

    /// <summary>このエフェクトが参照するファイル一覧。</summary>
    public IEnumerable<string> GetFiles() => [];

    /// <summary>参照リソース (ファイルパス) を列挙する。派生クラスで上書きする。</summary>
    public virtual IEnumerable<YukkuriMovieMaker.Project.TimelineResource> GetResources() => [];

    /// <summary>参照ファイルのパスを置換する。</summary>
    public virtual void ReplaceFile(string from, string to) { }
}
