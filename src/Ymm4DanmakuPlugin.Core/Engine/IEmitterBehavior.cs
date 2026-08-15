using Ymm4DanmakuPlugin.Core.Audio;
using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Mathematics;
using Ymm4DanmakuPlugin.Core.Model;

namespace Ymm4DanmakuPlugin.Core.Engine;

/// <summary>
/// エミッターの挙動 (いつ・どこへ・何発撃つか) を決める戦略インターフェース。
/// パターン生成・JSON・BulletML・Lua はすべてこの形に落とし込まれる。
/// </summary>
public interface IEmitterBehavior
{
    /// <summary>内部状態を初期化する (タイムラインの先頭へシークしたときに呼ばれる)。</summary>
    void Reset();

    /// <summary>1 ステップ進める。</summary>
    /// <param name="context">発射コンテキスト。</param>
    /// <param name="deltaTime">経過時間 (秒)。</param>
    void Update(EmitterContext context, double deltaTime);
}

/// <summary>エミッターがエンジンへアクセスするためのコンテキスト。</summary>
public sealed class EmitterContext(DanmakuEngine engine, int emitterIndex)
{
    public DanmakuEngine Engine { get; } = engine;

    public int EmitterIndex { get; } = emitterIndex;

    /// <summary>このエミッターの設定。</summary>
    public EmitterSettings Settings => Engine.Settings.Emitters[EmitterIndex];

    /// <summary>シミュレーション開始からの経過秒数。</summary>
    public double Time => Engine.CurrentTime;

    /// <summary>エミッターの現在位置 (公転を反映済み)。</summary>
    public Vec2 Position { get; internal set; }

    /// <summary>ターゲット (自機) の位置。</summary>
    public Vec2 TargetPosition => Engine.TargetPosition;

    public DeterministicRandom Random => Engine.Random;

    public SoundEventLog Sounds => Engine.SoundLog;

    /// <summary>弾を 1 発生成する。</summary>
    public Bullet? Spawn(in BulletSpawnRequest request) => Engine.Spawn(in request);

    /// <summary>ターゲットへの角度 (度) を返す。</summary>
    public double AngleToTarget() => (TargetPosition - Position).Degrees;

    /// <summary>動的な発射基準角度 (度) を引く。未設定なら null。</summary>
    public double? EmitterAngle(double time) => Engine.Live.EmitterAngle?.Invoke(EmitterIndex, time);
}
