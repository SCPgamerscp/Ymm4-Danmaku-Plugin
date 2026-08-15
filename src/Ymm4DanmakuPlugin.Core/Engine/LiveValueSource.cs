using Ymm4DanmakuPlugin.Core.Mathematics;

namespace Ymm4DanmakuPlugin.Core.Engine;

/// <summary>
/// キーフレーム (YMM4 の <c>Animation</c>) によって時間変化する値をエンジンへ供給する差し込み口。
/// <para>
/// エミッター位置やターゲット位置をタイムラインでアニメーションさせたい場合、
/// これらの値は「設定 (record)」ではなく「時刻の関数」として扱う必要がある。
/// 設定に埋め込んでしまうと 1 フレーム動かすたびに設定署名が変わり、
/// <see cref="DanmakuSimulator"/> がシミュレーションを作り直して極端に重くなる。
/// </para>
/// <para>
/// <b>決定論の担保:</b> ここに渡す関数は必ず「時刻のみに依存する純粋関数」でなければならない。
/// キーフレーム曲線の評価はまさにその条件を満たすため、
/// どのフレームへシークしても同一の弾幕が再現される性質は保たれる。
/// 乱数や前回値に依存する関数を渡してはならない。
/// </para>
/// </summary>
public sealed class LiveValueSource
{
    /// <summary>
    /// エミッター位置を供給する関数。引数は (エミッター番号, 時刻秒)。
    /// null を返した場合は設定値 (<c>EmitterSettings.X/Y</c>) が使われる。
    /// </summary>
    public Func<int, double, Vec2?>? EmitterPosition { get; set; }

    /// <summary>
    /// ターゲット (自機) 位置を供給する関数。引数は時刻秒。
    /// null を返した場合は設定値 (<c>CollisionSettings.TargetX/Y</c>) が使われる。
    /// </summary>
    public Func<double, Vec2?>? TargetPosition { get; set; }

    /// <summary>
    /// エミッターの発射基準角度 (度) を供給する関数。引数は (エミッター番号, 時刻秒)。
    /// null を返した場合は設定値 (<c>PatternSettings.BaseAngle</c>) が使われる。
    /// </summary>
    public Func<int, double, double?>? EmitterAngle { get; set; }

    /// <summary>いずれかの供給関数が設定されているかどうか。</summary>
    public bool HasAny => EmitterPosition is not null || TargetPosition is not null || EmitterAngle is not null;

    /// <summary>すべての供給関数を解除する。</summary>
    public void Clear()
    {
        EmitterPosition = null;
        TargetPosition = null;
        EmitterAngle = null;
    }
}
