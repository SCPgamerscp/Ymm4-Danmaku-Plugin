// YMM4 API スタブ (ビルド検証専用)。実装は空で、実行はできません。
using System.Drawing;
using System.Numerics;
using System.Windows.Input;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Player.Video;

/// <summary>フレーム番号と時刻のペア (スタブ)。</summary>
public class FrameTime
{
    public FrameTime(int frame, int fps)
    {
        Frame = frame;
        Time = TimeSpan.FromSeconds(fps <= 0 ? 0 : (double)frame / fps);
    }

    public FrameTime(TimeSpan time, int fps)
    {
        Time = time;
        Frame = (int)Math.Round(time.TotalSeconds * fps);
    }

    public int Frame { get; }

    public TimeSpan Time { get; }
}

/// <summary>タイムラインソースの用途 (スタブ)。</summary>
public enum TimelineSourceUsage
{
    Preview,
    Export,
}

/// <summary>シーン情報 (スタブ)。</summary>
public interface ISceneInfo
{
    Guid Id { get; }
}

/// <summary>タイムライン全体の情報 (スタブ)。</summary>
public record TimelineSourceDescription(
    Size ScreenSize,
    FrameTime TimelinePosition,
    FrameTime TimelineDuration,
    int FPS,
    TimelineSourceUsage Usage,
    Guid SceneId,
    IEnumerable<ISceneInfo> Scenes);

/// <summary>タイムライン上の 1 アイテムの情報 (スタブ)。</summary>
public record TimelineItemSourceDescription : TimelineSourceDescription
{
    public TimelineItemSourceDescription(
        TimelineSourceDescription timelineSourceDescription,
        int itemFrame,
        int itemLength,
        int layer)
        : base(timelineSourceDescription)
    {
        ItemPosition = new FrameTime(itemFrame, timelineSourceDescription.FPS);
        ItemDuration = new FrameTime(itemLength, timelineSourceDescription.FPS);
        Layer = layer;
    }

    /// <summary>アイテム先頭からの相対位置。</summary>
    public FrameTime ItemPosition { get; }

    /// <summary>アイテムの長さ。</summary>
    public FrameTime ItemDuration { get; }

    public int Layer { get; }
}

/// <summary>描画時の情報 (スタブ)。</summary>
public class DrawDescription;

/// <summary>制御点のドラッグ情報 (スタブ)。</summary>
public class ControlPointDragEventArgs(Vector3 delta, ModifierKeys modifierKeys)
{
    public Vector3 Delta { get; } = delta;

    public ModifierKeys ModifierKeys { get; } = modifierKeys;
}

/// <summary>制御点のマウスホイール情報 (スタブ)。</summary>
public class ControllerPointMouseWheelEventArgs(int delta, ModifierKeys modifierKeys)
{
    public int Delta { get; } = delta;

    public ModifierKeys ModifierKeys { get; } = modifierKeys;
}

/// <summary>制御点の形状 (スタブ)。</summary>
public enum VideoControllerPointShape
{
    Default,
    Square,
    Circle,
}

/// <summary>プレビューエリア上でドラッグできる制御点 (スタブ)。</summary>
public class ControllerPoint(Vector3 position, Action<ControlPointDragEventArgs>? onDrag = null)
{
    public Vector3 Position { get; set; } = position;

    public bool IsSelected { get; set; }

    public VideoControllerPointShape Shape { get; set; } = VideoControllerPointShape.Default;

    public Action<ControlPointDragEventArgs>? OnDrag { get; set; } = onDrag;

    public Action<ControlPointDragEventArgs>? OnDragStart { get; set; }

    public Action<ControlPointDragEventArgs>? OnDragEnd { get; set; }

    public Action<ControllerPointMouseWheelEventArgs>? OnMouseWheel { get; set; }

    public Vector3 GetWorldPosition(DrawDescription desc) => Position;
}

/// <summary>制御点同士の接続方法 (スタブ)。</summary>
public enum VideoControllerPointConnection
{
    None,
    Line,
    Loop,
}

/// <summary>プレビューエリアに表示する制御点の集合 (スタブ)。</summary>
public class VideoController(IEnumerable<ControllerPoint> points)
{
    public IEnumerable<ControllerPoint> Points { get; } = points;

    public VideoControllerPointConnection Connection { get; set; } = VideoControllerPointConnection.Line;
}

/// <summary>図形の描画を担当するソース (スタブ)。</summary>
public interface IShapeSource : IDisposable
{
    /// <summary>描画結果。</summary>
    ID2D1Image Output { get; }

    /// <summary>指定フレームの図形を更新する。</summary>
    void Update(TimelineItemSourceDescription timelineItemSourceDescription);
}

/// <summary>プレビューエリアの制御点に対応した図形ソース (スタブ)。</summary>
public interface IShapeSource2 : IShapeSource
{
    /// <summary>プレビューエリアに表示する制御点。</summary>
    IEnumerable<VideoController> Controllers { get; }
}
