using Ymm4DanmakuPlugin.Core.Model;

namespace Ymm4DanmakuPlugin.Core.Configuration;

/// <summary>
/// ボス体力バー (HP ゲージ) の描画・挙動設定。
/// </summary>
public sealed record BossHpBarSettings
{
    /// <summary>体力バーを表示するかどうか。</summary>
    public bool Enabled { get; init; }

    /// <summary>体力バーの表示形式 (円形 / 画面上部 / 頭上追従 / 両方)。</summary>
    public HpBarStyle Style { get; init; } = HpBarStyle.CircularRing;

    /// <summary>ボスの最大 HP 実数値。</summary>
    public double MaxHp { get; init; } = 1000;

    /// <summary>手動 / 初期 HP パーセンテージ (0〜100%)。タイムラインでアニメーション可能。</summary>
    public double InitialHpPercentage { get; init; } = 100.0;

    /// <summary>自機ショット 1 発あたりの被弾ダメージ実数値。</summary>
    public double DamagePerHit { get; init; } = 15.0;

    /// <summary>円形ゲージの半径 (px)。</summary>
    public double Radius { get; init; } = 140.0;

    /// <summary>横長バーの幅 (px)。</summary>
    public double Width { get; init; } = 800.0;

    /// <summary>横長バーの高さ (px)。</summary>
    public double Height { get; init; } = 16.0;

    /// <summary>横長バーの X 座標 (px)。</summary>
    public double X { get; init; } = 0.0;

    /// <summary>横長バーの Y 座標 (px)。画面上部の場合は通常 -480 付近。</summary>
    public double Y { get; init; } = -480.0;

    /// <summary>ゲージ線の太さ (px)。</summary>
    public double Thickness { get; init; } = 6.0;

    /// <summary>通常時の HP バー色。</summary>
    public BulletColor BarColor { get; init; } = new(0.2f, 0.85f, 0.4f, 1.0f);

    /// <summary>ピンチ時 (HP < 25%) の警告色。</summary>
    public BulletColor DangerColor { get; init; } = new(0.95f, 0.2f, 0.2f, 1.0f);

    /// <summary>被弾追従ラグバーの色。</summary>
    public BulletColor DamageLagColor { get; init; } = new(1.0f, 0.9f, 0.3f, 0.9f);

    /// <summary>背景枠の色。</summary>
    public BulletColor BackgroundColor { get; init; } = new(0.1f, 0.1f, 0.15f, 0.7f);

    /// <summary>スペルカード (フェーズ) 区切り数 (1〜10)。</summary>
    public int PhaseCount { get; init; } = 3;

    /// <summary>発光 (グロー) 効果を適用するかどうか。</summary>
    public bool Glow { get; init; } = true;

    /// <summary>不透明度 (0〜100%)。</summary>
    public double Opacity { get; init; } = 100.0;
}
