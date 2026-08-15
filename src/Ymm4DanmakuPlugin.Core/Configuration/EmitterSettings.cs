using Ymm4DanmakuPlugin.Core.Model;

namespace Ymm4DanmakuPlugin.Core.Configuration;

/// <summary>1 つのエミッター (発射源) の設定。マルチエミッターに対応するため配列で保持する。</summary>
public sealed record EmitterSettings
{
    public string Name { get; init; } = "エミッター";

    public bool IsEnabled { get; init; } = true;

    /// <summary>発射位置 X (キャンバス中心を原点とする px)。</summary>
    public double X { get; init; }

    /// <summary>発射位置 Y (キャンバス中心を原点とする px、下が正)。</summary>
    public double Y { get; init; } = -200;

    /// <summary>エミッター自体の公転半径 (px)。0 で静止。</summary>
    public double OrbitRadius { get; init; }

    /// <summary>エミッター自体の公転速度 (度/秒)。</summary>
    public double OrbitSpeed { get; init; }

    /// <summary>公転の初期位相 (度)。</summary>
    public double OrbitPhase { get; init; }

    /// <summary>弾幕定義の供給元。</summary>
    public DanmakuSourceMode SourceMode { get; init; } = DanmakuSourceMode.Pattern;

    /// <summary>外部データのファイルパス (SourceMode が Pattern 以外のとき使用)。</summary>
    public string? SourcePath { get; init; }

    /// <summary>外部データをインラインで持つ場合の本文。SourcePath より優先される。</summary>
    public string? SourceText { get; init; }

    public PatternSettings Pattern { get; init; } = new();

    public BulletPhysics Physics { get; init; } = new();

    public BulletAppearance Appearance { get; init; } = new();

    /// <summary>分裂 (多段弾幕) 設定。null で分裂しない。</summary>
    public SplitSpec? Split { get; init; }

    /// <summary>分裂までの時間 (秒)。</summary>
    public double SplitDelay { get; init; } = 0.6;

    /// <summary>このエミッター固有のシードオフセット。</summary>
    public int SeedOffset { get; init; }

    /// <summary>BulletML の速度 1 単位に対応する px/秒。1px/フレーム(60fps) = 60 が既定。</summary>
    public double ScriptSpeedScale { get; init; } = 60;

    /// <summary>BulletML の $rank に渡す難易度 (0〜1)。</summary>
    public double ScriptRank { get; init; } = 0.5;

    /// <summary>スクリプト (BulletML / Lua) をループ再生するか。</summary>
    public bool ScriptLoop { get; init; } = true;

    /// <summary>カスタム画像ファイルパス。</summary>
    public string? ImagePath { get; init; }
}
