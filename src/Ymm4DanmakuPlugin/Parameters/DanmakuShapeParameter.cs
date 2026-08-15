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
/// <b>発射位置の移動</b>: プラグイン内の「発射位置X / Y」をキーフレームやプレビュー上のドラッグで
/// 動かすことで、すでに発射された弾は画面上に残り、発射源だけをスムーズに移動させることができる。
/// </para>
/// </summary>
public class DanmakuShapeParameter : ShapeParameterBase
{
    // =====================================================================
    // 発射位置 (エミッター)
    // =====================================================================

    [Display(GroupName = "発射位置", Name = "X", Description = "発射位置 X。キーフレームで動かせます。プレビュー上のドラッグでも変更できます。")]
    [AnimationSlider("F1", "px", -1920, 1920)]
    public Animation X => MainEmitter.X;

    [Display(GroupName = "発射位置", Name = "Y", Description = "発射位置 Y。キーフレームで動かせます。プレビュー上のドラッグでも変更できます。")]
    [AnimationSlider("F1", "px", -1080, 1080)]
    public Animation Y => MainEmitter.Y;

    [Display(GroupName = "発射位置", Name = "公転半径", Description = "エミッター自体を円運動させる半径。0 で静止します。")]
    [TextBoxSlider("F1", "px", 0, 600)]
    [DefaultValue(0d)]
    [Range(0, 100000)]
    public double OrbitRadius
    {
        get => MainEmitter.OrbitRadius;
        set { if (MainEmitter.OrbitRadius != value) { MainEmitter.OrbitRadius = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "発射位置", Name = "公転速度", Description = "エミッターの円運動の速度。")]
    [TextBoxSlider("F1", "度/秒", -360, 360)]
    [DefaultValue(0d)]
    [Range(-100000, 100000)]
    public double OrbitSpeed
    {
        get => MainEmitter.OrbitSpeed;
        set { if (MainEmitter.OrbitSpeed != value) { MainEmitter.OrbitSpeed = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "発射位置", Name = "公転位相", Description = "公転の初期角度。")]
    [TextBoxSlider("F1", "度", 0, 360)]
    [DefaultValue(0d)]
    [Range(-100000, 100000)]
    public double OrbitPhase
    {
        get => MainEmitter.OrbitPhase;
        set { if (MainEmitter.OrbitPhase != value) { MainEmitter.OrbitPhase = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "発射位置", Name = "シードずらし", Description = "乱数をずらして弾のばらけ方を変えます。")]
    [TextBoxSlider("F0", "", 0, 100)]
    [DefaultValue(0)]
    [Range(-1000000, 1000000)]
    public int SeedOffset
    {
        get => MainEmitter.SeedOffset;
        set { if (MainEmitter.SeedOffset != value) { MainEmitter.SeedOffset = value; OnPropertyChanged(); } }
    }

    // =====================================================================
    // プリセット
    // =====================================================================

    [Display(GroupName = "プリセット", Name = "プリセット選択", Description = "東方風のサンプルを選んで [適用]。保存・読み込み・書き出しもここから行えます。")]
    [Presets.PresetSelector]
    public string PresetName
    {
        get => MainEmitter.PresetName;
        set { if (MainEmitter.PresetName != value) { MainEmitter.PresetName = value; OnPropertyChanged(); } }
    }

    // =====================================================================
    // 発射パターン
    // =====================================================================

    [Display(GroupName = "発射パターン", Name = "パターン")]
    [EnumComboBox]
    public PatternKind PatternKind
    {
        get => MainEmitter.PatternKind;
        set { if (MainEmitter.PatternKind != value) { MainEmitter.PatternKind = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "発射パターン", Name = "ウェイ数 (方向数)", Description = "同時に放つ方向の数。全方位弾なら 16〜36 程度。")]
    [TextBoxSlider("F0", "方向", 1, 128)]
    [DefaultValue(16)]
    [Range(1, 1000)]
    public int Way
    {
        get => MainEmitter.Way;
        set { if (MainEmitter.Way != value) { MainEmitter.Way = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "発射パターン", Name = "段数 (連数)", Description = "1 方向あたりに連続して放つ弾数。")]
    [TextBoxSlider("F0", "段", 1, 32)]
    [DefaultValue(1)]
    [Range(1, 100)]
    public int Stack
    {
        get => MainEmitter.Stack;
        set { if (MainEmitter.Stack != value) { MainEmitter.Stack = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "発射パターン", Name = "段ごとの速度差")]
    [TextBoxSlider("F0", "px/秒", -200, 200)]
    [DefaultValue(0d)]
    [Range(-100000, 100000)]
    public double StackSpeedStep
    {
        get => MainEmitter.StackSpeedStep;
        set { if (MainEmitter.StackSpeedStep != value) { MainEmitter.StackSpeedStep = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "発射パターン", Name = "段ごとの角度差")]
    [TextBoxSlider("F1", "度", -30, 30)]
    [DefaultValue(0d)]
    [Range(-100000, 100000)]
    public double StackAngleStep
    {
        get => MainEmitter.StackAngleStep;
        set { if (MainEmitter.StackAngleStep != value) { MainEmitter.StackAngleStep = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "発射パターン", Name = "発射角度", Description = "発射の基準となる向き。キーフレームで自由に回転させられます。0 で下向き。")]
    [AnimationSlider("F1", "度", -360, 360)]
    public Animation BaseAngle => MainEmitter.BaseAngle;

    [Display(GroupName = "発射パターン", Name = "拡散角度", Description = "扇形や放射で弾を広げる範囲。360 で全方位。")]
    [TextBoxSlider("F1", "度", 0, 360)]
    [DefaultValue(360d)]
    [Range(0, 360)]
    public double SpreadAngle
    {
        get => MainEmitter.SpreadAngle;
        set { if (MainEmitter.SpreadAngle != value) { MainEmitter.SpreadAngle = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "発射パターン", Name = "回転速度 (発射毎)", Description = "1 回撃つごとに発射方向を回転させる角度。渦巻き弾幕を作れます。")]
    [TextBoxSlider("F1", "度/発", -180, 180)]
    [DefaultValue(7.5d)]
    [Range(-100000, 100000)]
    public double AngleStepPerShot
    {
        get => MainEmitter.AngleStepPerShot;
        set { if (MainEmitter.AngleStepPerShot != value) { MainEmitter.AngleStepPerShot = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "発射パターン", Name = "角度ゆらぎ")]
    [TextBoxSlider("F1", "度", 0, 90)]
    [DefaultValue(0d)]
    [Range(0, 180)]
    public double AngleJitter
    {
        get => MainEmitter.AngleJitter;
        set { if (MainEmitter.AngleJitter != value) { MainEmitter.AngleJitter = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "発射パターン", Name = "発射間隔", Description = "次の発射までの時間。0.1 で秒間 10 回。")]
    [TextBoxSlider("F2", "秒", 0.01, 10)]
    [DefaultValue(0.12d)]
    [Range(0.001, 1000)]
    public double FireInterval
    {
        get => MainEmitter.FireInterval;
        set { if (MainEmitter.FireInterval != value) { MainEmitter.FireInterval = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "発射パターン", Name = "バースト発射数")]
    [TextBoxSlider("F0", "回", 1, 100)]
    [DefaultValue(1)]
    [Range(1, 1000)]
    public int BurstCount
    {
        get => MainEmitter.BurstCount;
        set { if (MainEmitter.BurstCount != value) { MainEmitter.BurstCount = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "発射パターン", Name = "バースト内間隔")]
    [TextBoxSlider("F2", "秒", 0.01, 2)]
    [DefaultValue(0.05d)]
    [Range(0.001, 1000)]
    public double BurstInterval
    {
        get => MainEmitter.BurstInterval;
        set { if (MainEmitter.BurstInterval != value) { MainEmitter.BurstInterval = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "発射パターン", Name = "バースト後休止")]
    [TextBoxSlider("F2", "秒", 0, 10)]
    [DefaultValue(0d)]
    [Range(0, 1000)]
    public double BurstCooldown
    {
        get => MainEmitter.BurstCooldown;
        set { if (MainEmitter.BurstCooldown != value) { MainEmitter.BurstCooldown = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "発射パターン", Name = "生成半径", Description = "エミッター中心から離れた円周上から弾を発生させます。")]
    [TextBoxSlider("F1", "px", 0, 600)]
    [DefaultValue(0d)]
    [Range(0, 100000)]
    public double SpawnRadius
    {
        get => MainEmitter.SpawnRadius;
        set { if (MainEmitter.SpawnRadius != value) { MainEmitter.SpawnRadius = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "発射パターン", Name = "自機狙い (ターゲット追尾)", Description = "ターゲット (自機) の方向へ向けて発射します。")]
    [ToggleSlider]
    public bool AimAtTarget
    {
        get => MainEmitter.AimAtTarget;
        set { if (MainEmitter.AimAtTarget != value) { MainEmitter.AimAtTarget = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "発射パターン", Name = "開始時刻")]
    [TextBoxSlider("F2", "秒", 0, 600)]
    [DefaultValue(0d)]
    [Range(0, 100000)]
    public double StartTime
    {
        get => MainEmitter.StartTime;
        set { if (MainEmitter.StartTime != value) { MainEmitter.StartTime = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "発射パターン", Name = "終了時刻")]
    [TextBoxSlider("F2", "秒", 0, 600)]
    [DefaultValue(600d)]
    [Range(0, 100000)]
    public double EndTime
    {
        get => MainEmitter.EndTime;
        set { if (MainEmitter.EndTime != value) { MainEmitter.EndTime = value; OnPropertyChanged(); } }
    }

    // =====================================================================
    // 弾の見た目
    // =====================================================================

    [Display(GroupName = "弾の見た目", Name = "弾の形")]
    [EnumComboBox]
    public BulletShape Shape
    {
        get => MainEmitter.Shape;
        set { if (MainEmitter.Shape != value) { MainEmitter.Shape = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "弾の見た目", Name = "画像", Description = "指定すると「弾の形」の代わりにこの画像を使います。")]
    [FileSelector(YukkuriMovieMaker.Settings.FileGroupType.ImageItem)]
    public string ImagePath
    {
        get => MainEmitter.ImagePath;
        set { if (MainEmitter.ImagePath != value) { MainEmitter.ImagePath = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "弾の見た目", Name = "大きさ")]
    [TextBoxSlider("F2", "倍", 0.1, 10)]
    [DefaultValue(1d)]
    [Range(0.001, 1000)]
    public double Scale
    {
        get => MainEmitter.Scale;
        set { if (MainEmitter.Scale != value) { MainEmitter.Scale = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "弾の見た目", Name = "進行方向に向ける", Description = "弾が飛んでいく向きに合わせて弾の向き・画像を回転させます。")]
    [ToggleSlider]
    public bool AlignToDirection
    {
        get => MainEmitter.AlignToDirection;
        set { if (MainEmitter.AlignToDirection != value) { MainEmitter.AlignToDirection = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "弾の見た目", Name = "着色モード")]
    [EnumComboBox]
    public ColorMode ColorMode
    {
        get => MainEmitter.ColorMode;
        set { if (MainEmitter.ColorMode != value) { MainEmitter.ColorMode = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "弾の見た目", Name = "メイン色 / ティント色")]
    [ColorPicker]
    public System.Windows.Media.Color PrimaryColor
    {
        get => MainEmitter.PrimaryColor;
        set { if (MainEmitter.PrimaryColor != value) { MainEmitter.PrimaryColor = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "弾の見た目", Name = "サブ色 (グラデーション用)")]
    [ColorPicker]
    public System.Windows.Media.Color SecondaryColor
    {
        get => MainEmitter.SecondaryColor;
        set { if (MainEmitter.SecondaryColor != value) { MainEmitter.SecondaryColor = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "弾の見た目", Name = "加算合成 (発光)")]
    [ToggleSlider]
    public bool Additive
    {
        get => MainEmitter.Additive;
        set { if (MainEmitter.Additive != value) { MainEmitter.Additive = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "弾の見た目", Name = "グロー発光強度")]
    [TextBoxSlider("F2", "倍", 1, 5)]
    [DefaultValue(1.5d)]
    [Range(0, 100)]
    public double GlowIntensity
    {
        get => MainEmitter.GlowIntensity;
        set { if (MainEmitter.GlowIntensity != value) { MainEmitter.GlowIntensity = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "弾の見た目", Name = "弾の不透明度")]
    [TextBoxSlider("F2", "", 0, 1)]
    [DefaultValue(1d)]
    [Range(0, 1)]
    public double Opacity
    {
        get => MainEmitter.Opacity;
        set { if (MainEmitter.Opacity != value) { MainEmitter.Opacity = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "弾の見た目", Name = "残像の長さ")]
    [TextBoxSlider("F0", "個", 0, 30)]
    [DefaultValue(0)]
    [Range(0, 100)]
    public int TrailLength
    {
        get => MainEmitter.TrailLength;
        set { if (MainEmitter.TrailLength != value) { MainEmitter.TrailLength = value; OnPropertyChanged(); } }
    }

    // =====================================================================
    // 弾の物理
    // =====================================================================

    [Display(GroupName = "弾の物理", Name = "弾速")]
    [TextBoxSlider("F0", "px/秒", 0, 3000)]
    [DefaultValue(220d)]
    [Range(0, 100000)]
    public double Speed
    {
        get => MainEmitter.Speed;
        set { if (MainEmitter.Speed != value) { MainEmitter.Speed = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "弾の物理", Name = "加速度", Description = "1 秒あたりの速度変化。正で加速、負で減速。")]
    [TextBoxSlider("F0", "px/秒²", -2000, 2000)]
    [DefaultValue(0d)]
    [Range(-100000, 100000)]
    public double Acceleration
    {
        get => MainEmitter.Acceleration;
        set { if (MainEmitter.Acceleration != value) { MainEmitter.Acceleration = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "弾の物理", Name = "角速度 (カーブ)", Description = "弾の進行方向を曲げる速度。正で時計回り。")]
    [TextBoxSlider("F1", "度/秒", -360, 360)]
    [DefaultValue(0d)]
    [Range(-100000, 100000)]
    public double AngularVelocity
    {
        get => MainEmitter.AngularVelocity;
        set { if (MainEmitter.AngularVelocity != value) { MainEmitter.AngularVelocity = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "弾の物理", Name = "重力", Description = "下方向への引力。")]
    [TextBoxSlider("F0", "px/秒²", -2000, 2000)]
    [DefaultValue(0d)]
    [Range(-100000, 100000)]
    public double Gravity
    {
        get => MainEmitter.Gravity;
        set { if (MainEmitter.Gravity != value) { MainEmitter.Gravity = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "弾の物理", Name = "風 (横力)", Description = "右方向への力。")]
    [TextBoxSlider("F0", "px/秒²", -2000, 2000)]
    [DefaultValue(0d)]
    [Range(-100000, 100000)]
    public double Wind
    {
        get => MainEmitter.Wind;
        set { if (MainEmitter.Wind != value) { MainEmitter.Wind = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "弾の物理", Name = "弾の寿命")]
    [TextBoxSlider("F2", "秒", 0.1, 60)]
    [DefaultValue(8d)]
    [Range(0.01, 1000)]
    public double Lifetime
    {
        get => MainEmitter.Lifetime;
        set { if (MainEmitter.Lifetime != value) { MainEmitter.Lifetime = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "弾の物理", Name = "ホーミング (自機誘導)")]
    [ToggleSlider]
    public bool HomingEnabled
    {
        get => MainEmitter.HomingEnabled;
        set { if (MainEmitter.HomingEnabled != value) { MainEmitter.HomingEnabled = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "弾の物理", Name = "誘導旋回速度")]
    [TextBoxSlider("F1", "度/秒", 0, 720)]
    [DefaultValue(60d)]
    [Range(0, 100000)]
    public double HomingTurnRate
    {
        get => MainEmitter.HomingTurnRate;
        set { if (MainEmitter.HomingTurnRate != value) { MainEmitter.HomingTurnRate = value; OnPropertyChanged(); } }
    }

    // =====================================================================
    // 弾の分裂
    // =====================================================================

    [Display(GroupName = "弾の分裂", Name = "分裂有効")]
    [ToggleSlider]
    public bool SplitEnabled
    {
        get => MainEmitter.SplitEnabled;
        set { if (MainEmitter.SplitEnabled != value) { MainEmitter.SplitEnabled = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "弾の分裂", Name = "分裂数")]
    [TextBoxSlider("F0", "発", 2, 64)]
    [DefaultValue(6)]
    [Range(2, 256)]
    public int SplitCount
    {
        get => MainEmitter.SplitCount;
        set { if (MainEmitter.SplitCount != value) { MainEmitter.SplitCount = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "弾の分裂", Name = "分裂までの時間")]
    [TextBoxSlider("F2", "秒", 0.05, 10)]
    [DefaultValue(0.6d)]
    [Range(0.01, 1000)]
    public double SplitDelay
    {
        get => MainEmitter.SplitDelay;
        set { if (MainEmitter.SplitDelay != value) { MainEmitter.SplitDelay = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "弾の分裂", Name = "分裂拡散角度")]
    [TextBoxSlider("F1", "度", 0, 360)]
    [DefaultValue(360d)]
    [Range(0, 360)]
    public double SplitSpread
    {
        get => MainEmitter.SplitSpread;
        set { if (MainEmitter.SplitSpread != value) { MainEmitter.SplitSpread = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "弾の分裂", Name = "分裂後の速度")]
    [TextBoxSlider("F0", "px/秒", 10, 3000)]
    [DefaultValue(160d)]
    [Range(0, 100000)]
    public double SplitSpeed
    {
        get => MainEmitter.SplitSpeed;
        set { if (MainEmitter.SplitSpeed != value) { MainEmitter.SplitSpeed = value; OnPropertyChanged(); } }
    }

    // =====================================================================
    // 弾幕データ / スクリプト
    // =====================================================================

    [Display(GroupName = "弾幕データ", Name = "データ形式")]
    [EnumComboBox]
    public DanmakuSourceMode SourceMode
    {
        get => MainEmitter.SourceMode;
        set { if (MainEmitter.SourceMode != value) { MainEmitter.SourceMode = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "弾幕データ", Name = "スクリプトファイル")]
    [FileSelector(YukkuriMovieMaker.Settings.FileGroupType.None)]
    public string SourcePath
    {
        get => MainEmitter.SourcePath;
        set { if (MainEmitter.SourcePath != value) { MainEmitter.SourcePath = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "弾幕データ", Name = "スクリプト本文")]
    [TextEditor(AcceptsReturn = true, PropertyEditorSize = PropertyEditorSize.FullWidth)]
    public string SourceText
    {
        get => MainEmitter.SourceText;
        set { if (MainEmitter.SourceText != value) { MainEmitter.SourceText = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "弾幕データ", Name = "BulletML 速度スケール")]
    [TextBoxSlider("F0", "px/秒", 1, 240)]
    [DefaultValue(60d)]
    [Range(0.1, 10000)]
    public double ScriptSpeedScale
    {
        get => MainEmitter.ScriptSpeedScale;
        set { if (MainEmitter.ScriptSpeedScale != value) { MainEmitter.ScriptSpeedScale = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "弾幕データ", Name = "BulletML 難易度 ($rank)")]
    [TextBoxSlider("F2", "", 0, 1)]
    [DefaultValue(0.5d)]
    [Range(0, 1)]
    public double ScriptRank
    {
        get => MainEmitter.ScriptRank;
        set { if (MainEmitter.ScriptRank != value) { MainEmitter.ScriptRank = value; OnPropertyChanged(); } }
    }

    [Display(GroupName = "弾幕データ", Name = "スクリプトをループ再生")]
    [ToggleSlider]
    public bool ScriptLoop
    {
        get => MainEmitter.ScriptLoop;
        set { if (MainEmitter.ScriptLoop != value) { MainEmitter.ScriptLoop = value; OnPropertyChanged(); } }
    }

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

    /// <summary>直近の描画で使われたキャンバスサイズ。</summary>
    public int LastCanvasWidth { get; internal set; } = 1920;

    /// <inheritdoc cref="LastCanvasWidth"/>
    public int LastCanvasHeight { get; internal set; } = 1080;

    // =====================================================================
    // エミッター (マルチエミッター)
    // =====================================================================

    /// <summary>現在編集中のメインエミッター。</summary>
    public EmitterParameter MainEmitter => emitters.Count > 0 ? emitters[0] : (emitters = [new EmitterParameter()])[0];

    /// <summary>
    /// エミッター一覧。
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
    /// </summary>
    public DanmakuSettings ToSettings(int canvasWidth, int canvasHeight)
    {
        var sound = DanmakuSoundSettings.Default;

        var builder = ImmutableArray.CreateBuilder<EmitterSettings>(emitters.Count);
        for (var i = 0; i < emitters.Count; i++)
        {
            builder.Add(emitters[i].ToSettings(i));
        }

        return new DanmakuSettings
        {
            CanvasWidth = canvasWidth,
            CanvasHeight = canvasHeight,
            BoundsMargin = BoundsMargin,
            OutOfBounds = OutOfBounds,
            Seed = Seed,
            MaxBullets = MaxBullets,
            TimeScale = TimeScale,
            FixedTimeStep = SimulationStep.ToSeconds(),

            Collision = new CollisionSettings
            {
                IsEnabled = CollisionEnabled,
                TargetRadius = TargetRadius,
                SpawnHitEffect = SpawnHitEffect,
                HitEffectCount = HitEffectCount,
                HitEffectSpeed = HitEffectSpeed,
                HitEffectLifetime = HitEffectLifetime,
            },

            FireSound = sound.Fire.ToSoundSettings(FireSoundEnabled),
            ChangeSound = sound.Change.ToSoundSettings(ChangeSoundEnabled),
            HitSound = sound.Hit.ToSoundSettings(HitSoundEnabled),
            VanishSound = sound.Vanish.ToSoundSettings(VanishSoundEnabled),

            Emitters = builder.ToImmutable(),
        };
    }

    /// <summary>スプライト番号からグロー発光の強度を引く。</summary>
    public double GetGlowIntensity(int spriteIndex)
    {
        if (spriteIndex < SpriteSlots.CustomBase)
        {
            return emitters.Count > 0 ? emitters[0].GlowIntensity : 1.0;
        }

        var emitterIndex = SpriteSlots.EmitterIndexOf(spriteIndex);
        if (emitterIndex >= 0 && emitterIndex < emitters.Count)
        {
            return emitters[emitterIndex].GlowIntensity;
        }

        return 1.0;
    }

    // =====================================================================
    // ShapeParameterBase の実装
    // =====================================================================

    public override IShapeSource CreateShapeSource(IGraphicsDevicesAndContext devices) =>
        new DanmakuShapeSource(devices, this);

    public override IEnumerable<string> CreateShapeItemExoFilter(int keyFrameIndex, ExoOutputDescription desc) => [];

    public override IEnumerable<string> CreateMaskExoFilter(
        int keyFrameIndex,
        ExoOutputDescription desc,
        ShapeMaskExoOutputDescription shapeMaskParameters) => [];

    protected override IEnumerable<IAnimatable> GetAnimatables()
    {
        yield return BaseAngle;
        yield return TargetX;
        yield return TargetY;
        foreach (var emitter in emitters)
        {
            yield return emitter;
        }
    }

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
