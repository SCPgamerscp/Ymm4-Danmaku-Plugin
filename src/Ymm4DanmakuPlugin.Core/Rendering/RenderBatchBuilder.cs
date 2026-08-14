using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Model;

namespace Ymm4DanmakuPlugin.Core.Rendering;

/// <summary>
/// 弾のリストから描画用インスタンス配列とバッチ情報を組み立てる。
/// <para>
/// - スプライト番号と合成モードでソートし、描画呼び出し回数 (ドローコール) を最小化する<br/>
/// - 配列は使い回し、毎フレームのメモリ確保をゼロにする<br/>
/// - トレイルは弾本体より先に描画されるよう、同一バッチ内で先に詰める
/// </para>
/// </summary>
public sealed class RenderBatchBuilder
{
    private BulletInstance[] instances = new BulletInstance[1024];
    private int[] sortKeys = new int[1024];
    private int[] order = new int[1024];
    private readonly List<DanmakuRenderBatch> batches = [];

    /// <summary>組み立て済みインスタンス (0 〜 <see cref="Count"/> の範囲が有効)。</summary>
    public BulletInstance[] Instances => instances;

    /// <summary>有効なインスタンス数。</summary>
    public int Count { get; private set; }

    /// <summary>描画バッチ。</summary>
    public IReadOnlyList<DanmakuRenderBatch> Batches => batches;

    /// <summary>スプライトの種類数の上限 (ソートキー計算に使用)。</summary>
    public int MaxSpriteSlots { get; init; } = 64;

    /// <summary>弾のリストから描画データを構築する。</summary>
    /// <param name="bullets">生存中の弾。</param>
    /// <param name="appearanceProvider">エミッター番号から見た目設定を引く関数 (トレイル設定の参照に使用)。</param>
    /// <param name="globalOpacity">全体の不透明度 (0〜1)。</param>
    public void Build(
        IReadOnlyList<Bullet> bullets,
        Func<int, BulletAppearance> appearanceProvider,
        double globalOpacity = 1.0)
    {
        Count = 0;
        batches.Clear();

        var required = 0;
        for (var i = 0; i < bullets.Count; i++)
        {
            var bullet = bullets[i];
            if (!bullet.IsAlive) continue;
            required += 1 + bullet.TrailCount;
        }

        EnsureCapacity(required);

        var opacity = (float)Math.Clamp(globalOpacity, 0.0, 1.0);

        for (var i = 0; i < bullets.Count; i++)
        {
            var bullet = bullets[i];
            if (!bullet.IsAlive) continue;

            var appearance = appearanceProvider(bullet.EmitterIndex);
            var alpha = bullet.Color.A * bullet.OpacityFactor * opacity;
            if (alpha <= 0.001f) continue;

            // --- トレイル (古い順に描く) ---
            if (bullet.TrailCount > 0)
            {
                var trailFade = (float)Math.Clamp(appearance.TrailFade, 0.0, 1.0);
                var trailScale = (float)Math.Max(0.0, appearance.TrailScale);

                for (var t = bullet.TrailCount - 1; t >= 0; t--)
                {
                    var ratio = 1f - (float)t / bullet.TrailCount;
                    var position = bullet.GetTrailPosition(t);

                    AddInstance(new BulletInstance
                    {
                        X = (float)position.X,
                        Y = (float)position.Y,
                        Rotation = (float)bullet.Rotation,
                        Scale = (float)bullet.Scale * Lerp(trailScale, 1f, ratio),
                        R = bullet.Color.R,
                        G = bullet.Color.G,
                        B = bullet.Color.B,
                        A = alpha * Lerp(trailFade, 1f, ratio) * 0.75f,
                        SpriteIndex = bullet.SpriteIndex,
                        AnimationFrame = bullet.AnimationFrame,
                        Additive = bullet.Additive,
                        IsTrail = true,
                    });
                }
            }

            // --- 弾本体 ---
            var rotation = bullet.AlignToDirection
                ? (float)(bullet.Direction + bullet.Rotation)
                : (float)bullet.Rotation;

            AddInstance(new BulletInstance
            {
                X = (float)bullet.Position.X,
                Y = (float)bullet.Position.Y,
                Rotation = rotation,
                Scale = (float)bullet.Scale,
                R = bullet.Color.R,
                G = bullet.Color.G,
                B = bullet.Color.B,
                A = alpha,
                SpriteIndex = bullet.SpriteIndex,
                AnimationFrame = bullet.AnimationFrame,
                Additive = bullet.Additive,
                IsTrail = false,
            });
        }

        SortAndBuildBatches();
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private void AddInstance(in BulletInstance instance)
    {
        if (Count >= instances.Length) EnsureCapacity(Count + 1);
        instances[Count] = instance;

        // ソートキー: [加算合成] [スプライト番号] [トレイルか否か]
        var sprite = Math.Clamp(instance.SpriteIndex, 0, MaxSpriteSlots - 1);
        sortKeys[Count] = ((instance.Additive ? 1 : 0) << 20) | (sprite << 4) | (instance.IsTrail ? 0 : 1);
        Count++;
    }

    private void SortAndBuildBatches()
    {
        if (Count == 0) return;

        for (var i = 0; i < Count; i++) order[i] = i;

        var keys = sortKeys;
        Array.Sort(keys, order, 0, Count);

        // order に従って並び替える (一時バッファを使わずスワップ列で処理)
        var sorted = new BulletInstance[Count];
        for (var i = 0; i < Count; i++) sorted[i] = instances[order[i]];
        Array.Copy(sorted, instances, Count);

        // 連続する同一 (スプライト, 合成モード) をバッチにまとめる
        var batchStart = 0;
        var currentSprite = instances[0].SpriteIndex;
        var currentAdditive = instances[0].Additive;

        for (var i = 1; i < Count; i++)
        {
            var instance = instances[i];
            if (instance.SpriteIndex == currentSprite && instance.Additive == currentAdditive) continue;

            batches.Add(new DanmakuRenderBatch(currentSprite, currentAdditive, batchStart, i - batchStart));
            batchStart = i;
            currentSprite = instance.SpriteIndex;
            currentAdditive = instance.Additive;
        }

        batches.Add(new DanmakuRenderBatch(currentSprite, currentAdditive, batchStart, Count - batchStart));
    }

    private void EnsureCapacity(int required)
    {
        if (required <= instances.Length) return;

        var capacity = instances.Length;
        while (capacity < required) capacity *= 2;

        Array.Resize(ref instances, capacity);
        sortKeys = new int[capacity];
        order = new int[capacity];
    }
}
