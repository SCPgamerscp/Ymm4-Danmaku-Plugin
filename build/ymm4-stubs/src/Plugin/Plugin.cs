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

/// <summary>YMM4 の設定画面に項目を追加するためのインターフェース (スタブ)。</summary>
public interface ISetting
{
    /// <summary>設定画面での分類名。</summary>
    string Category { get; }

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
/// <para>
/// 実物は <c>%AppData%</c> 配下へ JSON として自動保存し、
/// <see cref="Default"/> でシングルトンを提供する。
/// </para>
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

    public abstract string Category { get; }

    public abstract string Name { get; }

    public abstract bool HasSettingView { get; }

    public abstract object? SettingView { get; }

    public virtual void Initialize() { }

    public virtual void Save() { }
}
