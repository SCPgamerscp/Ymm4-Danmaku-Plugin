// YMM4 API スタブ (ビルド検証専用)。実装は空で、実行はできません。
using System.Reflection;
using System.Windows;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Settings;

namespace YukkuriMovieMaker.Controls;

/// <summary>スタブ用の共通実装 (コントロールは生成しない)。</summary>
public abstract class StubEditorAttribute : PropertyEditorAttribute
{
    public override FrameworkElement Create() => throw new NotSupportedException("スタブです。");

    public override void SetBindings(
        FrameworkElement control,
        object item,
        object propertyOwner,
        PropertyInfo propertyInfo) => throw new NotSupportedException("スタブです。");

    public override void ClearBindings(FrameworkElement control) => throw new NotSupportedException("スタブです。");
}

/// <summary>アニメーション編集用スライダー (スタブ)。</summary>
public sealed class AnimationSliderAttribute(
    string format = "F0",
    string unit = "",
    double min = 0,
    double max = 100) : StubEditorAttribute
{
    public string Format { get; } = format;

    public string Unit { get; } = unit;

    public double Min { get; } = min;

    public double Max { get; } = max;
}

/// <summary>数値編集用スライダー (スタブ)。</summary>
public sealed class TextBoxSliderAttribute(
    string format = "F0",
    string unit = "",
    double min = 0,
    double max = 100) : StubEditorAttribute
{
    public string Format { get; } = format;

    public string Unit { get; } = unit;

    public double Min { get; } = min;

    public double Max { get; } = max;
}

/// <summary>色選択コントロール (スタブ)。</summary>
public sealed class ColorPickerAttribute : StubEditorAttribute;

/// <summary>フォルダ選択コントロール (スタブ)。</summary>
public sealed class DirectorySelectorAttribute : StubEditorAttribute;

/// <summary>ファイル選択コントロール (スタブ)。</summary>
public sealed class FileSelectorAttribute(FileGroupType type = FileGroupType.None) : StubEditorAttribute
{
    public FileGroupType Type { get; } = type;
}

/// <summary>列挙型選択コントロール (スタブ)。</summary>
public sealed class EnumComboBoxAttribute : StubEditorAttribute;

/// <summary>フォント選択コントロール (スタブ)。</summary>
public sealed class FontComboBoxAttribute : StubEditorAttribute;

/// <summary>テキスト編集コントロール (スタブ)。</summary>
public sealed class TextEditorAttribute : StubEditorAttribute
{
    public bool AcceptsReturn { get; set; }
}

/// <summary>bool 編集コントロール (スタブ)。</summary>
public sealed class ToggleSliderAttribute : StubEditorAttribute;
