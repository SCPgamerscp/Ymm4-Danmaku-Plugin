using System.Collections.Immutable;

namespace Ymm4DanmakuPlugin.Core.Configuration;

/// <summary>効果音 1 種類あたりの設定。YMM4 の設定画面からユーザーが自由に変更できる。</summary>
public sealed record SoundSettings
{
    public bool IsEnabled { get; init; } = true;

    /// <summary>音量 (0〜1)。</summary>
    public double Volume { get; init; } = 0.6;

    /// <summary>ピッチのランダム変調幅 (± 半音)。0 で完全に一定。</summary>
    public double PitchJitterSemitones { get; init; } = 0.0;

    /// <summary>基準ピッチ (半音単位のオフセット)。</summary>
    public double PitchSemitones { get; init; }

    /// <summary>1 秒あたりに再生できる最大発音数 (0 で無制限)。</summary>
    public int MaxVoicesPerSecond { get; init; } = 0;

    /// <summary>同一フレーム内で複数の発音が起きた際にまとめるかどうか。</summary>
    public bool CoalesceSimultaneous { get; init; } = true;
}

/// <summary>衝突判定 / ヒットエフェクトの設定。</summary>
public sealed record CollisionSettings
{
    public bool IsEnabled { get; init; }

    /// <summary>ターゲット (自機) の X 座標。</summary>
    public double TargetX { get; init; }

    /// <summary>ターゲットの Y 座標。</summary>
    public double TargetY { get; init; } = 250;

    /// <summary>ターゲットの当たり判定半径 (px)。</summary>
    public double TargetRadius { get; init; } = 8;

    /// <summary>エネミー (ボス) の X 座標。</summary>
    public double EnemyX { get; init; }

    /// <summary>エネミー (ボス) の Y 座標。</summary>
    public double EnemyY { get; init; }

    /// <summary>エネミー (ボス) の被弾判定半径 (px)。</summary>
    public double EnemyRadius { get; init; } = 32;

    /// <summary>エネミーへの被弾判定を有効にするかどうか。</summary>
    public bool EnemyHitEnabled { get; init; } = true;

    /// <summary>ヒット時にエフェクト (小さな飛沫弾) を出すかどうか。</summary>
    public bool SpawnHitEffect { get; init; } = true;

    /// <summary>ヒットエフェクトの粒子数。</summary>
    public int HitEffectCount { get; init; } = 8;

    /// <summary>ヒットエフェクトの速度 (px/秒)。</summary>
    public double HitEffectSpeed { get; init; } = 160;

    /// <summary>ヒットエフェクトの寿命 (秒)。</summary>
    public double HitEffectLifetime { get; init; } = 0.35;

    /// <summary>ヒットエフェクトに使うスプライト番号。</summary>
    public int HitEffectSpriteIndex { get; init; }
}

/// <summary>弾幕全体の設定。</summary>
public sealed record DanmakuSettings
{
    /// <summary>乱数シード。同じシードなら常に同じ弾幕になる。</summary>
    public int Seed { get; init; } = 20240101;

    /// <summary>キャンバス幅 (px)。</summary>
    public int CanvasWidth { get; init; } = 1920;

    /// <summary>キャンバス高さ (px)。</summary>
    public int CanvasHeight { get; init; } = 1080;

    /// <summary>画面外判定のマージン (px)。</summary>
    public double BoundsMargin { get; init; } = 1000;

    /// <summary>同時に存在できる弾の最大数。</summary>
    public int MaxBullets { get; init; } = 100000;

    /// <summary>シミュレーションの時間倍率。</summary>
    public double TimeScale { get; init; } = 1.0;

    /// <summary>物理計算の固定ステップ (秒)。フレームレートによらず結果を安定させる。</summary>
    public double FixedTimeStep { get; init; } = 1.0 / 120.0;

    /// <summary>画面外に出た弾の扱い。</summary>
    public OutOfBoundsBehavior OutOfBounds { get; init; } = OutOfBoundsBehavior.Destroy;

    /// <summary>エミッター一覧。</summary>
    public ImmutableArray<EmitterSettings> Emitters { get; init; } = [new EmitterSettings()];

    public CollisionSettings Collision { get; init; } = new();

    /// <summary>自機 (プレイヤー) の射撃設定。</summary>
    public PlayerShotSettings PlayerShot { get; init; } = new();

    public SoundSettings FireSound { get; init; } = new();

    public SoundSettings ChangeSound { get; init; } = new() { Volume = 0.5 };

    public SoundSettings HitSound { get; init; } = new() { Volume = 0.8 };

    public SoundSettings VanishSound { get; init; } = new() { Volume = 0.35 };

    public SoundSettings PlayerShotSound { get; init; } = new() { Volume = 0.5 };

    /// <summary>指定した種類の効果音設定を取得する。</summary>
    public SoundSettings GetSound(DanmakuSoundKind kind) => kind switch
    {
        DanmakuSoundKind.Fire => FireSound,
        DanmakuSoundKind.Change => ChangeSound,
        DanmakuSoundKind.Hit => HitSound,
        DanmakuSoundKind.PlayerShot => PlayerShotSound,
        _ => VanishSound,
    };
}
