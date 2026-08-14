using System.Runtime.InteropServices;

namespace Ymm4DanmakuPlugin.Core.Rendering;

/// <summary>
/// GPU へ渡す 1 スプライトぶんの描画情報。
/// <para>
/// 構造体をフラット配列で持つことで、描画側は
/// 「同じスプライト・同じ合成モード」のまとまりを 1 回のバッチで描ける。
/// </para>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct BulletInstance
{
    /// <summary>キャンバス中心を原点とする描画位置。</summary>
    public float X;
    public float Y;

    /// <summary>回転角 (度)。</summary>
    public float Rotation;

    /// <summary>スケール。</summary>
    public float Scale;

    /// <summary>色 (線形 RGBA、アルファ乗算済み)。</summary>
    public float R;
    public float G;
    public float B;
    public float A;

    /// <summary>スプライトスロット番号。</summary>
    public int SpriteIndex;

    /// <summary>スプライトシートのコマ番号。</summary>
    public int AnimationFrame;

    /// <summary>加算合成するかどうか。</summary>
    public bool Additive;

    /// <summary>トレイル (残像) の一部かどうか。</summary>
    public bool IsTrail;
}

/// <summary>同一スプライト・同一合成モードでまとめた描画バッチ。</summary>
public readonly record struct DanmakuRenderBatch(int SpriteIndex, bool Additive, int Offset, int Count);
