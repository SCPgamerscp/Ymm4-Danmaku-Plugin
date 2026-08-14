// YMM4 API スタブ (ビルド検証専用)。実装は空で、実行はできません。
// 実 API 形状の出典: https://github.com/ikoma-reunion/ymm4-plugin-document
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace YukkuriMovieMaker.Commons;

/// <summary>キーフレーム集合 (スタブ)。</summary>
public class KeyFrames;

/// <summary>アニメーション可能なオブジェクト (スタブ)。</summary>
public interface IAnimatable
{
    void SetAnimationParameters(int animationLength, int videoFPS);

    void SetKeyFrames(KeyFrames keyFrames);
}

/// <summary>プロパティ変更通知 + アニメーション対応の基底クラス (スタブ)。</summary>
public abstract class Animatable : IAnimatable, System.ComponentModel.INotifyPropertyChanged
{
    protected int videoFPS = 60;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public virtual void BeginEdit() { }

    public virtual Task EndEditAsync() => Task.CompletedTask;

    /// <summary>このクラスが保持する <see cref="IAnimatable"/> を列挙する。</summary>
    protected abstract IEnumerable<IAnimatable> GetAnimatables();

    protected bool Set<T>(
        ref T storage,
        T value,
        [CallerMemberName] string name = "",
        params string[] etcChangedPropertyNames)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
        storage = value;
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        foreach (var etc in etcChangedPropertyNames)
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(etc));
        return true;
    }

    protected bool Set<T>(
        Expression<Func<T>> propertySelector,
        T value,
        [CallerMemberName] string name = "",
        params string[] etcChangedPropertyNames)
        => throw new NotSupportedException("スタブです。");

    public void SetAnimationParameters(int animationLength, int videoFPS)
    {
        this.videoFPS = videoFPS;
        foreach (var animatable in GetAnimatables())
            animatable.SetAnimationParameters(animationLength, videoFPS);
    }

    public void SetKeyFrames(KeyFrames keyFrames)
    {
        foreach (var animatable in GetAnimatables())
            animatable.SetKeyFrames(keyFrames);
    }
}

/// <summary>キーフレームで時間変化する数値 (スタブ)。</summary>
public class Animation : IAnimatable
{
    public Animation() : this(0) { }

    public Animation(double defaultValue) : this(defaultValue, double.MinValue, double.MaxValue) { }

    public Animation(double defaultValue, double minValue, double maxValue, double loop = 0.0)
    {
        DefaultValue = defaultValue;
        MinValue = minValue;
        MaxValue = maxValue;
        Loop = loop;
    }

    public double DefaultValue { get; }

    public double MinValue { get; }

    public double MaxValue { get; }

    public double Loop { get; }

    public IReadOnlyList<double> Values => [DefaultValue];

    /// <summary>指定フレームでの値を取得する。スタブでは既定値を返す。</summary>
    public double GetValue(long frame, long totalFrame, int fps) => DefaultValue;

    public void AddToEachValues(double delta) { }

    public void MultiplyToEachValues(double delta) { }

    public void CopyFrom(Animation animation) { }

    public bool DeepEquals(Animation animation) => false;

    public void BeginEdit() { }

    public Task EndEditAsync() => Task.CompletedTask;

    public void SetAnimationParameters(int animationLength, int videoFPS) { }

    public void SetKeyFrames(KeyFrames keyFrames) { }

    public string ToExoString(int keyFrameIndex, string format, int fps, Func<double, double>? converter = null)
        => DefaultValue.ToString(format);

    public string ToExoStringForOpacityToTransparency(int keyFrameIndex, string format, int fps)
        => DefaultValue.ToString(format);
}
