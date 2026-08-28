namespace Ymm4DanmakuPlugin.Core.Model;

/// <summary>
/// 弾のオブジェクトプール。
/// <para>
/// 弾幕は 1 フレームで数千発が生成/消滅するため、GC 負荷を避ける目的で
/// <see cref="Bullet"/> インスタンスを使い回す。生存中の弾は密な配列
/// (<see cref="ActiveBullets"/>) に詰めて保持し、描画時のキャッシュ効率を確保する。
/// </para>
/// </summary>
public sealed class BulletPool
{
    private readonly List<Bullet> storage;
    private readonly Stack<Bullet> free;
    private readonly List<Bullet> active;
    private long nextId = 1;

    /// <summary>プールが確保する弾の最大数。</summary>
    public int Capacity { get; private set; }

    /// <summary>生存中の弾。</summary>
    public IReadOnlyList<Bullet> ActiveBullets => active;

    /// <summary>生存中の弾数。</summary>
    public int ActiveCount => active.Count;

    /// <summary>これまでに確保された弾インスタンス数。</summary>
    public int AllocatedCount => storage.Count;

    /// <summary>容量不足で生成を却下した回数。</summary>
    public int RejectedCount { get; private set; }

    public BulletPool(int capacity = 100000)
    {
        Capacity = Math.Max(1, capacity);
        storage = new List<Bullet>(Math.Min(Capacity, 4096));
        free = new Stack<Bullet>(Math.Min(Capacity, 4096));
        active = new List<Bullet>(Math.Min(Capacity, 4096));
    }

    /// <summary>弾を 1 つ確保する。必要に応じて最大50万発まで動的に自動拡張する。</summary>
    public Bullet? Rent()
    {
        Bullet bullet;
        if (free.Count > 0)
        {
            bullet = free.Pop();
        }
        else if (storage.Count < Capacity)
        {
            bullet = new Bullet { PoolIndex = storage.Count };
            storage.Add(bullet);
        }
        else if (Capacity < 500000)
        {
            // 動的自動拡張 (最大50万発まで自動スケール)
            Capacity = Math.Min(500000, Math.Max(Capacity * 2, storage.Count + 4096));
            bullet = new Bullet { PoolIndex = storage.Count };
            storage.Add(bullet);
        }
        else
        {
            RejectedCount++;
            return null;
        }

        // Reset() は InActiveList をクリアしないが、意図を明示するため事前に退避する。
        var alreadyListed = bullet.InActiveList;
        bullet.Reset();
        bullet.Id = nextId++;
        bullet.IsAlive = true;

        // Return 直後 (Compact 未実行) に再確保された場合、まだ active に残っているので追加しない。
        if (!alreadyListed)
        {
            bullet.InActiveList = true;
            active.Add(bullet);
        }

        return bullet;
    }

    /// <summary>弾を返却する。</summary>
    public void Return(Bullet bullet)
    {
        if (!bullet.IsAlive) return;
        bullet.IsAlive = false;
        free.Push(bullet);
    }

    /// <summary>
    /// 死亡した弾を <see cref="ActiveBullets"/> から取り除く。
    /// swap-remove により O(n) で圧縮する。
    /// </summary>
    public void Compact()
    {
        var write = 0;
        for (var read = 0; read < active.Count; read++)
        {
            var bullet = active[read];
            if (bullet.IsAlive)
            {
                active[write++] = bullet;
            }
            else
            {
                bullet.InActiveList = false;
            }
        }

        if (write < active.Count)
            active.RemoveRange(write, active.Count - write);
    }

    /// <summary>すべての弾を消去して初期状態に戻す。</summary>
    public void Clear()
    {
        // Return 済みで Compact 前の弾は free に既に入っているため、
        // active を走査して push すると free に重複が生じる。
        // 確実性を優先し free を storage から作り直す。
        active.Clear();
        free.Clear();
        foreach (var bullet in storage)
        {
            bullet.IsAlive = false;
            bullet.InActiveList = false;
            free.Push(bullet);
        }

        RejectedCount = 0;
        nextId = 1;
    }
}
