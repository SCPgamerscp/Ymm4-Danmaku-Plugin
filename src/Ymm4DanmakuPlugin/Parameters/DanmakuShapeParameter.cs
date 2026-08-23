using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Settings;
using Ymm4DanmakuPlugin.Core.Audio;
using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Scripting;
using Ymm4DanmakuPlugin.Settings;

namespace Ymm4DanmakuPlugin.Parameters;

/// <summary>
/// YMM4 の図形アイテムとして弾幕を描画するための設定パラメータ。
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
    [AnimationSlider("F1", "px", 0, 600)]
    public Animation OrbitRadius => MainEmitter.OrbitRadius;

    [Display(GroupName = "発射位置", Name = "公転速度", Description = "エミッターの円運動の速度。")]
    [AnimationSlider("F1", "度/秒", -360, 360)]
    public Animation OrbitSpeed => MainEmitter.OrbitSpeed;

    [Display(GroupName = "発射位置", Name = "公転位相", Description = "公転の初期角度。")]
    [AnimationSlider("F1", "度", 0, 360)]
    public Animation OrbitPhase => MainEmitter.OrbitPhase;

    [Display(GroupName = "発射位置", Name = "シードずらし", Description = "乱数をずらして弾のばらけ方を変えます。")]
    [AnimationSlider("F0", "", 0, 100)]
    public Animation SeedOffset => MainEmitter.SeedOffset;

    // =====================================================================
    // プリセット
    // =====================================================================

    [Display(GroupName = "プリセット", Name = "プリセット選択", Description = "東方風のサンプルを選んで [適用]。保存・読み込み・書き出しもここから行えます。")]
    [Presets.PresetSelector]
    public string PresetName
    {
        get => MainEmitter.PresetName;
        set => MainEmitter.PresetName = value;
    }

    // =====================================================================
    // 発射パターン
    // =====================================================================

    [Display(GroupName = "発射パターン", Name = "パターン")]
    [EnumComboBox]
    public PatternKind PatternKind
    {
        get => MainEmitter.PatternKind;
        set => MainEmitter.PatternKind = value;
    }

    [Display(GroupName = "発射パターン", Name = "ウェイ数 (方向数)", Description = "同時に放つ方向の数。0 で発射しません。")]
    [AnimationSlider("F0", "方向", 0, 128)]
    public Animation Way => MainEmitter.Way;

    [Display(GroupName = "発射パターン", Name = "段数 (連数)", Description = "1 方向あたりに連続して放つ弾数。0 で発射しません。")]
    [AnimationSlider("F0", "段", 0, 32)]
    public Animation Stack => MainEmitter.Stack;

    [Display(GroupName = "発射パターン", Name = "段ごとの速度差")]
    [AnimationSlider("F0", "px/秒", -200, 200)]
    public Animation StackSpeedStep => MainEmitter.StackSpeedStep;

    [Display(GroupName = "発射パターン", Name = "段ごとの角度差")]
    [AnimationSlider("F1", "度", -90, 90)]
    public Animation StackAngleStep => MainEmitter.StackAngleStep;

    [Display(GroupName = "発射パターン", Name = "発射角度", Description = "発射の基準となる向き。キーフレームで自由に回転させられます。0 で下向き。")]
    [AnimationSlider("F1", "度", -360, 360)]
    public Animation BaseAngle => MainEmitter.BaseAngle;

    [Display(GroupName = "発射パターン", Name = "拡散角度", Description = "扇形や放射で弾を広げる範囲。360 で全方位。")]
    [AnimationSlider("F1", "度", 0, 360)]
    public Animation SpreadAngle => MainEmitter.SpreadAngle;

    [Display(GroupName = "発射パターン", Name = "回転速度 (発射毎)", Description = "1 回撃つごとに発射方向を回転させる角度。渦巻き弾幕を作れます。")]
    [AnimationSlider("F1", "度/発", -180, 180)]
    public Animation AngleStepPerShot => MainEmitter.AngleStepPerShot;

    [Display(GroupName = "発射パターン", Name = "角度ゆらぎ")]
    [AnimationSlider("F1", "度", 0, 90)]
    public Animation AngleJitter => MainEmitter.AngleJitter;

    [Display(GroupName = "発射パターン", Name = "発射間隔", Description = "次の発射までの時間。0.1 で秒間 10 回。")]
    [AnimationSlider("F3", "秒", 0, 2)]
    public Animation FireInterval => MainEmitter.FireInterval;

    [Display(GroupName = "発射パターン", Name = "バースト発射数")]
    [AnimationSlider("F0", "発", 0, 32)]
    public Animation BurstCount => MainEmitter.BurstCount;

    [Display(GroupName = "発射パターン", Name = "バースト内間隔")]
    [AnimationSlider("F3", "秒", 0, 0.5)]
    public Animation BurstInterval => MainEmitter.BurstInterval;

    [Display(GroupName = "発射パターン", Name = "バースト後休止")]
    [AnimationSlider("F3", "秒", 0, 3)]
    public Animation BurstCooldown => MainEmitter.BurstCooldown;

    [Display(GroupName = "発射パターン", Name = "開始時刻", Description = "アイテム先頭からの発射開始秒数。")]
    [AnimationSlider("F2", "秒", 0, 10)]
    public Animation StartTime => MainEmitter.StartTime;

    [Display(GroupName = "発射パターン", Name = "終了時刻", Description = "0 でアイテム終端まで撃ち続けます。")]
    [AnimationSlider("F2", "秒", 0, 60)]
    public Animation EndTime => MainEmitter.EndTime;

    [Display(GroupName = "発射パターン", Name = "生成半径", Description = "エミッター中心から離れた円周上から弾を発生させます。")]
    [AnimationSlider("F1", "px", 0, 300)]
    public Animation SpawnRadius => MainEmitter.SpawnRadius;

    [Display(GroupName = "発射パターン", Name = "発生位置ゆらぎ")]
    [AnimationSlider("F1", "px", 0, 200)]
    public Animation SpawnJitter => MainEmitter.SpawnJitter;

    [Display(GroupName = "発射パターン", Name = "自機狙い度 (ターゲット追尾)", Description = "ターゲット (自機) 方向へ向ける割合。0% で固定角、100% で完全自機狙い。")]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation AimRate => MainEmitter.AimRate;

    [Display(GroupName = "発射パターン", Name = "壁の横幅")]
    [AnimationSlider("F0", "px", 0, 3840)]
    public Animation WallWidth => MainEmitter.WallWidth;

    [Display(GroupName = "発射パターン", Name = "レーザー間隔")]
    [AnimationSlider("F1", "px", 0, 120)]
    public Animation LaserSpacing => MainEmitter.LaserSpacing;

    [Display(GroupName = "発射パターン", Name = "鞭の振れ幅")]
    [AnimationSlider("F1", "度", 0, 180)]
    public Animation WhipAmplitude => MainEmitter.WhipAmplitude;

    [Display(GroupName = "発射パターン", Name = "鞭の周期")]
    [AnimationSlider("F2", "秒", 0, 6)]
    public Animation WhipPeriod => MainEmitter.WhipPeriod;

    // =====================================================================
    // 弾の見た目
    // =====================================================================

    [Display(GroupName = "弾の見た目", Name = "弾の形")]
    [EnumComboBox]
    public BulletShape Shape
    {
        get => MainEmitter.Shape;
        set => MainEmitter.Shape = value;
    }

    [Display(GroupName = "弾の見た目", Name = "画像", Description = "指定すると「弾の形」の代わりにこの画像を使います。")]
    [FileSelector(YukkuriMovieMaker.Settings.FileGroupType.ImageItem)]
    public string ImagePath
    {
        get => MainEmitter.ImagePath;
        set => MainEmitter.ImagePath = value;
    }

    [Display(GroupName = "弾の見た目", Name = "大きさ")]
    [AnimationSlider("F2", "倍", 0, 4)]
    public Animation Scale => MainEmitter.Scale;

    [Display(GroupName = "弾の見た目", Name = "大きさゆらぎ")]
    [AnimationSlider("F2", "倍", 0, 1)]
    public Animation ScaleJitter => MainEmitter.ScaleJitter;

    [Display(GroupName = "弾の見た目", Name = "拡縮速度", Description = "1 秒あたりの大きさの変化量。負で縮みます。")]
    [AnimationSlider("F2", "倍/秒", -2, 2)]
    public Animation ScaleVelocity => MainEmitter.ScaleVelocity;

    [Display(GroupName = "弾の見た目", Name = "進行方向に向ける", Description = "弾が飛んでいく向きに合わせて弾の向き・画像を回転させます。")]
    [ToggleSlider]
    public bool AlignToDirection
    {
        get => MainEmitter.AlignToDirection;
        set => MainEmitter.AlignToDirection = value;
    }

    [Display(GroupName = "弾の見た目", Name = "着色モード")]
    [EnumComboBox]
    public ColorMode ColorMode
    {
        get => MainEmitter.ColorMode;
        set => MainEmitter.ColorMode = value;
    }

    [Display(GroupName = "弾の見た目", Name = "メイン色 / ティント色")]
    [ColorPicker]
    public Color PrimaryColor
    {
        get => MainEmitter.PrimaryColor;
        set => MainEmitter.PrimaryColor = value;
    }

    [Display(GroupName = "弾の見た目", Name = "サブ色 (グラデーション用)")]
    [ColorPicker]
    public Color SecondaryColor
    {
        get => MainEmitter.SecondaryColor;
        set => MainEmitter.SecondaryColor = value;
    }

    [Display(GroupName = "弾の見た目", Name = "色相の速度", Description = "「虹色」のときの色の流れる速さ。")]
    [AnimationSlider("F0", "度/秒", -720, 720)]
    public Animation HueVelocity => MainEmitter.HueVelocity;

    [Display(GroupName = "弾の見た目", Name = "弾ごとの色相差")]
    [AnimationSlider("F1", "度", -180, 180)]
    public Animation HueStep => MainEmitter.HueStep;

    [Display(GroupName = "弾の見た目", Name = "加算合成 (発光)")]
    [ToggleSlider]
    public bool Additive
    {
        get => MainEmitter.Additive;
        set => MainEmitter.Additive = value;
    }

    [Display(GroupName = "弾の見た目", Name = "グロー発光強度")]
    [AnimationSlider("F2", "倍", 0, 3)]
    public Animation GlowIntensity => MainEmitter.GlowIntensity;

    [Display(GroupName = "弾の見た目", Name = "弾の不透明度")]
    [AnimationSlider("F2", "", 0, 1)]
    public Animation Opacity => MainEmitter.Opacity;

    [Display(GroupName = "弾の見た目", Name = "フェードイン")]
    [AnimationSlider("F2", "秒", 0, 1)]
    public Animation FadeInDuration => MainEmitter.FadeInDuration;

    [Display(GroupName = "弾の見た目", Name = "フェードアウト")]
    [AnimationSlider("F2", "秒", 0, 2)]
    public Animation FadeOutDuration => MainEmitter.FadeOutDuration;

    [Display(GroupName = "弾の見た目", Name = "残像の長さ")]
    [AnimationSlider("F0", "個", 0, 32)]
    public Animation TrailLength => MainEmitter.TrailLength;

    [Display(GroupName = "弾の見た目", Name = "残像の間隔")]
    [AnimationSlider("F3", "秒", 0, 0.2)]
    public Animation TrailInterval => MainEmitter.TrailInterval;

    [Display(GroupName = "弾の見た目", Name = "残像のフェード")]
    [AnimationSlider("F2", "", 0, 1)]
    public Animation TrailFade => MainEmitter.TrailFade;

    [Display(GroupName = "弾の見た目", Name = "残像の縮小")]
    [AnimationSlider("F2", "倍", 0, 1.5)]
    public Animation TrailScale => MainEmitter.TrailScale;

    // =====================================================================
    // 弾の物理
    // =====================================================================

    [Display(GroupName = "弾の物理", Name = "弾速")]
    [AnimationSlider("F0", "px/秒", -900, 900)]
    public Animation Speed => MainEmitter.Speed;

    [Display(GroupName = "弾の物理", Name = "初速ゆらぎ")]
    [AnimationSlider("F0", "px/秒", 0, 300)]
    public Animation SpeedJitter => MainEmitter.SpeedJitter;

    [Display(GroupName = "弾の物理", Name = "弾ごとの速度差")]
    [AnimationSlider("F1", "px/秒", -50, 50)]
    public Animation SpeedStep => MainEmitter.SpeedStep;

    [Display(GroupName = "弾の物理", Name = "加速度", Description = "1 秒あたりの速度変化。正で加速、負で減速。")]
    [AnimationSlider("F1", "px/秒²", -400, 400)]
    public Animation Acceleration => MainEmitter.Acceleration;

    [Display(GroupName = "弾の物理", Name = "角速度 (カーブ)", Description = "弾の進行方向を曲げる速度。正で時計回り。")]
    [AnimationSlider("F1", "度/秒", -360, 360)]
    public Animation AngularVelocity => MainEmitter.AngularVelocity;

    [Display(GroupName = "弾の物理", Name = "旋回ゆらぎ")]
    [AnimationSlider("F1", "度/秒", 0, 180)]
    public Animation AngularVelocityJitter => MainEmitter.AngularVelocityJitter;

    [Display(GroupName = "弾の物理", Name = "減速", Description = "1 秒後に残る速度の割合。1 で減速なし。0 で瞬時に静止。")]
    [AnimationSlider("F2", "", 0, 1)]
    public Animation Damping => MainEmitter.Damping;

    [Display(GroupName = "弾の物理", Name = "最低速度")]
    [AnimationSlider("F0", "px/秒", 0, 300)]
    public Animation MinSpeed => MainEmitter.MinSpeed;

    [Display(GroupName = "弾の物理", Name = "最高速度")]
    [AnimationSlider("F0", "px/秒", 0, 3000)]
    public Animation MaxSpeed => MainEmitter.MaxSpeed;

    [Display(GroupName = "弾の物理", Name = "重力", Description = "正で下向きの加速度。")]
    [AnimationSlider("F0", "px/秒²", -600, 600)]
    public Animation Gravity => MainEmitter.Gravity;

    [Display(GroupName = "弾の物理", Name = "風", Description = "正で右向きの加速度。")]
    [AnimationSlider("F0", "px/秒²", -600, 600)]
    public Animation Wind => MainEmitter.Wind;

    [Display(GroupName = "弾の物理", Name = "弾の寿命", Description = "0 で画面外に出るまで存続。")]
    [AnimationSlider("F2", "秒", 0, 20)]
    public Animation Lifetime => MainEmitter.Lifetime;

    [Display(GroupName = "弾の物理", Name = "寿命ゆらぎ")]
    [AnimationSlider("F2", "秒", 0, 5)]
    public Animation LifetimeJitter => MainEmitter.LifetimeJitter;

    [Display(GroupName = "弾の物理", Name = "自転速度")]
    [AnimationSlider("F0", "度/秒", -720, 720)]
    public Animation RotationVelocity => MainEmitter.RotationVelocity;

    // =====================================================================
    // ホーミング (誘導弾)
    // =====================================================================

    [Display(GroupName = "ホーミング", Name = "ホーミング有効")]
    [ToggleSlider]
    public bool HomingEnabled
    {
        get => MainEmitter.HomingEnabled;
        set => MainEmitter.HomingEnabled = value;
    }

    [Display(GroupName = "ホーミング", Name = "旋回性能")]
    [AnimationSlider("F0", "度/秒", 0, 720)]
    public Animation HomingTurnRate => MainEmitter.HomingTurnRate;

    [Display(GroupName = "ホーミング", Name = "誘導時間")]
    [AnimationSlider("F2", "秒", 0, 10)]
    public Animation HomingDuration => MainEmitter.HomingDuration;

    [Display(GroupName = "ホーミング", Name = "誘導開始遅延")]
    [AnimationSlider("F2", "秒", 0, 3)]
    public Animation HomingDelay => MainEmitter.HomingDelay;

    // =====================================================================
    // 弾の分裂
    // =====================================================================

    [Display(GroupName = "弾の分裂", Name = "分裂を有効化")]
    [ToggleSlider]
    public bool SplitEnabled
    {
        get => MainEmitter.SplitEnabled;
        set => MainEmitter.SplitEnabled = value;
    }

    [Display(GroupName = "弾の分裂", Name = "分裂までの時間")]
    [AnimationSlider("F2", "秒", 0, 5)]
    public Animation SplitDelay => MainEmitter.SplitDelay;

    [Display(GroupName = "弾の分裂", Name = "分裂数")]
    [AnimationSlider("F0", "個", 0, 32)]
    public Animation SplitCount => MainEmitter.SplitCount;

    [Display(GroupName = "弾の分裂", Name = "分裂拡散角")]
    [AnimationSlider("F1", "度", 0, 360)]
    public Animation SplitSpread => MainEmitter.SplitSpread;

    [Display(GroupName = "弾の分裂", Name = "分裂初速")]
    [AnimationSlider("F0", "px/秒", -800, 800)]
    public Animation SplitSpeed => MainEmitter.SplitSpeed;

    [Display(GroupName = "弾の分裂", Name = "分裂サイズ倍率")]
    [AnimationSlider("F2", "倍", 0, 2)]
    public Animation SplitScaleFactor => MainEmitter.SplitScaleFactor;

    [Display(GroupName = "弾の分裂", Name = "親弾を消滅させる")]
    [ToggleSlider]
    public bool SplitDestroyParent
    {
        get => MainEmitter.SplitDestroyParent;
        set => MainEmitter.SplitDestroyParent = value;
    }

    [Display(GroupName = "弾の分裂", Name = "多段分裂の回数")]
    [AnimationSlider("F0", "世代", 0, 5)]
    public Animation SplitMaxGeneration => MainEmitter.SplitMaxGeneration;

    // =====================================================================
    // 外部スクリプト
    // =====================================================================

    [Display(GroupName = "外部スクリプト", Name = "データ形式")]
    [EnumComboBox]
    public DanmakuSourceMode SourceMode
    {
        get => MainEmitter.SourceMode;
        set => MainEmitter.SourceMode = value;
    }

    [Display(GroupName = "外部スクリプト", Name = "スクリプトファイル")]
    [FileSelector(FileGroupType.None)]
    public string SourcePath
    {
        get => MainEmitter.SourcePath;
        set => MainEmitter.SourcePath = value;
    }

    [Display(GroupName = "外部スクリプト", Name = "スクリプト本文")]
    [TextEditor(AcceptsReturn = true, PropertyEditorSize = PropertyEditorSize.FullWidth)]
    public string SourceText
    {
        get => MainEmitter.SourceText;
        set => MainEmitter.SourceText = value;
    }

    [Display(GroupName = "外部スクリプト", Name = "BulletML 速度換算")]
    [AnimationSlider("F0", "px/秒", 0, 240)]
    public Animation ScriptSpeedScale => MainEmitter.ScriptSpeedScale;

    [Display(GroupName = "外部スクリプト", Name = "難易度 ($rank)")]
    [AnimationSlider("F2", "", 0, 1)]
    public Animation ScriptRank => MainEmitter.ScriptRank;

    [Display(GroupName = "外部スクリプト", Name = "繰り返し")]
    [ToggleSlider]
    public bool ScriptLoop
    {
        get => MainEmitter.ScriptLoop;
        set => MainEmitter.ScriptLoop = value;
    }

    // =====================================================================
    // 当たり判定 (被弾シミュレーション & 自機設定)
    // =====================================================================

    [Display(GroupName = "当たり判定", Name = "当たり判定を有効化", Description = "ターゲット (自機) との被弾判定を行います。")]
    [ToggleSlider]
    public bool CollisionEnabled { get => collisionEnabled; set => Set(ref collisionEnabled, value); }
    private bool collisionEnabled;

    [Display(GroupName = "当たり判定", Name = "ターゲット X", Description = "自機の X 座標。プレビュー画面でのドラッグやキーフレーム移動が可能です。")]
    [AnimationSlider("F1", "px", -1920, 1920)]
    public Animation TargetX { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "当たり判定", Name = "ターゲット Y", Description = "自機の Y 座標。プレビュー画面でのドラッグやキーフレーム移動が可能です。")]
    [AnimationSlider("F1", "px", -1080, 1080)]
    public Animation TargetY { get; } = new Animation(250, -100000, 100000);

    [Display(GroupName = "当たり判定", Name = "自機画像", Description = "自機 (ターゲット) の位置に表示するキャラクターや機体の画像。")]
    [FileSelector(YukkuriMovieMaker.Settings.FileGroupType.ImageItem)]
    public string TargetImagePath
    {
        get => targetImagePath;
        set => Set(ref targetImagePath, value ?? string.Empty);
    }
    private string targetImagePath = string.Empty;

    public bool HasCustomTargetImage => !string.IsNullOrWhiteSpace(TargetImagePath);

    [Display(GroupName = "当たり判定", Name = "自機画像サイズ", Description = "自機画像の拡大倍率。")]
    [AnimationSlider("F2", "倍", 0, 10)]
    public Animation TargetScale { get; } = new Animation(1.0, 0, 1000);

    [Display(GroupName = "当たり判定", Name = "自機画像の回転", Description = "自機画像の回転角度。")]
    [AnimationSlider("F1", "度", -360, 360)]
    public Animation TargetRotation { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "当たり判定", Name = "自機画像の不透明度")]
    [AnimationSlider("F2", "", 0, 1)]
    public Animation TargetOpacity { get; } = new Animation(1.0, 0, 1);

    [Display(GroupName = "当たり判定", Name = "ターゲット半径", Description = "自機の被弾判定半径 (喰らい判定)。0 で無敵になります。")]
    [AnimationSlider("F1", "px", 0, 200)]
    public Animation TargetRadius { get; } = new Animation(30, 0, 10000);

    [Display(GroupName = "当たり判定", Name = "弾の判定半径")]
    [AnimationSlider("F1", "px", 0, 40)]
    public Animation HitRadius => MainEmitter.HitRadius;

    [Display(GroupName = "当たり判定", Name = "被弾時に弾を消す")]
    [ToggleSlider]
    public bool DestroyOnHit
    {
        get => MainEmitter.DestroyOnHit;
        set => MainEmitter.DestroyOnHit = value;
    }

    [Display(GroupName = "当たり判定", Name = "被弾エフェクト (飛沫)")]
    [ToggleSlider]
    public bool SpawnHitEffect { get => spawnHitEffect; set => Set(ref spawnHitEffect, value); }
    private bool spawnHitEffect = true;

    [Display(GroupName = "当たり判定", Name = "飛沫の数")]
    [AnimationSlider("F0", "個", 0, 64)]
    public Animation HitEffectCount { get; } = new Animation(8, 0, 500);

    [Display(GroupName = "当たり判定", Name = "飛沫の速度")]
    [AnimationSlider("F0", "px/秒", 0, 600)]
    public Animation HitEffectSpeed { get; } = new Animation(160, 0, 100000);

    [Display(GroupName = "当たり判定", Name = "飛沫の寿命")]
    [AnimationSlider("F2", "秒", 0, 2)]
    public Animation HitEffectLifetime { get; } = new Animation(0.35, 0, 1000);

    [Display(GroupName = "当たり判定", Name = "ターゲットを表示", Description = "自機画像や当たり判定枠 (喰らい判定) を描画します。")]
    [ToggleSlider]
    public bool ShowTargetMarker { get => showTargetMarker; set => Set(ref showTargetMarker, value); }
    private bool showTargetMarker = true;

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
    [AnimationSlider("F0", "", 0, 100000)]
    public Animation Seed { get; } = new Animation(20240101, 0, 10000000);

    [Display(GroupName = "全体", Name = "最大弾数", Description = "同時に存在できる弾の上限。大きくすると重くなります。")]
    [AnimationSlider("F0", "発", 0, 20000)]
    public Animation MaxBullets { get; } = new Animation(4096, 0, 200000);

    [Display(GroupName = "全体", Name = "再生速度", Description = "弾幕全体の時間倍率。0 で完全静止 (時止め)、0.5 でスローモーションになります。")]
    [AnimationSlider("F2", "倍", 0, 3)]
    public Animation TimeScale { get; } = new Animation(1.0, 0, 100);

    [Display(GroupName = "全体", Name = "計算の細かさ", Description = "物理計算 1 ステップの時間。小さいほど正確ですが重くなります。")]
    [EnumComboBox]
    public SimulationStep SimulationStep { get => simulationStep; set => Set(ref simulationStep, value); }
    private SimulationStep simulationStep = SimulationStep.Hz120;

    [Display(GroupName = "全体", Name = "画面外の扱い")]
    [EnumComboBox]
    public OutOfBoundsBehavior OutOfBounds { get => outOfBounds; set => Set(ref outOfBounds, value); }
    private OutOfBoundsBehavior outOfBounds = OutOfBoundsBehavior.Destroy;

    [Display(GroupName = "全体", Name = "画面外の余裕", Description = "画面の外側にこの距離ぶん余裕を持たせ、その外へ出た弾を処理します。")]
    [AnimationSlider("F0", "px", 0, 1000)]
    public Animation BoundsMargin { get; } = new Animation(160, 0, 100000);

    [Display(GroupName = "全体", Name = "全体の不透明度")]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation GlobalOpacity { get; } = new Animation(100, 0, 100);

    [Display(GroupName = "全体", Name = "効果音チャンネル",
        Description = "「弾幕効果音」音声エフェクト側で同じ番号を指定すると、この弾幕に合わせて効果音が鳴ります。")]
    [AnimationSlider("F0", "ch", 0, 15)]
    public Animation Channel { get; } = new Animation(0, 0, 255);

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
        set
        {
            var oldEmitters = emitters;
            var newEmitters = value.IsEmpty ? [new EmitterParameter()] : value;
            if (Set(ref emitters, newEmitters))
            {
                UnsubscribeEmitters(oldEmitters);
                SubscribeEmitters(newEmitters);
                OnPropertyChanged(string.Empty);
            }
        }
    }
    private ImmutableList<EmitterParameter> emitters = [new EmitterParameter()];

    /// <summary>エミッターの上限。画像スロットの数 (<see cref="SpriteSlots"/>) と揃えている。</summary>
    public const int MaxEmitters = 16;

    public DanmakuShapeParameter() : this(null) { }

    public DanmakuShapeParameter(SharedDataStore? sharedData) : base(sharedData)
    {
        SubscribeAnimatable(TargetX, nameof(TargetX));
        SubscribeAnimatable(TargetY, nameof(TargetY));
        SubscribeAnimatable(TargetScale, nameof(TargetScale));
        SubscribeAnimatable(TargetRotation, nameof(TargetRotation));
        SubscribeAnimatable(TargetOpacity, nameof(TargetOpacity));
        SubscribeAnimatable(TargetRadius, nameof(TargetRadius));
        SubscribeAnimatable(HitEffectCount, nameof(HitEffectCount));
        SubscribeAnimatable(HitEffectSpeed, nameof(HitEffectSpeed));
        SubscribeAnimatable(HitEffectLifetime, nameof(HitEffectLifetime));
        SubscribeAnimatable(Seed, nameof(Seed));
        SubscribeAnimatable(MaxBullets, nameof(MaxBullets));
        SubscribeAnimatable(TimeScale, nameof(TimeScale));
        SubscribeAnimatable(BoundsMargin, nameof(BoundsMargin));
        SubscribeAnimatable(GlobalOpacity, nameof(GlobalOpacity));
        SubscribeAnimatable(Channel, nameof(Channel));

        SubscribeEmitters(emitters);
    }

    private void SubscribeAnimatable(Animation anim, string propertyName)
    {
        SubscribeChildUndoRedoable(anim);
        anim.PropertyChanged += (_, _) => OnPropertyChanged(propertyName);
    }

    private void SubscribeEmitters(IEnumerable<EmitterParameter> list)
    {
        foreach (var emitter in list)
        {
            SubscribeChildUndoRedoable(emitter);
            emitter.PropertyChanged += OnEmitterPropertyChanged;
        }
    }

    private void UnsubscribeEmitters(IEnumerable<EmitterParameter> list)
    {
        foreach (var emitter in list)
        {
            UnSubscribeChildUndoRedoable(emitter);
            emitter.PropertyChanged -= OnEmitterPropertyChanged;
        }
    }

    private void OnEmitterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender == MainEmitter)
        {
            if (string.IsNullOrEmpty(e.PropertyName))
            {
                OnPropertyChanged(string.Empty);
            }
            else
            {
                OnPropertyChanged(e.PropertyName);
            }
        }
    }

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
            BoundsMargin = BoundsMargin.GetFirstValue(),
            OutOfBounds = OutOfBounds,
            Seed = Math.Max(0, (int)Math.Round(Seed.GetFirstValue())),
            MaxBullets = Math.Max(0, (int)Math.Round(MaxBullets.GetFirstValue())),
            TimeScale = TimeScale.GetFirstValue(),
            FixedTimeStep = SimulationStep.ToSeconds(),

            Collision = new CollisionSettings
            {
                IsEnabled = CollisionEnabled,
                TargetRadius = TargetRadius.GetFirstValue(),
                SpawnHitEffect = SpawnHitEffect,
                HitEffectCount = Math.Max(0, (int)Math.Round(HitEffectCount.GetFirstValue())),
                HitEffectSpeed = HitEffectSpeed.GetFirstValue(),
                HitEffectLifetime = HitEffectLifetime.GetFirstValue(),
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
            return emitters.Count > 0 ? emitters[0].GlowIntensity.GetFirstValue() : 1.0;
        }

        var emitterIndex = SpriteSlots.EmitterIndexOf(spriteIndex);
        if (emitterIndex >= 0 && emitterIndex < emitters.Count)
        {
            return emitters[emitterIndex].GlowIntensity.GetFirstValue();
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
        yield return TargetX;
        yield return TargetY;
        yield return TargetScale;
        yield return TargetRotation;
        yield return TargetOpacity;
        yield return TargetRadius;
        yield return HitEffectCount;
        yield return HitEffectSpeed;
        yield return HitEffectLifetime;
        yield return Seed;
        yield return MaxBullets;
        yield return TimeScale;
        yield return BoundsMargin;
        yield return GlobalOpacity;
        yield return Channel;
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
        private readonly Animation seed = new(20240101, 0, 10000000);
        private readonly Animation maxBullets = new(4096, 0, 200000);
        private readonly Animation timeScale = new(1.0, 0, 100);
        private readonly SimulationStep simulationStep;
        private readonly OutOfBoundsBehavior outOfBounds;
        private readonly Animation boundsMargin = new(160, 0, 100000);
        private readonly Animation globalOpacity = new(100, 0, 100);
        private readonly Animation channel = new(0, 0, 255);

        private readonly bool collisionEnabled;
        private readonly Animation targetX = new(0, -100000, 100000);
        private readonly Animation targetY = new(250, -100000, 100000);
        private readonly string targetImagePath = string.Empty;
        private readonly Animation targetScale = new(1.0, 0, 1000);
        private readonly Animation targetRotation = new(0, -100000, 100000);
        private readonly Animation targetOpacity = new(1.0, 0, 1);
        private readonly Animation targetRadius = new(30, 0, 10000);
        private readonly bool spawnHitEffect;
        private readonly Animation hitEffectCount = new(8, 0, 500);
        private readonly Animation hitEffectSpeed = new(160, 0, 100000);
        private readonly Animation hitEffectLifetime = new(0.35, 0, 1000);
        private readonly bool showTargetMarker;

        private readonly bool fireSoundEnabled;
        private readonly bool changeSoundEnabled;
        private readonly bool hitSoundEnabled;
        private readonly bool vanishSoundEnabled;

        private readonly ImmutableList<EmitterParameter> emitters;

        public SharedData(DanmakuShapeParameter source)
        {
            seed.CopyFrom(source.Seed);
            maxBullets.CopyFrom(source.MaxBullets);
            timeScale.CopyFrom(source.TimeScale);
            simulationStep = source.SimulationStep;
            outOfBounds = source.OutOfBounds;
            boundsMargin.CopyFrom(source.BoundsMargin);
            globalOpacity.CopyFrom(source.GlobalOpacity);
            channel.CopyFrom(source.Channel);

            collisionEnabled = source.CollisionEnabled;
            targetX.CopyFrom(source.TargetX);
            targetY.CopyFrom(source.TargetY);
            targetImagePath = source.TargetImagePath;
            targetScale.CopyFrom(source.TargetScale);
            targetRotation.CopyFrom(source.TargetRotation);
            targetOpacity.CopyFrom(source.TargetOpacity);
            targetRadius.CopyFrom(source.TargetRadius);
            spawnHitEffect = source.SpawnHitEffect;
            hitEffectCount.CopyFrom(source.HitEffectCount);
            hitEffectSpeed.CopyFrom(source.HitEffectSpeed);
            hitEffectLifetime.CopyFrom(source.HitEffectLifetime);
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
            target.Seed.CopyFrom(seed);
            target.MaxBullets.CopyFrom(maxBullets);
            target.TimeScale.CopyFrom(timeScale);
            target.SimulationStep = simulationStep;
            target.OutOfBounds = outOfBounds;
            target.BoundsMargin.CopyFrom(boundsMargin);
            target.GlobalOpacity.CopyFrom(globalOpacity);
            target.Channel.CopyFrom(channel);

            target.CollisionEnabled = collisionEnabled;
            target.TargetX.CopyFrom(targetX);
            target.TargetY.CopyFrom(targetY);
            target.TargetImagePath = targetImagePath;
            target.TargetScale.CopyFrom(targetScale);
            target.TargetRotation.CopyFrom(targetRotation);
            target.TargetOpacity.CopyFrom(targetOpacity);
            target.TargetRadius.CopyFrom(targetRadius);
            target.SpawnHitEffect = spawnHitEffect;
            target.HitEffectCount.CopyFrom(hitEffectCount);
            target.HitEffectSpeed.CopyFrom(hitEffectSpeed);
            target.HitEffectLifetime.CopyFrom(hitEffectLifetime);
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
