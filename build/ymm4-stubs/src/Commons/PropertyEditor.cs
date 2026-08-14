// YMM4 API スタブ (ビルド検証専用)。実装は空で、実行はできません。
using System.Reflection;
using System.Windows;

namespace YukkuriMovieMaker.Commons;

/// <summary>アイテム編集エリアでのコントロール幅 (スタブ)。</summary>
public enum PropertyEditorSize
{
    Normal,
    FullWidth,
}

/// <summary>アイテム編集エリアに表示するコントロールを指定する属性の基底 (スタブ)。</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public abstract class PropertyEditorAttribute : Attribute
{
    protected PropertyEditorAttribute() { }

    public PropertyEditorSize PropertyEditorSize { get; set; } = PropertyEditorSize.Normal;

    public double MinHeight { get; set; }

    public abstract FrameworkElement Create();

    public abstract void SetBindings(
        FrameworkElement control,
        object item,
        object propertyOwner,
        PropertyInfo propertyInfo);

    public abstract void ClearBindings(FrameworkElement control);
}

/// <summary>カスタムコントロール用の属性基底 (スタブ)。</summary>
public abstract class PropertyEditorAttribute2 : PropertyEditorAttribute;

/// <summary>アイテム編集エリアのカスタムコントロールが実装するインターフェース (スタブ)。</summary>
public interface IPropertyEditorControl
{
    event EventHandler? BeginEdit;

    event EventHandler? EndEdit;
}
