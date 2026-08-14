using Ymm4DanmakuPlugin.Core.Model;

namespace Ymm4DanmakuPlugin.Core.Configuration;

/// <summary>弾の見た目に関する設定。</summary>
public sealed record BulletAppearance
{
    /// <summary>使用するスプライトスロット番号。</summary>
    public int SpriteIndex { get; init; }

    /// <summary>複数スプライトを循環使用する場合の使用枚数 (1 で固定)。</summary>
    public int SpriteCycleCount { get; init; } = 1;

    /// <summary>基準スケール。</summary>
    public double Scale { get; init; } = 1.0;

    /// <summary>スケールのランダム幅 (±)。</summary>
    public double ScaleJitter { get; init; }

    /// <summary>1 秒あたりのスケール変化量。</summary>
    public double ScaleVelocity { get; init; }

    /// <summary>初期回転角 (度)。</summary>
    public double Rotation { get; init; }

    /// <summary>回転速度 (度/秒)。</summary>
    public double RotationVelocity { get; init; }

    /// <summary>進行方向へ画像を向ける。</summary>
    public bool AlignToDirection { get; init; } = true;

    public ColorMode ColorMode { get; init; } = ColorMode.Single;

    public BulletColor PrimaryColor { get; init; } = BulletColor.White;

    public BulletColor SecondaryColor { get; init; } = new(0.4f, 0.7f, 1f, 1f);

    /// <summary>Rainbow モード時の色相変化速度 (度/秒)。</summary>
    public double HueVelocity { get; init; } = 120;

    /// <summary>Rainbow / Palette モード時の弾ごとの色相オフセット (度)。</summary>
    public double HueStep { get; init; } = 15;

    /// <summary>Gradient モードで 2 色を何段階に分けて補間するか。</summary>
    public int ColorGradientSteps { get; init; } = 16;

    /// <summary>加算合成 (発光) を行う。</summary>
    public bool Additive { get; init; } = true;

    /// <summary>不透明度 (0〜1)。</summary>
    public double Opacity { get; init; } = 1.0;

    /// <summary>発生時のフェードイン時間 (秒)。</summary>
    public double FadeInDuration { get; init; } = 0.05;

    /// <summary>消滅前のフェードアウト時間 (秒)。</summary>
    public double FadeOutDuration { get; init; } = 0.15;

    /// <summary>スプライトアニメーション速度 (コマ/秒)。0 で静止。</summary>
    public double AnimationFps { get; init; }

    /// <summary>トレイル (残像) の描画数。0 で無効。</summary>
    public int TrailLength { get; init; }

    /// <summary>トレイル記録間隔 (秒)。</summary>
    public double TrailInterval { get; init; } = 1.0 / 60.0;

    /// <summary>トレイル末端の不透明度倍率。</summary>
    public double TrailFade { get; init; } = 0.0;

    /// <summary>トレイル末端のスケール倍率。</summary>
    public double TrailScale { get; init; } = 0.6;

    /// <summary>グロー (発光) の強さ。描画側で加算描画を重ねる回数に使用。</summary>
    public double GlowIntensity { get; init; } = 1.0;
}
