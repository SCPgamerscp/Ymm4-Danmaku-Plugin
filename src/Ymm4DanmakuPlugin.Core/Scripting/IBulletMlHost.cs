using Ymm4DanmakuPlugin.Core.Mathematics;

namespace Ymm4DanmakuPlugin.Core.Scripting;

/// <summary>
/// BulletML の実行主体 (エミッター本体、または個々の弾) がエンジンへ働きかけるための窓口。
/// <para>速度は BulletML 単位 (1 単位 ≒ 1px/フレーム) で扱う。</para>
/// </summary>
public interface IBulletMlHost
{
    /// <summary>実行主体の現在位置。</summary>
    Vec2 SelfPosition { get; }

    /// <summary>実行主体の進行方向 (エンジン角度、度、0 = 右)。</summary>
    double SelfDirection { get; set; }

    /// <summary>実行主体の速度 (BulletML 単位)。</summary>
    double SelfSpeed { get; set; }

    /// <summary>ターゲット (自機) の位置。</summary>
    Vec2 TargetPosition { get; }

    /// <summary>難易度 (0〜1)。$rank に対応。</summary>
    double Rank { get; }

    DeterministicRandom Random { get; }

    /// <summary>弾を発射する。</summary>
    /// <param name="direction">エンジン角度 (度)。</param>
    /// <param name="speed">BulletML 単位の速度。</param>
    /// <param name="definition">弾の定義 (見た目などの拡張属性の参照用)。</param>
    /// <param name="runner">この弾に紐付ける BulletML ランナー (null 可)。</param>
    void Fire(double direction, double speed, BulletMlBullet? definition, BulletMlRunner? runner);

    /// <summary>実行主体を消滅させる。</summary>
    void Vanish();

    /// <summary>速度ベクトルへ増分を加える (accel 用)。単位は BulletML 単位/フレーム。</summary>
    void ApplyVelocityDelta(double deltaVx, double deltaVy);

    /// <summary>軌道変化イベントを通知する (変化音の発火に使う)。</summary>
    void NotifyChange();
}
