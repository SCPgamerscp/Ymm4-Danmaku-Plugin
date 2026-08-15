// YMM4 API スタブ (ビルド検証専用)。実装は空で、実行はできません。
using System.Windows;

namespace YukkuriMovieMaker.Plugin;

/// <summary>プラグインの詳細情報 (スタブ)。</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class PluginDetailsAttribute : Attribute
{
    /// <summary>作者名。</summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>ニコニ・コモンズのコンテンツ ID など。</summary>
    public string ContentId { get; set; } = string.Empty;
}

/// <summary>すべての YMM4 プラグインが実装する基本インターフェース (スタブ)。</summary>
public interface IPlugin
{
    /// <summary>プラグインの表示名。</summary>
    string Name { get; }

    /// <summary>プラグインの詳細情報。</summary>
    PluginDetailsAttribute? Details { get; }
}

/// <summary>設定画面のカテゴリ (スタブ)。</summary>
public enum SettingsCategory
{
    None = 0,
    Voice = 1,
    VideoEffect = 2,
    AudioEffect = 3,
    VideoFileWriter = 4,
    VideoFileSource = 5,
    AudioFileSource = 6,
    ImageFileSource = 7,
    Transition = 8,
    Shape = 9,
    Tachie = 10,
    Tool = 11,
    TextCompletion = 12,
    Brush = 13,
    Other = 14,
}

/// <summary>YMM4 の設定画面に項目を追加するためのインターフェース (スタブ)。</summary>
public interface ISetting
{
    /// <summary>設定画面での分類。</summary>
    SettingsCategory Category { get; }

    /// <summary>設定画面での表示名。</summary>
    string Name { get; }

    /// <summary>専用の設定画面を持つかどうか。</summary>
    bool HasSettingView { get; }

    /// <summary>専用の設定画面 (<see cref="HasSettingView"/> が true のとき使用)。</summary>
    object? SettingView { get; }

    /// <summary>初期化する。</summary>
    void Initialize();

    /// <summary>設定を保存する。</summary>
    void Save();
}

/// <summary>
/// プラグイン設定の基底クラス (スタブ)。
/// </summary>
public abstract class SettingsBase<T> : ISetting
    where T : SettingsBase<T>, new()
{
    protected static int currentVersion;

    private static readonly Lazy<T> LazyDefault = new(() =>
    {
        var instance = new T();
        instance.Initialize();
        return instance;
    });

    /// <summary>設定のシングルトンインスタンス。</summary>
    public static T Default => LazyDefault.Value;

    protected SettingsBase() { }

    public abstract SettingsCategory Category { get; }

    public abstract string Name { get; }

    public abstract bool HasSettingView { get; }

    public abstract object? SettingView { get; }

    public abstract void Initialize();

    public virtual void Save() { }
}
