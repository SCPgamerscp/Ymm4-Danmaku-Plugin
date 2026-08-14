using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;
using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Rendering;
using Ymm4DanmakuPlugin.Settings;

namespace Ymm4DanmakuPlugin.Parameters;

/// <summary>
/// 弾幕図形アイテムの設定全体。
/// <para>
/// YMM4 のタイムラインには「図形アイテム」として並び、
/// 拡大率・回転・不透明度・座標などは YMM4 本体の機能がそのまま使える。
/// </para>
/// <para>
/// <b>エミッターは複数持てる</b> (<see cref="Emitters"/>)。
/// 1 アイテムで「全方位弾 + 自機狙い弾」のような複合弾幕を組める。
/// </para>
/// <para>
/// <b>キーフレーム対応の方針</b>: <see cref="EmitterParameter.X"/> /
/// <see cref="EmitterParameter.Y"/> と <see cref="TargetX"/> / <see cref="TargetY"/> のみ
/// <see cref="Animation"/> とし、それ以外は静的な値とする。
/// これは設定署名 (シミュレーション再構築の判定) を安定させるための設計上の制約である。
/// </para>
/// </summary>
public class DanmakuShapeParameter : ShapeParameterBase
{
    // =====================================================================
    // 全体設定
    // =====================================================================

    [Display(GroupName = "全体", Name = "乱数シード", Description = "同じシードなら常に同じ弾幕になります。値を変えると弾のばらけ方が変わります。")]
    [TextBoxSlider("F0", "", 0, 100000)]
    [DefaultValue(20240101)]
    [Range(int.MinValue, int.MaxValue)]
    public int Seed { get => seed; set => Set(ref seed, value); }
    private int seed = 20240101;

    [Display(GroupName = "全体", Name = "最大弾数", Description = "同時に存在できる弾の上限。大きくすると重くなります。")]
    [TextBoxSlider("F0", "発", 256, 20000)]
    [DefaultValue(4096)]
    [Range(1, 200000)]
    public int MaxBullets { get => maxBullets; set => Set(ref maxBullets, value); }
    private int maxBullets = 4096;

    [Display(GroupName = "全体", Name = "再生速度", Description = "弾幕全体の時間倍率。0.5 でスローモーションになります。")]
    [TextBoxSlider("F2", "倍", 0.1, 3)]
    [DefaultValue(1d)]
    [Range(0.01, 100)]
    public double TimeScale { get => timeScale; set => Set(ref timeScale, value); }
    private double timeScale = 1.0;

    [Display(GroupName = "全体", Name = "計算の細かさ", Description = "物理計算 1 ステップの時間。小さいほど正確ですが重くなります。")]
    [EnumComboBox]
    public SimulationStep SimulationStep { get => simulationStep; set => Set(ref simulationStep, value); }
    private SimulationStep simulationStep = SimulationStep.Hz120;

    [Display(GroupName = "全体", Name = "画面外の扱い")]
    [EnumComboBox]
    public OutOfBoundsBehavior OutOfBounds { get => outOfBounds; set => Set(ref outOfBounds, value); }
    private OutOfBoundsBehavior outOfBounds = OutOfBoundsBehavior.Destroy;

    [Display(GroupName = "全体", Name = "画面外の余裕", Description = "画面の外側にこの距離ぶん余裕を持たせ、その外へ出た弾を処理します。")]
    [TextBoxSlider("F0", "px", 0, 1000)]
    [DefaultValue(160d)]
    [Range(0, 100000)]
    public double BoundsMargin { get => boundsMargin; set => Set(ref boundsMargin, value); }
    private double boundsMargin = 160;

    [Display(GroupName = "全体", Name = "全体の不透明度")]
    [TextBoxSlider("F2", "", 0, 1)]
    [DefaultValue(1d)]
    [Range(0, 1)]
    public double GlobalOpacity { get => globalOpacity; set => Set(ref globalOpacity, value); }
    private double globalOpacity = 1.0;

    [Display(GroupName = "全体", Name = "効果音チャンネル",
        Description = "「弾幕効果音」音声エフェクト側で同じ番号を指定すると、この弾幕に合わせて効果音が鳴ります。")]
    [TextBoxSlider("F0", "ch", 0, 15)]
    [DefaultValue(0)]
    [Range(0, 255)]
    public int Channel { get => channel; set => Set(ref channel, value); }
    private int channel;

    /// <summary>
    /// 直近の描画で使われたキャンバスサイズ。
    /// <para>
    /// 音声側 (<see cref="Audio.DanmakuChannelBus"/> 経由) から設定を再現するとき、
    /// 画面外判定の基準を映像側と揃えるために必要になる。
    /// </para>
    /// </summary>
    public int LastCanvasWidth { get; internal set; } = 1920;

    /// <inheritdoc cref="LastCanvasWidth"/>
    public int LastCanvasHeight { get; internal set; } = 1080;

    // =====================================================================
    // 当たり判定 / ターゲット (自機)
    // =====================================================================

    [Display(GroupName = "当たり判定", Name = "有効", Description = "ターゲット (自機) との当たり判定を行い、被弾エフェクトを出します。")]
    [ToggleSlider]
    public bool CollisionEnabled { get => collisionEnabled; set => Set(ref collisionEnabled, value); }
    private bool collisionEnabled;

    [Display(GroupName = "当たり判定", Name = "ターゲットX", Description = "自機の位置 X。キーフレームで動かせます。プレビュー上のドラッグでも変更できます。")]
    [AnimationSlider("F1", "px", -960, 960)]
    public Animation TargetX { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "当たり判定", Name = "ターゲットY", Description = "自機の位置 Y。キーフレームで動かせます。プレビュー上のドラッグでも変更できます。")]
    [AnimationSlider("F1", "px", -540, 540)]
    public Animation TargetY { get; } = new Animation(250, -100000, 100000);

    [Display(GroupName = "当たり判定", Name = "ターゲット半径")]
    [TextBoxSlider("F1", "px", 1, 100)]
    [DefaultValue(8d)]
    [Range(0.1, 100000)]
    public double TargetRadius { get => targetRadius; set => Set(ref targetRadius, value); }
    private double targetRadius = 8;

    [Display(GroupName = "当たり判定", Name = "被弾エフェクト", Description = "当たった瞬間に小さな飛沫を散らします。")]
    [ToggleSlider]
    public bool SpawnHitEffect { get => spawnHitEffect; set => Set(ref spawnHitEffect, value); }
    private bool spawnHitEffect = true;

    [Display(GroupName = "当たり判定", Name = "飛沫の数")]
    [TextBoxSlider("F0", "個", 1, 32)]
    [DefaultValue(8)]
    [Range(1, 500)]
    public int HitEffectCount { get => hitEffectCount; set => Set(ref hitEffectCount, value); }
    private int hitEffectCount = 8;

    [Display(GroupName = "当たり判定", Name = "飛沫の速度")]
    [TextBoxSlider("F0", "px/秒", 20, 600)]
    [DefaultValue(160d)]
    [Range(0, 100000)]
    public double HitEffectSpeed { get => hitEffectSpeed; set => Set(ref hitEffectSpeed, value); }
    private double hitEffectSpeed = 160;

    [Display(GroupName = "当たり判定", Name = "飛沫の寿命")]
    [TextBoxSlider("F2", "秒", 0.05, 2)]
    [DefaultValue(0.35d)]
    [Range(0.01, 1000)]
    public double HitEffectLifetime { get => hitEffectLifetime; set => Set(ref hitEffectLifetime, value); }
    private double hitEffectLifetime = 0.35;

    [Display(GroupName = "当たり判定", Name = "ターゲットを表示", Description = "確認用にターゲット位置へ枠を描きます。書き出し時も描かれるので注意してください。")]
    [ToggleSlider]
    public bool ShowTargetMarker { get => showTargetMarker; set => Set(ref showTargetMarker, value); }
    private bool showTargetMarker;

    // =====================================================================
    // 効果音
    // =====================================================================

    [Display(GroupName = "効果音", Name = "発射音", Description = "音声は「弾幕効果音」音声エフェクトを音声アイテムへ追加すると鳴ります。音声ファイルは YMM4 の設定画面で指定します。")]
    [ToggleSlider]
    public bool FireSoundEnabled { get => fireSoundEnabled; set => Set(ref fireSoundEnabled, value); }
    private bool fireSoundEnabled;

    [Display(GroupName = "効果音", Name = "変化音", Description = "分裂・軌道変化のときに鳴ります。")]
    [ToggleSlider]
    public bool ChangeSoundEnabled { get => changeSoundEnabled; set => Set(ref changeSoundEnabled, value); }
    private bool changeSoundEnabled;

    [Display(GroupName = "効果音", Name = "被弾音")]
    [ToggleSlider]
    public bool HitSoundEnabled { get => hitSoundEnabled; set => Set(ref hitSoundEnabled, value); }
    private bool hitSoundEnabled;

    [Display(GroupName = "効果音", Name = "消滅音")]
    [ToggleSlider]
    public bool VanishSoundEnabled { get => vanishSoundEnabled; set => Set(ref vanishSoundEnabled, value); }
    private bool vanishSoundEnabled;

    // =====================================================================
    // エミッター (マルチエミッター)
    // =====================================================================

    /// <summary>
    /// エミッター一覧。
    /// <para>
    /// <see cref="ImmutableList{T}"/> なのは YMM4 のアイテム編集 UI が
    /// 「差し替え = 変更通知」で扱えるようにするためである。
    /// 追加・削除は <see cref="AddEmitter"/> / <see cref="RemoveEmitter"/> を使う。
    /// </para>
    /// </summary>
    public ImmutableList<EmitterParameter> Emitters
    {
        get => emitters;
        set => Set(ref emitters, value.IsEmpty ? [new EmitterParameter()] : value);
    }
    private ImmutableList<EmitterParameter> emitters = [new EmitterParameter()];

    /// <summary>エミッターの上限。画像スロットの数 (<see cref="SpriteSlots"/>) と揃えている。</summary>
    public const int MaxEmitters = SpriteSlots.Capacity - SpriteSlots.CustomBase;

    public DanmakuShapeParameter() : this(null) { }

    public DanmakuShapeParameter(SharedDataStore? sharedData) : base(sharedData) { }

    /// <summary>エミッターを追加する。既存の最後のエミッターを複製する。</summary>
    public EmitterParameter? AddEmitter()
    {
        if (emitters.Count >= MaxEmitters) return null;

        var added = new EmitterParameter();
        if (emitters.Count > 0) emitters[^1].CopyTo(added);

        // 複製元と同名だと編集 UI で区別できないので連番を振り直す
        added.Name = $"エミッター{emitters.Count + 1}";

        Emitters = emitters.Add(added);
        return added;
    }

    /// <summary>エミッターを削除する。最後の 1 つは削除できない。</summary>
    public bool RemoveEmitter(EmitterParameter emitter)
    {
        if (emitters.Count <= 1) return false;
        if (!emitters.Contains(emitter)) return false;

        Emitters = emitters.Remove(emitter);
        return true;
    }

    // =====================================================================
    // コアエンジンへの変換
    // =====================================================================

    /// <summary>
    /// 現在の設定をコアエンジンの設定へ変換する。
    /// <para>
    /// キーフレームで動く値 (エミッター位置・ターゲット位置) は
    /// <b>ここには含めず</b>、<c>LiveValueSource</c> 経由で毎ステップ供給する。
    /// 含めてしまうと 1 フレーム進むたびに設定署名が変わり、
    /// シミュレーションが作り直されて極端に重くなる。
    /// </para>
    /// </summary>
    public DanmakuSettings ToSettings(int canvasWidth, int canvasHeight)
    {
        var sound = DanmakuSoundSettings.Default;

        var list = ImmutableArray.CreateBuilder<EmitterSettings>(emitters.Count);
        for (var i = 0; i < emitters.Count; i++)
            list.Add(emitters[i].ToSettings(i));

        return new DanmakuSettings
        {
            Seed = Seed,
            CanvasWidth = canvasWidth,
            CanvasHeight = canvasHeight,
            BoundsMargin = BoundsMargin,
            MaxBullets = MaxBullets,
            TimeScale = TimeScale,
            FixedTimeStep = SimulationStep.ToSeconds(),
            OutOfBounds = OutOfBounds,
            Emitters = list.ToImmutable(),

            Collision = new CollisionSettings
            {
                IsEnabled = CollisionEnabled,
                // ターゲット座標はキーフレームで動くため既定値のみ (実値は LiveValueSource 経由)
                TargetX = 0,
                TargetY = 0,
                TargetRadius = TargetRadius,
                SpawnHitEffect = SpawnHitEffect,
                HitEffectCount = HitEffectCount,
                HitEffectSpeed = HitEffectSpeed,
                HitEffectLifetime = HitEffectLifetime,
                HitEffectSpriteIndex = (int)BulletShape.Particle,
            },

            FireSound = sound.ToSoundSettings(DanmakuSoundKind.Fire, FireSoundEnabled),
            ChangeSound = sound.ToSoundSettings(DanmakuSoundKind.Change, ChangeSoundEnabled),
            HitSound = sound.ToSoundSettings(DanmakuSoundKind.Hit, HitSoundEnabled),
            VanishSound = sound.ToSoundSettings(DanmakuSoundKind.Vanish, VanishSoundEnabled),
        };
    }

    /// <summary>スプライト番号に対応する発光の強さを引く (描画側で使用)。</summary>
    public double GetGlowIntensity(int spriteIndex)
    {
        for (var i = 0; i < emitters.Count; i++)
        {
            var emitter = emitters[i];
            var slot = emitter.HasCustomImage ? SpriteSlots.CustomSlotOf(i) : (int)emitter.Shape;
            if (slot == spriteIndex) return emitter.GlowIntensity;
        }

        return 1.0;
    }

    // =====================================================================
    // ShapeParameterBase の実装
    // =====================================================================

    public override IShapeSource CreateShapeSource(IGraphicsDevicesAndContext devices) =>
        new DanmakuShapeSource(devices, this);

    /// <summary>
    /// exo (AviUtl) 出力には対応しない。
    /// 弾幕は YMM4 独自のシミュレーションであり AviUtl 側に等価な機能がないため、
    /// プラグイン側で <c>IsExoShapeSupported = false</c> を宣言している。
    /// </summary>
    public override IEnumerable<string> CreateShapeItemExoFilter(int keyFrameIndex, ExoOutputDescription desc) => [];

    /// <summary>exo のマスク出力にも対応しない。</summary>
    public override IEnumerable<string> CreateMaskExoFilter(
        int keyFrameIndex,
        ExoOutputDescription desc,
        ShapeMaskExoOutputDescription shapeMaskParameters) => [];

    protected override IEnumerable<IAnimatable> GetAnimatables()
    {
        yield return TargetX;
        yield return TargetY;
        foreach (var emitter in emitters) yield return emitter;
    }

    /// <summary>
    /// 図形の種類を切り替えて戻ってきたときに設定を復元する。
    /// <para>
    /// <see cref="SharedDataStore"/> には自前のスナップショット型を入れる。
    /// このインスタンス自身を入れると、後から編集した内容が
    /// 退避データにも反映されてしまう (参照が共有される) ため。
    /// </para>
    /// </summary>
    protected override void LoadSharedData(SharedDataStore store)
    {
        if (store.Load<SharedData>() is not { } data) return;
        data.CopyTo(this);
    }

    protected override void SaveSharedData(SharedDataStore store) => store.Save(new SharedData(this));

    /// <summary>図形の種類を切り替える間だけ設定を保持するスナップショット。</summary>
    private sealed class SharedData
    {
        private readonly int seed;
        private readonly int maxBullets;
        private readonly double timeScale;
        private readonly SimulationStep simulationStep;
        private readonly OutOfBoundsBehavior outOfBounds;
        private readonly double boundsMargin;
        private readonly double globalOpacity;
        private readonly int channel;

        private readonly bool collisionEnabled;
        private readonly Animation targetX = new(0, -100000, 100000);
        private readonly Animation targetY = new(250, -100000, 100000);
        private readonly double targetRadius;
        private readonly bool spawnHitEffect;
        private readonly int hitEffectCount;
        private readonly double hitEffectSpeed;
        private readonly double hitEffectLifetime;
        private readonly bool showTargetMarker;

        private readonly bool fireSoundEnabled;
        private readonly bool changeSoundEnabled;
        private readonly bool hitSoundEnabled;
        private readonly bool vanishSoundEnabled;

        private readonly ImmutableList<EmitterParameter> emitters;

        public SharedData(DanmakuShapeParameter source)
        {
            seed = source.Seed;
            maxBullets = source.MaxBullets;
            timeScale = source.TimeScale;
            simulationStep = source.SimulationStep;
            outOfBounds = source.OutOfBounds;
            boundsMargin = source.BoundsMargin;
            globalOpacity = source.GlobalOpacity;
            channel = source.Channel;

            collisionEnabled = source.CollisionEnabled;
            targetX.CopyFrom(source.TargetX);
            targetY.CopyFrom(source.TargetY);
            targetRadius = source.TargetRadius;
            spawnHitEffect = source.SpawnHitEffect;
            hitEffectCount = source.HitEffectCount;
            hitEffectSpeed = source.HitEffectSpeed;
            hitEffectLifetime = source.HitEffectLifetime;
            showTargetMarker = source.ShowTargetMarker;

            fireSoundEnabled = source.FireSoundEnabled;
            changeSoundEnabled = source.ChangeSoundEnabled;
            hitSoundEnabled = source.HitSoundEnabled;
            vanishSoundEnabled = source.VanishSoundEnabled;

            // エミッターは複製して退避する (参照を共有しない)
            var builder = ImmutableList.CreateBuilder<EmitterParameter>();
            foreach (var emitter in source.Emitters)
            {
                var copy = new EmitterParameter();
                emitter.CopyTo(copy);
                builder.Add(copy);
            }
            emitters = builder.ToImmutable();
        }

        public void CopyTo(DanmakuShapeParameter target)
        {
            target.Seed = seed;
            target.MaxBullets = maxBullets;
            target.TimeScale = timeScale;
            target.SimulationStep = simulationStep;
            target.OutOfBounds = outOfBounds;
            target.BoundsMargin = boundsMargin;
            target.GlobalOpacity = globalOpacity;
            target.Channel = channel;

            target.CollisionEnabled = collisionEnabled;
            target.TargetX.CopyFrom(targetX);
            target.TargetY.CopyFrom(targetY);
            target.TargetRadius = targetRadius;
            target.SpawnHitEffect = spawnHitEffect;
            target.HitEffectCount = hitEffectCount;
            target.HitEffectSpeed = hitEffectSpeed;
            target.HitEffectLifetime = hitEffectLifetime;
            target.ShowTargetMarker = showTargetMarker;

            target.FireSoundEnabled = fireSoundEnabled;
            target.ChangeSoundEnabled = changeSoundEnabled;
            target.HitSoundEnabled = hitSoundEnabled;
            target.VanishSoundEnabled = vanishSoundEnabled;

            var builder = ImmutableList.CreateBuilder<EmitterParameter>();
            foreach (var emitter in emitters)
            {
                var copy = new EmitterParameter();
                emitter.CopyTo(copy);
                builder.Add(copy);
            }
            target.Emitters = builder.ToImmutable();
        }
    }
}

/// <summary>物理計算の刻み幅。</summary>
public enum SimulationStep
{
    [Display(Name = "60Hz (軽い)", Description = "1/60 秒ごとに計算します。最も軽い設定です。")]
    Hz60 = 0,

    [Display(Name = "120Hz (標準)", Description = "1/120 秒ごとに計算します。高速な弾でも軌道が破綻しません。")]
    Hz120 = 1,

    [Display(Name = "240Hz (高精度)", Description = "1/240 秒ごとに計算します。重いですが最も滑らかです。")]
    Hz240 = 2,
}

/// <summary><see cref="SimulationStep"/> の拡張。</summary>
public static class SimulationStepExtensions
{
    /// <summary>刻み幅を秒に変換する。</summary>
    public static double ToSeconds(this SimulationStep step) => step switch
    {
        SimulationStep.Hz60 => 1.0 / 60.0,
        SimulationStep.Hz240 => 1.0 / 240.0,
        _ => 1.0 / 120.0,
    };
}
