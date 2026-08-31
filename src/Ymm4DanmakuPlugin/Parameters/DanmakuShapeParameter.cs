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
using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Scripting;
using Ymm4DanmakuPlugin.Interop;

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

    [Display(GroupName = "発射位置", Name = "公転半径", Description = "エミッター自体を円運動させる半径。負の値で反対側を基準にします。")]
    [AnimationSlider("F1", "px", -600, 600)]
    public Animation OrbitRadius => MainEmitter.OrbitRadius;

    [Display(GroupName = "発射位置", Name = "公転速度", Description = "エミッターの円運動の速度。")]
    [AnimationSlider("F1", "度/秒", -360, 360)]
    public Animation OrbitSpeed => MainEmitter.OrbitSpeed;

    [Display(GroupName = "発射位置", Name = "公転位相", Description = "公転の初期角度。")]
    [AnimationSlider("F1", "度", -360, 360)]
    public Animation OrbitPhase => MainEmitter.OrbitPhase;

    [Display(GroupName = "発射位置", Name = "シードずらし", Description = "乱数をずらして弾のばらけ方を変えます。")]
    [AnimationSlider("F0", "", -100, 100)]
    public Animation SeedOffset => MainEmitter.SeedOffset;

    [Display(GroupName = "発射位置", Name = "制御点を表示",
        Description = "オフにすると、プレビュー画面上のドラッグ用丸ハンドル (〇) を非表示にします。")]
    [ToggleSlider]
    [DefaultValue(true)]
    public bool ShowControllers { get => showControllers; set => Set(ref showControllers, value); }
    private bool showControllers = true;

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
    // エネミー (敵) & 魔法陣 & オーラ
    // =====================================================================

    [Display(GroupName = "エネミー (敵)", Name = "エネミー画像", Description = "発射位置 (エミッター) の中心に表示するキャラクターやボスの画像。")]
    [FileSelector(FileGroupType.ImageItem)]
    public string EnemyImagePath
    {
        get => MainEmitter.EnemyImagePath;
        set => MainEmitter.EnemyImagePath = value;
    }

    public bool HasEnemyImage => MainEmitter.HasEnemyImage;

    [Display(GroupName = "エネミー (敵)", Name = "画像サイズ", Description = "エネミー画像の拡大倍率。負の値で左右/上下反転。")]
    [AnimationSlider("F2", "倍", -10, 10)]
    public Animation EnemyScale => MainEmitter.EnemyScale;

    [Display(GroupName = "エネミー (敵)", Name = "画像の回転", Description = "エネミー画像の回転角度。")]
    [AnimationSlider("F1", "度", -360, 360)]
    public Animation EnemyRotation => MainEmitter.EnemyRotation;

    [Display(GroupName = "エネミー (敵)", Name = "不透明度")]
    [AnimationSlider("F2", "", -1, 1)]
    public Animation EnemyOpacity => MainEmitter.EnemyOpacity;

    [Display(GroupName = "エネミー (敵)", Name = "弾の奥に描画", Description = "オンで弾幕の背後に配置、オフで弾幕の手前に配置します。")]
    [ToggleSlider]
    public bool EnemyBehindBullets
    {
        get => MainEmitter.EnemyBehindBullets;
        set => MainEmitter.EnemyBehindBullets = value;
    }

    [Display(GroupName = "エネミー (敵)", Name = "魔法陣を有効化", Description = "ボスの背後に東方風の魔法陣を展開します。")]
    [ToggleSlider]
    public bool MagicCircleEnabled
    {
        get => MainEmitter.MagicCircleEnabled;
        set => MainEmitter.MagicCircleEnabled = value;
    }

    [Display(GroupName = "エネミー (敵)", Name = "魔法陣画像", Description = "カスタム魔法陣画像。未指定時は組み込みの東方風幾何学魔法陣が描かれます。")]
    [FileSelector(FileGroupType.ImageItem)]
    public string MagicCircleImagePath
    {
        get => MainEmitter.MagicCircleImagePath;
        set => MainEmitter.MagicCircleImagePath = value;
    }

    public bool HasCustomMagicCircleImage => MainEmitter.HasCustomMagicCircleImage;

    [Display(GroupName = "エネミー (敵)", Name = "魔法陣サイズ")]
    [AnimationSlider("F2", "倍", -10, 10)]
    public Animation MagicCircleScale => MainEmitter.MagicCircleScale;

    [Display(GroupName = "エネミー (敵)", Name = "魔法陣回転速度", Description = "1 秒あたりの回転角度。正で時計回り、負で反時計回り。")]
    [AnimationSlider("F1", "度/秒", -720, 720)]
    public Animation MagicCircleRotationSpeed => MainEmitter.MagicCircleRotationSpeed;

    [Display(GroupName = "エネミー (敵)", Name = "魔法陣の色")]
    [ColorPicker]
    public Color MagicCircleColor
    {
        get => MainEmitter.MagicCircleColor;
        set => MainEmitter.MagicCircleColor = value;
    }

    [Display(GroupName = "エネミー (敵)", Name = "魔法陣の不透明度")]
    [AnimationSlider("F2", "", -1, 1)]
    public Animation MagicCircleOpacity => MainEmitter.MagicCircleOpacity;

    [Display(GroupName = "エネミー (敵)", Name = "魔法陣を加算合成")]
    [ToggleSlider]
    public bool MagicCircleAdditive
    {
        get => MainEmitter.MagicCircleAdditive;
        set => MainEmitter.MagicCircleAdditive = value;
    }

    [Display(GroupName = "エネミー (敵)", Name = "オーラを有効化", Description = "ボスの周囲に発光オーラを纏わせます。")]
    [ToggleSlider]
    public bool AuraEnabled
    {
        get => MainEmitter.AuraEnabled;
        set => MainEmitter.AuraEnabled = value;
    }

    [Display(GroupName = "エネミー (敵)", Name = "オーラ強度")]
    [AnimationSlider("F2", "倍", -5, 5)]
    public Animation AuraIntensity => MainEmitter.AuraIntensity;

    [Display(GroupName = "エネミー (敵)", Name = "オーラの色")]
    [ColorPicker]
    public Color AuraColor
    {
        get => MainEmitter.AuraColor;
        set => MainEmitter.AuraColor = value;
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

    [Display(GroupName = "発射パターン", Name = "Way数", Description = "同時に放つ方向の数。0 で発射しません。")]
    [AnimationSlider("F0", "方向", 0, 128)]
    public Animation Way => MainEmitter.Way;

    [Display(GroupName = "発射パターン", Name = "段数", Description = "1 方向あたりに連続して放つ弾数。0 で発射しません。")]
    [AnimationSlider("F0", "段", 0, 32)]
    public Animation Stack => MainEmitter.Stack;

    [Display(GroupName = "発射パターン", Name = "段速度差", Description = "段ごとの弾速の差 (px/秒)。")]
    [AnimationSlider("F0", "px/秒", -200, 200)]
    public Animation StackSpeedStep => MainEmitter.StackSpeedStep;

    [Display(GroupName = "発射パターン", Name = "段角度差", Description = "段ごとの発射角度の差 (度)。")]
    [AnimationSlider("F1", "度", -90, 90)]
    public Animation StackAngleStep => MainEmitter.StackAngleStep;

    [Display(GroupName = "発射パターン", Name = "発射角度", Description = "発射の基準となる向き。キーフレームで自由に回転させられます。0 で下向き。")]
    [AnimationSlider("F1", "度", -360, 360)]
    public Animation BaseAngle => MainEmitter.BaseAngle;

    [Display(GroupName = "発射パターン", Name = "拡散角度", Description = "扇形や放射で弾を広げる範囲。負の値で逆方向に展開。360 で全方位。")]
    [AnimationSlider("F1", "度", -360, 360)]
    public Animation SpreadAngle => MainEmitter.SpreadAngle;

    [Display(GroupName = "発射パターン", Name = "発射毎回転", Description = "1 回撃つごとに発射方向を回転させる角度。渦巻き弾幕を作れます。")]
    [AnimationSlider("F1", "度/発", -180, 180)]
    public Animation AngleStepPerShot => MainEmitter.AngleStepPerShot;

    [Display(GroupName = "発射パターン", Name = "角度ブレ", Description = "発射角度のランダムな揺らぎ (±度)。")]
    [AnimationSlider("F1", "度", -90, 90)]
    public Animation AngleJitter => MainEmitter.AngleJitter;

    [Display(GroupName = "発射パターン", Name = "発射間隔", Description = "次の発射までの時間。0.1 で秒間 10 回。")]
    [AnimationSlider("F3", "秒", 0, 2)]
    public Animation FireInterval => MainEmitter.FireInterval;

    [Display(GroupName = "発射パターン", Name = "連射数", Description = "1 回のバーストで連続発射する弾数。")]
    [AnimationSlider("F0", "発", 0, 32)]
    public Animation BurstCount => MainEmitter.BurstCount;

    [Display(GroupName = "発射パターン", Name = "連射間隔", Description = "バースト内の弾と弾の発射間隔 (秒)。")]
    [AnimationSlider("F3", "秒", 0, 0.5)]
    public Animation BurstInterval => MainEmitter.BurstInterval;

    [Display(GroupName = "発射パターン", Name = "連射後休止", Description = "バースト終了後の待機時間 (秒)。")]
    [AnimationSlider("F3", "秒", 0, 3)]
    public Animation BurstCooldown => MainEmitter.BurstCooldown;

    [Display(GroupName = "発射パターン", Name = "開始時刻", Description = "アイテム先頭からの発射開始秒数。")]
    [AnimationSlider("F2", "秒", -10, 10)]
    public Animation StartTime => MainEmitter.StartTime;

    [Display(GroupName = "発射パターン", Name = "終了時刻", Description = "0 でアイテム終端まで撃ち続けます。")]
    [AnimationSlider("F2", "秒", 0, 60)]
    public Animation EndTime => MainEmitter.EndTime;

    [Display(GroupName = "発射パターン", Name = "発生半径", Description = "エミッター中心から離れた円周上から弾を発生させます。負の値で後方から発生。")]
    [AnimationSlider("F1", "px", -300, 300)]
    public Animation SpawnRadius => MainEmitter.SpawnRadius;

    [Display(GroupName = "発射パターン", Name = "発生位置ブレ", Description = "弾の発生位置のランダムなズレ (px)。")]
    [AnimationSlider("F1", "px", -200, 200)]
    public Animation SpawnJitter => MainEmitter.SpawnJitter;

    [Display(GroupName = "発射パターン", Name = "自機狙い度", Description = "ターゲット (自機) 方向へ向ける割合。0% で固定角、100% で完全自機狙い、-100% で自機の真反対へ発射。")]
    [AnimationSlider("F1", "%", -100, 100)]
    public Animation AimRate => MainEmitter.AimRate;

    [Display(GroupName = "発射パターン", Name = "壁の横幅", Description = "横一列に並べて配置する横幅。0 で点発生。")]
    [AnimationSlider("F0", "px", -3840, 3840)]
    public Animation WallWidth => MainEmitter.WallWidth;

    [Display(GroupName = "発射パターン", Name = "レーザー間隔", Description = "進行方向に弾を並べる間隔。0 で前後オフセットなし。")]
    [AnimationSlider("F1", "px", -120, 120)]
    public Animation LaserSpacing => MainEmitter.LaserSpacing;

    [Display(GroupName = "発射パターン", Name = "鞭の振幅", Description = "左右に首を振る振れ幅。0 で首振りなし。")]
    [AnimationSlider("F1", "度", -180, 180)]
    public Animation WhipAmplitude => MainEmitter.WhipAmplitude;

    [Display(GroupName = "発射パターン", Name = "鞭の周期", Description = "首振りの 1 周期にかかる時間 (秒)。")]
    [AnimationSlider("F2", "秒", -6, 6)]
    public Animation WhipPeriod => MainEmitter.WhipPeriod;

    // =====================================================================
    // 弾の見た目
    // =====================================================================

    [Display(GroupName = "弾の見た目", Name = "弾の形状", Description = "12 種類の組み込み形状から選択します。画像未指定時に使用されます。")]
    [EnumComboBox]
    public BulletShape Shape
    {
        get => MainEmitter.Shape;
        set => MainEmitter.Shape = value;
    }

    [Display(GroupName = "弾の見た目", Name = "カスタム画像", Description = "指定すると「弾の形状」の代わりにこの画像を使います。")]
    [FileSelector(YukkuriMovieMaker.Settings.FileGroupType.ImageItem)]
    public string ImagePath
    {
        get => MainEmitter.ImagePath;
        set => MainEmitter.ImagePath = value;
    }

    [Display(GroupName = "弾の見た目", Name = "サイズ", Description = "拡大倍率。負の値で画像を反転 (ミラー) 描画します。")]
    [AnimationSlider("F2", "倍", -4, 4)]
    public Animation Scale => MainEmitter.Scale;

    [Display(GroupName = "弾の見た目", Name = "サイズブレ", Description = "弾ごとの大きさのランダムな揺らぎ (±倍率)。")]
    [AnimationSlider("F2", "倍", -1, 1)]
    public Animation ScaleJitter => MainEmitter.ScaleJitter;

    [Display(GroupName = "弾の見た目", Name = "拡縮速度", Description = "1 秒あたりの大きさの変化量。負で縮みます。")]
    [AnimationSlider("F2", "倍/秒", -2, 2)]
    public Animation ScaleVelocity => MainEmitter.ScaleVelocity;

    [Display(GroupName = "弾の見た目", Name = "弾向き追従", Description = "弾が飛んでいく向きに合わせて弾の向き・画像を回転させます。")]
    [ToggleSlider]
    public bool AlignToDirection
    {
        get => MainEmitter.AlignToDirection;
        set => MainEmitter.AlignToDirection = value;
    }

    [Display(GroupName = "弾の見た目", Name = "着色モード", Description = "単色・グラデーション・虹色・パレット・ランダムから選択します。")]
    [EnumComboBox]
    public ColorMode ColorMode
    {
        get => MainEmitter.ColorMode;
        set => MainEmitter.ColorMode = value;
    }

    [Display(GroupName = "弾の見た目", Name = "メイン色", Description = "弾の基本色、またはカスタム画像への着色 (ティント) 色。")]
    [ColorPicker]
    public Color PrimaryColor
    {
        get => MainEmitter.PrimaryColor;
        set => MainEmitter.PrimaryColor = value;
    }

    [Display(GroupName = "弾の見た目", Name = "サブ色", Description = "「グラデーション」選択時の終端色。")]
    [ColorPicker]
    public Color SecondaryColor
    {
        get => MainEmitter.SecondaryColor;
        set => MainEmitter.SecondaryColor = value;
    }

    [Display(GroupName = "弾の見た目", Name = "虹色速度", Description = "「虹色」選択時の色が流れる回転速度 (度/秒)。")]
    [AnimationSlider("F0", "度/秒", -720, 720)]
    public Animation HueVelocity => MainEmitter.HueVelocity;

    [Display(GroupName = "弾の見た目", Name = "弾毎の色差", Description = "way ごとにずらす色相の差 (度)。")]
    [AnimationSlider("F1", "度", -180, 180)]
    public Animation HueStep => MainEmitter.HueStep;

    [Display(GroupName = "弾の見た目", Name = "加算発光", Description = "東方風の光る弾 (加算合成グロー) にします。")]
    [ToggleSlider]
    public bool Additive
    {
        get => MainEmitter.Additive;
        set => MainEmitter.Additive = value;
    }

    [Display(GroupName = "弾の見た目", Name = "発光強度", Description = "加算グローの輝度倍率。")]
    [AnimationSlider("F2", "倍", 0, 3)]
    public Animation GlowIntensity => MainEmitter.GlowIntensity;

    [Display(GroupName = "弾の見た目", Name = "不透明度", Description = "弾の濃さ (0.0 で透明、1.0 で完全不透明)。")]
    [AnimationSlider("F2", "", -1, 1)]
    public Animation Opacity => MainEmitter.Opacity;

    [Display(GroupName = "弾の見た目", Name = "フェードイン", Description = "発生時に透明から浮かび上がる秒数。")]
    [AnimationSlider("F2", "秒", 0, 1)]
    public Animation FadeInDuration => MainEmitter.FadeInDuration;

    [Display(GroupName = "弾の見た目", Name = "フェードアウト", Description = "消滅時に透明へと消える秒数。")]
    [AnimationSlider("F2", "秒", 0, 2)]
    public Animation FadeOutDuration => MainEmitter.FadeOutDuration;

    [Display(GroupName = "弾の見た目", Name = "残像数", Description = "弾の後ろに残す軌跡スプライトの個数。0 で残像なし。")]
    [AnimationSlider("F0", "個", 0, 32)]
    public Animation TrailLength => MainEmitter.TrailLength;

    [Display(GroupName = "弾の見た目", Name = "残像間隔", Description = "残像を記録・配置する時間間隔 (秒)。")]
    [AnimationSlider("F3", "秒", 0, 0.2)]
    public Animation TrailInterval => MainEmitter.TrailInterval;

    [Display(GroupName = "弾の見た目", Name = "残像濃度", Description = "残像の末端に向かって減衰する不透明度。")]
    [AnimationSlider("F2", "", 0, 1)]
    public Animation TrailFade => MainEmitter.TrailFade;

    [Display(GroupName = "弾の見た目", Name = "残像サイズ", Description = "残像の末端に向かって変化するスケール倍率。")]
    [AnimationSlider("F2", "倍", -1.5, 1.5)]
    public Animation TrailScale => MainEmitter.TrailScale;

    // =====================================================================
    // 弾の物理
    // =====================================================================

    [Display(GroupName = "弾の物理", Name = "初速", Description = "弾が発射された瞬間の基本スピード (px/秒)。")]
    [AnimationSlider("F0", "px/秒", -900, 900)]
    public Animation Speed => MainEmitter.Speed;

    [Display(GroupName = "弾の物理", Name = "初速ブレ", Description = "初速のランダムな揺らぎ (±px/秒)。")]
    [AnimationSlider("F0", "px/秒", -300, 300)]
    public Animation SpeedJitter => MainEmitter.SpeedJitter;

    [Display(GroupName = "弾の物理", Name = "弾毎の速度差", Description = "n-way の外側と内側でつける速度差。")]
    [AnimationSlider("F1", "px/秒", -50, 50)]
    public Animation SpeedStep => MainEmitter.SpeedStep;

    [Display(GroupName = "弾の物理", Name = "加速度", Description = "1 秒あたりの速度変化。正で加速、負で減速。")]
    [AnimationSlider("F1", "px/秒²", -400, 400)]
    public Animation Acceleration => MainEmitter.Acceleration;

    [Display(GroupName = "弾の物理", Name = "カーブ速度", Description = "弾の進行方向を曲げる角速度。正で時計回り。")]
    [AnimationSlider("F1", "度/秒", -360, 360)]
    public Animation AngularVelocity => MainEmitter.AngularVelocity;

    [Display(GroupName = "弾の物理", Name = "カーブブレ", Description = "曲がる強さのランダムな揺らぎ (±度/秒)。")]
    [AnimationSlider("F1", "度/秒", -180, 180)]
    public Animation AngularVelocityJitter => MainEmitter.AngularVelocityJitter;

    [Display(GroupName = "弾の物理", Name = "減速割合", Description = "空気抵抗。1 秒後に残る速度の割合。1.0 で減速なし。")]
    [AnimationSlider("F2", "", -1, 1)]
    public Animation Damping => MainEmitter.Damping;

    [Display(GroupName = "弾の物理", Name = "最低速度", Description = "減速時の速度の下限リミッター。")]
    [AnimationSlider("F0", "px/秒", -3000, 3000)]
    public Animation MinSpeed => MainEmitter.MinSpeed;

    [Display(GroupName = "弾の物理", Name = "最高速度", Description = "加速時の速度の上限リミッター。")]
    [AnimationSlider("F0", "px/秒", -3000, 3000)]
    public Animation MaxSpeed => MainEmitter.MaxSpeed;

    [Display(GroupName = "弾の物理", Name = "重力", Description = "正で下向きにかかる重力加速度。")]
    [AnimationSlider("F0", "px/秒²", -600, 600)]
    public Animation Gravity => MainEmitter.Gravity;

    [Display(GroupName = "弾の物理", Name = "風", Description = "正で右向きにかかる横風加速度。")]
    [AnimationSlider("F0", "px/秒²", -600, 600)]
    public Animation Wind => MainEmitter.Wind;

    [Display(GroupName = "弾の物理", Name = "弾の寿命", Description = "弾が存在できる秒数。0 で画面外に出るまで存続。")]
    [AnimationSlider("F2", "秒", 0, 20)]
    public Animation Lifetime => MainEmitter.Lifetime;

    [Display(GroupName = "弾の物理", Name = "寿命ブレ", Description = "寿命のランダムな揺らぎ (±秒)。")]
    [AnimationSlider("F2", "秒", -5, 5)]
    public Animation LifetimeJitter => MainEmitter.LifetimeJitter;

    [Display(GroupName = "弾の物理", Name = "自転速度", Description = "弾自体のスプライト自転速度 (度/秒)。")]
    [AnimationSlider("F0", "度/秒", -720, 720)]
    public Animation RotationVelocity => MainEmitter.RotationVelocity;

    // =====================================================================
    // ホーミング (誘導弾)
    // =====================================================================

    [Display(GroupName = "ホーミング", Name = "ホーミング", Description = "ターゲット (自機) を追いかける誘導弾にします。")]
    [ToggleSlider]
    public bool HomingEnabled
    {
        get => MainEmitter.HomingEnabled;
        set => MainEmitter.HomingEnabled = value;
    }

    [Display(GroupName = "ホーミング", Name = "追尾力", Description = "正で自機を追尾、負で自機から逃げるように反発旋回します。")]
    [AnimationSlider("F0", "度/秒", -720, 720)]
    public Animation HomingTurnRate => MainEmitter.HomingTurnRate;

    [Display(GroupName = "ホーミング", Name = "追尾時間", Description = "0 で寿命いっぱい追尾します。")]
    [AnimationSlider("F2", "秒", 0, 10)]
    public Animation HomingDuration => MainEmitter.HomingDuration;

    [Display(GroupName = "ホーミング", Name = "追尾遅延", Description = "発射から追尾を開始するまでの時間 (秒)。")]
    [AnimationSlider("F2", "秒", 0, 3)]
    public Animation HomingDelay => MainEmitter.HomingDelay;

    // =====================================================================
    // 弾の分裂
    // =====================================================================

    [Display(GroupName = "弾の分裂", Name = "分裂", Description = "一定時間後に弾を多方向へ分裂させます。")]
    [ToggleSlider]
    public bool SplitEnabled
    {
        get => MainEmitter.SplitEnabled;
        set => MainEmitter.SplitEnabled = value;
    }

    [Display(GroupName = "弾の分裂", Name = "分裂時間", Description = "発射から分裂するまでの遅延秒数。")]
    [AnimationSlider("F2", "秒", 0, 5)]
    public Animation SplitDelay => MainEmitter.SplitDelay;

    [Display(GroupName = "弾の分裂", Name = "分裂数", Description = "1 発の弾から発生する子弾の個数。")]
    [AnimationSlider("F0", "個", 0, 32)]
    public Animation SplitCount => MainEmitter.SplitCount;

    [Display(GroupName = "弾の分裂", Name = "分裂拡散角", Description = "子弾を広げる扇の角度。360 で全方位。")]
    [AnimationSlider("F1", "度", -360, 360)]
    public Animation SplitSpread => MainEmitter.SplitSpread;

    [Display(GroupName = "弾の分裂", Name = "分裂速度", Description = "分裂直後の子弾の初速 (px/秒)。")]
    [AnimationSlider("F0", "px/秒", -800, 800)]
    public Animation SplitSpeed => MainEmitter.SplitSpeed;

    [Display(GroupName = "弾の分裂", Name = "分裂サイズ", Description = "親弾に対する子弾の大きさ倍率。")]
    [AnimationSlider("F2", "倍", -2, 2)]
    public Animation SplitScaleFactor => MainEmitter.SplitScaleFactor;

    [Display(GroupName = "弾の分裂", Name = "親弾消滅", Description = "分裂時に元の親弾を消去します。")]
    [ToggleSlider]
    public bool SplitDestroyParent
    {
        get => MainEmitter.SplitDestroyParent;
        set => MainEmitter.SplitDestroyParent = value;
    }

    [Display(GroupName = "弾の分裂", Name = "多段世代数", Description = "2 以上でさらに分裂を繰り返します。")]
    [AnimationSlider("F0", "世代", 0, 5)]
    public Animation SplitMaxGeneration => MainEmitter.SplitMaxGeneration;

    // =====================================================================
    // 外部スクリプト
    // =====================================================================

    [Display(GroupName = "外部スクリプト", Name = "形式", Description = "内部パターン・JSON・BulletML・Lua から選択。")]
    [EnumComboBox]
    public DanmakuSourceMode SourceMode
    {
        get => MainEmitter.SourceMode;
        set => MainEmitter.SourceMode = value;
    }

    [Display(GroupName = "外部スクリプト", Name = "外部ファイル", Description = "読み込む外部スクリプトのパス。")]
    [FileSelector(FileGroupType.None)]
    public string SourcePath
    {
        get => MainEmitter.SourcePath;
        set => MainEmitter.SourcePath = value;
    }

    [Display(GroupName = "外部スクリプト", Name = "コード編集", Description = "直接スクリプトコードを記述・編集できます。")]
    [TextEditor(AcceptsReturn = true, PropertyEditorSize = PropertyEditorSize.FullWidth)]
    public string SourceText
    {
        get => MainEmitter.SourceText;
        set => MainEmitter.SourceText = value;
    }

    [Display(GroupName = "外部スクリプト", Name = "BulletML速度", Description = "BulletML の speed 1.0 あたりの px/秒 換算値。")]
    [AnimationSlider("F0", "px/秒", -240, 240)]
    public Animation ScriptSpeedScale => MainEmitter.ScriptSpeedScale;

    [Display(GroupName = "外部スクリプト", Name = "難易度(rank)", Description = "BulletML / Lua スクリプト内の $rank パラメータ (0.0〜1.0)。")]
    [AnimationSlider("F2", "", -1, 1)]
    public Animation ScriptRank => MainEmitter.ScriptRank;

    [Display(GroupName = "外部スクリプト", Name = "ループ再生", Description = "スクリプト終了時に先頭から繰り返し実行します。")]
    [ToggleSlider]
    public bool ScriptLoop
    {
        get => MainEmitter.ScriptLoop;
        set => MainEmitter.ScriptLoop = value;
    }

    // =====================================================================
    // 当たり判定 (被弾シミュレーション & 自機設定)
    // =====================================================================

    [Display(GroupName = "当たり判定", Name = "当たり判定", Description = "自機と敵弾との被弾判定を行います。")]
    [ToggleSlider]
    public bool CollisionEnabled { get => collisionEnabled; set => Set(ref collisionEnabled, value); }
    private bool collisionEnabled;

    [Display(GroupName = "当たり判定", Name = "自機 X", Description = "自機の X 座標。プレビュー画面でのドラッグやキーフレーム移動が可能です。")]
    [AnimationSlider("F1", "px", -1920, 1920)]
    public Animation TargetX { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "当たり判定", Name = "自機 Y", Description = "自機の Y 座標。プレビュー画面でのドラッグやキーフレーム移動が可能です。")]
    [AnimationSlider("F1", "px", -1080, 1080)]
    public Animation TargetY { get; } = new Animation(250, -100000, 100000);

    [Display(GroupName = "当たり判定", Name = "自機画像", Description = "自機の位置に表示するキャラクターや機体の画像。")]
    [FileSelector(YukkuriMovieMaker.Settings.FileGroupType.ImageItem)]
    public string TargetImagePath
    {
        get => targetImagePath;
        set => Set(ref targetImagePath, value ?? string.Empty);
    }
    private string targetImagePath = string.Empty;

    public bool HasCustomTargetImage => !string.IsNullOrWhiteSpace(TargetImagePath);

    [Display(GroupName = "当たり判定", Name = "自機サイズ", Description = "自機画像の拡大倍率。負の値で左右/上下反転。")]
    [AnimationSlider("F2", "倍", -10, 10)]
    public Animation TargetScale { get; } = new Animation(1.0, -1000, 1000);

    [Display(GroupName = "当たり判定", Name = "自機回転", Description = "自機画像の回転角度。")]
    [AnimationSlider("F1", "度", -360, 360)]
    public Animation TargetRotation { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "当たり判定", Name = "自機濃度", Description = "自機画像の不透明度 (0.0〜1.0)。")]
    [AnimationSlider("F2", "", -1, 1)]
    public Animation TargetOpacity { get; } = new Animation(1.0, -1, 1);

    [Display(GroupName = "当たり判定", Name = "自機判定半径", Description = "自機の被弾判定半径 (喰らい判定)。0 で無敵になります。")]
    [AnimationSlider("F1", "px", -200, 200)]
    public Animation TargetRadius { get; } = new Animation(30, -10000, 10000);

    [Display(GroupName = "当たり判定", Name = "ボス判定有効", Description = "エネミー (ボス) への自機ショット被弾判定を行います。")]
    [ToggleSlider]
    public bool EnemyHitEnabled { get => enemyHitEnabled; set => Set(ref enemyHitEnabled, value); }
    private bool enemyHitEnabled = true;

    [Display(GroupName = "当たり判定", Name = "ボス判定半径", Description = "エネミーの被弾判定半径 (px)。")]
    [AnimationSlider("F1", "px", -500, 500)]
    public Animation EnemyRadius { get; } = new Animation(40, -10000, 10000);

    [Display(GroupName = "当たり判定", Name = "敵弾判定半径", Description = "エミッターから発射される敵弾の被弾判定半径 (px)。0 で判定なし。")]
    [AnimationSlider("F1", "px", -40, 40)]
    public Animation HitRadius => MainEmitter.HitRadius;

    [Display(GroupName = "当たり判定", Name = "当たると消滅", Description = "被弾時に敵弾を消去します。")]
    [ToggleSlider]
    public bool DestroyOnHit
    {
        get => MainEmitter.DestroyOnHit;
        set => MainEmitter.DestroyOnHit = value;
    }

    [Display(GroupName = "当たり判定", Name = "被弾スパーク", Description = "被弾時に飛沫エフェクトを発生させます。")]
    [ToggleSlider]
    public bool SpawnHitEffect { get => spawnHitEffect; set => Set(ref spawnHitEffect, value); }
    private bool spawnHitEffect = true;

    [Display(GroupName = "当たり判定", Name = "スパーク数", Description = "被弾時に飛び散る破片の個数。")]
    [AnimationSlider("F0", "個", 0, 64)]
    public Animation HitEffectCount { get; } = new Animation(8, 0, 500);

    [Display(GroupName = "当たり判定", Name = "スパーク速度", Description = "破片が飛び散る初速 (px/秒)。")]
    [AnimationSlider("F0", "px/秒", -600, 600)]
    public Animation HitEffectSpeed { get; } = new Animation(160, -100000, 100000);

    [Display(GroupName = "当たり判定", Name = "スパーク寿命", Description = "破片エフェクトの表示秒数。")]
    [AnimationSlider("F2", "秒", 0, 2)]
    public Animation HitEffectLifetime { get; } = new Animation(0.35, 0, 1000);

    [Display(GroupName = "当たり判定", Name = "自機マーカー", Description = "自機画像や当たり判定枠 (喰らい判定) を描画します。")]
    [ToggleSlider]
    public bool ShowTargetMarker { get => showTargetMarker; set => Set(ref showTargetMarker, value); }
    private bool showTargetMarker = true;

    // =====================================================================
    // 自機ショット
    // =====================================================================

    [Display(GroupName = "自機ショット", Name = "自機射撃", Description = "自機 (ターゲット) からショットを発射します。")]
    [ToggleSlider]
    public bool PlayerShotEnabled { get => playerShotEnabled; set => Set(ref playerShotEnabled, value); }
    private bool playerShotEnabled;

    [Display(GroupName = "自機ショット", Name = "ショット種別", Description = "正面集中・ワイド・多重・ホーミング・全方位から選択。")]
    [EnumComboBox]
    public PlayerShotType PlayerShotType { get => playerShotType; set => Set(ref playerShotType, value); }
    private PlayerShotType playerShotType = PlayerShotType.FocusStraight;

    [Display(GroupName = "自機ショット", Name = "自機弾画像", Description = "ユーザー指定の画像 (PNG等) を自機弾として発射します。未指定時は組み込み形状になります。")]
    [FileSelector(YukkuriMovieMaker.Settings.FileGroupType.ImageItem)]
    public string PlayerShotImagePath
    {
        get => playerShotImagePath;
        set => Set(ref playerShotImagePath, value ?? string.Empty);
    }
    private string playerShotImagePath = string.Empty;

    public bool HasCustomPlayerShotImage => !string.IsNullOrWhiteSpace(PlayerShotImagePath);

    [Display(GroupName = "自機ショット", Name = "自機Way数", Description = "同時に発射する弾数。0 で射撃休止。")]
    [AnimationSlider("F0", "本", -16, 128)]
    public Animation PlayerShotWay { get; } = new Animation(2, -128, 128);

    [Display(GroupName = "自機ショット", Name = "射撃間隔", Description = "発射間隔 (秒)。0.08 で秒間約 12 回連射。0 以下で射撃休止。")]
    [AnimationSlider("F3", "秒", -1.0, 10.0)]
    public Animation PlayerShotInterval { get; } = new Animation(0.08, -100.0, 100.0);

    [Display(GroupName = "自機ショット", Name = "ショット弾速", Description = "自機弾の飛行スピード (px/秒)。")]
    [AnimationSlider("F0", "px/秒", -3000, 3000)]
    public Animation PlayerShotSpeed { get; } = new Animation(1200, -10000, 10000);

    [Display(GroupName = "自機ショット", Name = "ショット拡散角", Description = "発射角の広がり。0 で平行に並んで直進します。")]
    [AnimationSlider("F1", "度", -360, 360)]
    public Animation PlayerShotSpread { get; } = new Animation(15, -360, 360);

    [Display(GroupName = "自機ショット", Name = "ショットサイズ", Description = "自機弾の大きさ倍率。")]
    [AnimationSlider("F2", "倍", -10, 10)]
    public Animation PlayerShotScale { get; } = new Animation(1.0, -1000, 1000);

    [Display(GroupName = "自機ショット", Name = "弾向き追従", Description = "自機弾の進行方向に向きを合わせます。")]
    [ToggleSlider]
    public bool PlayerShotAlignToDirection { get => playerShotAlignToDirection; set => Set(ref playerShotAlignToDirection, value); }
    private bool playerShotAlignToDirection = true;

    [Display(GroupName = "自機ショット", Name = "ショット色", Description = "自機弾の色。")]
    [ColorPicker]
    public Color PlayerShotColor { get => playerShotColor; set => Set(ref playerShotColor, value); }
    private Color playerShotColor = Color.FromArgb(255, 255, 255, 255);

    [Display(GroupName = "自機ショット", Name = "ショット発光", Description = "自機弾を加算合成で発光させます。")]
    [ToggleSlider]
    public bool PlayerShotAdditive { get => playerShotAdditive; set => Set(ref playerShotAdditive, value); }
    private bool playerShotAdditive = true;

    [Display(GroupName = "自機ショット", Name = "自動照準", Description = "ボスの方向へ自動で狙いを定めて発射します。")]
    [ToggleSlider]
    public bool PlayerShotAutoAim { get => playerShotAutoAim; set => Set(ref playerShotAutoAim, value); }
    private bool playerShotAutoAim;

    [Display(GroupName = "自機ショット", Name = "ショット判定", Description = "自機弾の被弾判定半径 (px)。")]
    [AnimationSlider("F1", "px", -100, 500)]
    public Animation PlayerShotHitRadius { get; } = new Animation(12, -1000, 1000);

    [Display(GroupName = "自機ショット", Name = "命中時消滅", Description = "敵に命中した自機弾を消滅させます。")]
    [ToggleSlider]
    public bool PlayerShotDestroyOnHit { get => playerShotDestroyOnHit; set => Set(ref playerShotDestroyOnHit, value); }
    private bool playerShotDestroyOnHit = true;

    [Display(GroupName = "自機ショット", Name = "敵弾相殺", Description = "自機ショットが敵弾と接触した際に敵弾を消滅させます。")]
    [ToggleSlider]
    public bool PlayerShotCancelEnemyBullets { get => playerShotCancelEnemyBullets; set => Set(ref playerShotCancelEnemyBullets, value); }
    private bool playerShotCancelEnemyBullets;

    [Display(GroupName = "自機ショット", Name = "対象ch", Description = "当たり判定・自動照準の相手となる弾幕アイテムのチャンネル番号 (-1 で全チャンネル対象)。")]
    [TextBoxSlider("F0", "ch", -1, 255)]
    public int PlayerShotTargetChannel { get => playerShotTargetChannel; set => Set(ref playerShotTargetChannel, value); }
    private int playerShotTargetChannel = -1;

    // =====================================================================
    // ボス体力バー (HP ゲージ)
    // =====================================================================

    [Display(GroupName = "体力バー", Name = "体力バー", Description = "ボス体力バーを表示します。")]
    [ToggleSlider]
    public bool HpBarEnabled { get => hpBarEnabled; set => Set(ref hpBarEnabled, value); }
    private bool hpBarEnabled;

    [Display(GroupName = "体力バー", Name = "ゲージ形状", Description = "円形リング・画面上部バーから選択。")]
    [EnumComboBox]
    public HpBarStyle HpBarStyle { get => hpBarStyle; set => Set(ref hpBarStyle, value); }
    private HpBarStyle hpBarStyle = HpBarStyle.CircularRing;

    [Display(GroupName = "体力バー", Name = "現在HP", Description = "現在のHP残量割合 (100%〜0%)。タイムラインでアニメーション可能です。")]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation BossHp { get; } = new Animation(100.0, 0, 100);

    [Display(GroupName = "体力バー", Name = "最大HP", Description = "ボスの最大HP実数値。")]
    [TextBoxSlider("F0", "HP", 1, 1000000)]
    public double BossMaxHp { get => bossMaxHp; set => Set(ref bossMaxHp, value); }
    private double bossMaxHp = 1000.0;

    [Display(GroupName = "体力バー", Name = "被弾ダメージ", Description = "自機ショット 1 発あたりのダメージ実数値。")]
    [TextBoxSlider("F1", "ダメージ", 0, 10000)]
    public double DamagePerHit { get => damagePerHit; set => Set(ref damagePerHit, value); }
    private double damagePerHit = 15.0;

    [Display(GroupName = "体力バー", Name = "リング半径", Description = "円形ゲージの半径 (px)。")]
    [AnimationSlider("F0", "px", 10, 800)]
    public Animation HpBarRadius { get; } = new Animation(140, -10000, 10000);

    [Display(GroupName = "体力バー", Name = "バー横幅", Description = "横長バーの横幅 (px)。")]
    [AnimationSlider("F0", "px", 50, 3840)]
    public Animation HpBarWidth { get; } = new Animation(800, -10000, 10000);

    [Display(GroupName = "体力バー", Name = "バー高さ", Description = "横長バーの太さ/高さ (px)。")]
    [AnimationSlider("F0", "px", 2, 100)]
    public Animation HpBarHeight { get; } = new Animation(16, -10000, 10000);

    [Display(GroupName = "体力バー", Name = "バー X", Description = "横長バーの X 座標。")]
    [AnimationSlider("F0", "px", -1920, 1920)]
    public Animation HpBarX { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "体力バー", Name = "バー Y", Description = "画面上部バーの Y 座標 (通常 -480 付近)。")]
    [AnimationSlider("F0", "px", -1080, 1080)]
    public Animation HpBarY { get; } = new Animation(-480, -100000, 100000);

    [Display(GroupName = "体力バー", Name = "ゲージ太さ", Description = "ゲージ枠線の太さ (px)。")]
    [TextBoxSlider("F1", "px", 1, 50)]
    public double HpBarThickness { get => hpBarThickness; set => Set(ref hpBarThickness, value); }
    private double hpBarThickness = 6.0;

    [Display(GroupName = "体力バー", Name = "通常色", Description = "通常時のゲージ色。")]
    [ColorPicker]
    public Color HpBarColor { get => hpBarColor; set => Set(ref hpBarColor, value); }
    private Color hpBarColor = Color.FromArgb(255, 60, 220, 100);

    [Display(GroupName = "体力バー", Name = "警告色", Description = "HP 25% 以下の危険警告色。")]
    [ColorPicker]
    public Color HpBarDangerColor { get => hpBarDangerColor; set => Set(ref hpBarDangerColor, value); }
    private Color hpBarDangerColor = Color.FromArgb(255, 240, 50, 50);

    [Display(GroupName = "体力バー", Name = "ラグ色", Description = "被弾追従ラグバーの色。")]
    [ColorPicker]
    public Color HpBarDamageLagColor { get => hpBarDamageLagColor; set => Set(ref hpBarDamageLagColor, value); }
    private Color hpBarDamageLagColor = Color.FromArgb(230, 255, 230, 80);

    [Display(GroupName = "体力バー", Name = "背景色", Description = "ゲージ背景枠の色。")]
    [ColorPicker]
    public Color HpBarBackgroundColor { get => hpBarBackgroundColor; set => Set(ref hpBarBackgroundColor, value); }
    private Color hpBarBackgroundColor = Color.FromArgb(180, 25, 25, 40);

    [Display(GroupName = "体力バー", Name = "星マーク数", Description = "ゲージ上に表示するフェーズ区切り目 (1〜10)。")]
    [TextBoxSlider("F0", "段階", 1, 10)]
    public int HpBarPhaseCount { get => hpBarPhaseCount; set => Set(ref hpBarPhaseCount, value); }
    private int hpBarPhaseCount = 3;

    [Display(GroupName = "体力バー", Name = "発光グロー", Description = "ゲージの発光グロー効果。")]
    [ToggleSlider]
    public bool HpBarGlow { get => hpBarGlow; set => Set(ref hpBarGlow, value); }
    private bool hpBarGlow = true;

    [Display(GroupName = "体力バー", Name = "ゲージ濃度", Description = "ゲージ全体の不透明度 (0〜100%)。")]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation HpBarOpacity { get; } = new Animation(100.0, 0, 100);

    // =====================================================================
    // 全体設定
    // =====================================================================

    [Display(GroupName = "全体", Name = "シード値", Description = "同じシードなら常に同じ弾幕になります。値を変えると弾のばらけ方が変わります。")]
    [AnimationSlider("F0", "", 0, 100000)]
    public Animation Seed { get; } = new Animation(20240101, 0, 10000000);

    [Display(GroupName = "全体設定", Name = "最大弾数", Description = "同時に画面上に存在できる弾の最大数。")]
    [AnimationSlider("F0", "発", 0, 500000)]
    public Animation MaxBullets { get; } = new Animation(100000, 0, 500000);

    [Display(GroupName = "全体", Name = "再生速度", Description = "弾幕全体の時間倍率。0 で完全静止 (時止め)、0.5 でスローモーションになります。")]
    [AnimationSlider("F2", "倍", -3, 3)]
    public Animation TimeScale { get; } = new Animation(1.0, -100, 100);

    [Display(GroupName = "全体", Name = "計算精度", Description = "物理計算 1 ステップの時間。小さいほど正確ですが重くなります。")]
    [EnumComboBox]
    public SimulationStep SimulationStep { get => simulationStep; set => Set(ref simulationStep, value); }
    private SimulationStep simulationStep = SimulationStep.Hz120;

    [Display(GroupName = "全体", Name = "画面外処理", Description = "画面外に出た弾の挙動 (消滅・バウンド・画面端ループ)。")]
    [EnumComboBox]
    public OutOfBoundsBehavior OutOfBounds { get => outOfBounds; set => Set(ref outOfBounds, value); }
    private OutOfBoundsBehavior outOfBounds = OutOfBoundsBehavior.Destroy;

    [Display(GroupName = "全体", Name = "画面外余白", Description = "画面の外側にこの距離ぶん余裕を持たせ、その外へ出た弾を処理します。")]
    [AnimationSlider("F0", "px", -2000, 2000)]
    public Animation BoundsMargin { get; } = new Animation(160, -100000, 100000);

    [Display(GroupName = "全体", Name = "全体濃度", Description = "弾幕全体の不透明度 (0〜100%)。")]
    [AnimationSlider("F1", "%", -100, 100)]
    public Animation GlobalOpacity { get; } = new Animation(100, -100, 100);

    [Display(GroupName = "全体", Name = "効果音ch",
        Description = "「弾幕効果音」音声エフェクト側で同じ番号を指定すると、この弾幕に合わせて効果音が鳴ります。-1 で全チャンネル連動。")]
    [AnimationSlider("F0", "ch", -1, 255)]
    public Animation Channel { get; } = new Animation(0, -1, 255);

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
        SubscribeAnimatable(GlobalOpacity, nameof(GlobalOpacity));
        SubscribeAnimatable(Channel, nameof(Channel));
        SubscribeAnimatable(BossHp, nameof(BossHp));
        SubscribeAnimatable(HpBarRadius, nameof(HpBarRadius));
        SubscribeAnimatable(HpBarWidth, nameof(HpBarWidth));
        SubscribeAnimatable(HpBarHeight, nameof(HpBarHeight));
        SubscribeAnimatable(HpBarX, nameof(HpBarX));
        SubscribeAnimatable(HpBarY, nameof(HpBarY));
        SubscribeAnimatable(HpBarOpacity, nameof(HpBarOpacity));

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
                EnemyHitEnabled = EnemyHitEnabled,
                EnemyRadius = EnemyRadius.GetFirstValue(),
                SpawnHitEffect = SpawnHitEffect,
                HitEffectCount = Math.Max(0, (int)Math.Round(HitEffectCount.GetFirstValue())),
                HitEffectSpeed = HitEffectSpeed.GetFirstValue(),
                HitEffectLifetime = Math.Max(0.01, HitEffectLifetime.GetFirstValue()),
            },

            PlayerShot = new PlayerShotSettings
            {
                IsEnabled = PlayerShotEnabled,
                ShotType = PlayerShotType,
                ImagePath = PlayerShotImagePath,
                Way = (int)Math.Round(PlayerShotWay.GetFirstValue()),
                FireInterval = PlayerShotInterval.GetFirstValue(),
                Speed = PlayerShotSpeed.GetFirstValue(),
                SpreadAngle = PlayerShotSpread.GetFirstValue(),
                Scale = PlayerShotScale.GetFirstValue(),
                AlignToDirection = PlayerShotAlignToDirection,
                Color = ColorExtensions.ToBulletColor(PlayerShotColor),
                Additive = PlayerShotAdditive,
                AutoAim = PlayerShotAutoAim,
                HitRadius = PlayerShotHitRadius.GetFirstValue(),
                DestroyOnHit = PlayerShotDestroyOnHit,
                CancelEnemyBullets = PlayerShotCancelEnemyBullets,
                TargetChannel = PlayerShotTargetChannel,
            },

            HpBar = new BossHpBarSettings
            {
                Enabled = HpBarEnabled,
                Style = HpBarStyle,
                MaxHp = BossMaxHp,
                InitialHpPercentage = BossHp.GetFirstValue(),
                DamagePerHit = DamagePerHit,
                Radius = HpBarRadius.GetFirstValue(),
                Width = HpBarWidth.GetFirstValue(),
                Height = HpBarHeight.GetFirstValue(),
                X = HpBarX.GetFirstValue(),
                Y = HpBarY.GetFirstValue(),
                Thickness = HpBarThickness,
                BarColor = ColorExtensions.ToBulletColor(HpBarColor),
                DangerColor = ColorExtensions.ToBulletColor(HpBarDangerColor),
                DamageLagColor = ColorExtensions.ToBulletColor(HpBarDamageLagColor),
                BackgroundColor = ColorExtensions.ToBulletColor(HpBarBackgroundColor),
                PhaseCount = HpBarPhaseCount,
                Glow = HpBarGlow,
                Opacity = HpBarOpacity.GetFirstValue(),
            },

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
        yield return EnemyRadius;
        yield return HitEffectCount;
        yield return HitEffectSpeed;
        yield return HitEffectLifetime;
        yield return PlayerShotWay;
        yield return PlayerShotInterval;
        yield return PlayerShotSpeed;
        yield return PlayerShotSpread;
        yield return PlayerShotScale;
        yield return PlayerShotHitRadius;
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
        private readonly Animation maxBullets = new(100000, 0, 500000);
        private readonly Animation timeScale = new(1.0, -100, 100);
        private readonly SimulationStep simulationStep;
        private readonly OutOfBoundsBehavior outOfBounds;
        private readonly Animation boundsMargin = new(160, -100000, 100000);
        private readonly Animation globalOpacity = new(100, -100, 100);
        private readonly Animation channel = new(0, -1, 255);

        private readonly bool collisionEnabled;
        private readonly Animation targetX = new(0, -100000, 100000);
        private readonly Animation targetY = new(250, -100000, 100000);
        private readonly string targetImagePath = string.Empty;
        private readonly Animation targetScale = new(1.0, -1000, 1000);
        private readonly Animation targetRotation = new(0, -100000, 100000);
        private readonly Animation targetOpacity = new(1.0, -1, 1);
        private readonly Animation targetRadius = new(30, -10000, 10000);
        private readonly bool enemyHitEnabled = true;
        private readonly Animation enemyRadius = new(40, -10000, 10000);
        private readonly bool spawnHitEffect;
        private readonly Animation hitEffectCount = new(8, 0, 500);
        private readonly Animation hitEffectSpeed = new(160, -100000, 100000);
        private readonly Animation hitEffectLifetime = new(0.35, 0, 1000);
        private readonly bool showTargetMarker;
        private readonly bool showControllers = true;

        private readonly bool playerShotEnabled;
        private readonly PlayerShotType playerShotType;
        private readonly string playerShotImagePath = string.Empty;
        private readonly Animation playerShotWay = new(2, -128, 128);
        private readonly Animation playerShotInterval = new(0.08, -100.0, 100.0);
        private readonly Animation playerShotSpeed = new(1200, -10000, 10000);
        private readonly Animation playerShotSpread = new(15, -360, 360);
        private readonly Animation playerShotScale = new(1.0, -1000, 1000);
        private readonly bool playerShotAlignToDirection = true;
        private readonly Color playerShotColor = Color.FromArgb(255, 255, 255, 255);
        private readonly bool playerShotAdditive = true;
        private readonly bool playerShotAutoAim;
        private readonly Animation playerShotHitRadius = new(12, -1000, 1000);
        private readonly bool playerShotDestroyOnHit = true;
        private readonly bool playerShotCancelEnemyBullets;
        private readonly int playerShotTargetChannel = -1;

        private readonly bool hpBarEnabled;
        private readonly HpBarStyle hpBarStyle;
        private readonly Animation bossHp = new(100.0, 0, 100);
        private readonly double bossMaxHp = 1000.0;
        private readonly double damagePerHit = 15.0;
        private readonly Animation hpBarRadius = new(140, -10000, 10000);
        private readonly Animation hpBarWidth = new(800, -10000, 10000);
        private readonly Animation hpBarHeight = new(16, -10000, 10000);
        private readonly Animation hpBarX = new(0, -100000, 100000);
        private readonly Animation hpBarY = new(-480, -100000, 100000);
        private readonly double hpBarThickness = 6.0;
        private readonly Color hpBarColor = Color.FromArgb(255, 60, 220, 100);
        private readonly Color hpBarDangerColor = Color.FromArgb(255, 240, 50, 50);
        private readonly Color hpBarDamageLagColor = Color.FromArgb(230, 255, 230, 80);
        private readonly Color hpBarBackgroundColor = Color.FromArgb(180, 25, 25, 40);
        private readonly int hpBarPhaseCount = 3;
        private readonly bool hpBarGlow = true;
        private readonly Animation hpBarOpacity = new(100.0, 0, 100);

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
            enemyHitEnabled = source.EnemyHitEnabled;
            enemyRadius.CopyFrom(source.EnemyRadius);
            spawnHitEffect = source.SpawnHitEffect;
            hitEffectCount.CopyFrom(source.HitEffectCount);
            hitEffectSpeed.CopyFrom(source.HitEffectSpeed);
            hitEffectLifetime.CopyFrom(source.HitEffectLifetime);
            showTargetMarker = source.ShowTargetMarker;
            showControllers = source.ShowControllers;

            playerShotEnabled = source.PlayerShotEnabled;
            playerShotType = source.PlayerShotType;
            playerShotImagePath = source.PlayerShotImagePath;
            playerShotWay.CopyFrom(source.PlayerShotWay);
            playerShotInterval.CopyFrom(source.PlayerShotInterval);
            playerShotSpeed.CopyFrom(source.PlayerShotSpeed);
            playerShotSpread.CopyFrom(source.PlayerShotSpread);
            playerShotScale.CopyFrom(source.PlayerShotScale);
            playerShotAlignToDirection = source.PlayerShotAlignToDirection;
            playerShotColor = source.PlayerShotColor;
            playerShotAdditive = source.PlayerShotAdditive;
            playerShotAutoAim = source.PlayerShotAutoAim;
            playerShotHitRadius.CopyFrom(source.PlayerShotHitRadius);
            playerShotDestroyOnHit = source.PlayerShotDestroyOnHit;
            playerShotCancelEnemyBullets = source.PlayerShotCancelEnemyBullets;
            playerShotTargetChannel = source.PlayerShotTargetChannel;

            hpBarEnabled = source.HpBarEnabled;
            hpBarStyle = source.HpBarStyle;
            bossHp.CopyFrom(source.BossHp);
            bossMaxHp = source.BossMaxHp;
            damagePerHit = source.DamagePerHit;
            hpBarRadius.CopyFrom(source.HpBarRadius);
            hpBarWidth.CopyFrom(source.HpBarWidth);
            hpBarHeight.CopyFrom(source.HpBarHeight);
            hpBarX.CopyFrom(source.HpBarX);
            hpBarY.CopyFrom(source.HpBarY);
            hpBarThickness = source.HpBarThickness;
            hpBarColor = source.HpBarColor;
            hpBarDangerColor = source.HpBarDangerColor;
            hpBarDamageLagColor = source.HpBarDamageLagColor;
            hpBarBackgroundColor = source.HpBarBackgroundColor;
            hpBarPhaseCount = source.HpBarPhaseCount;
            hpBarGlow = source.HpBarGlow;
            hpBarOpacity.CopyFrom(source.HpBarOpacity);

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
            target.EnemyHitEnabled = enemyHitEnabled;
            target.EnemyRadius.CopyFrom(enemyRadius);
            target.SpawnHitEffect = spawnHitEffect;
            target.HitEffectCount.CopyFrom(hitEffectCount);
            target.HitEffectSpeed.CopyFrom(hitEffectSpeed);
            target.HitEffectLifetime.CopyFrom(hitEffectLifetime);
            target.ShowTargetMarker = showTargetMarker;
            target.ShowControllers = showControllers;

            target.PlayerShotEnabled = playerShotEnabled;
            target.PlayerShotType = playerShotType;
            target.PlayerShotImagePath = playerShotImagePath;
            target.PlayerShotWay.CopyFrom(playerShotWay);
            target.PlayerShotInterval.CopyFrom(playerShotInterval);
            target.PlayerShotSpeed.CopyFrom(playerShotSpeed);
            target.PlayerShotSpread.CopyFrom(playerShotSpread);
            target.PlayerShotScale.CopyFrom(playerShotScale);
            target.PlayerShotAlignToDirection = playerShotAlignToDirection;
            target.PlayerShotColor = playerShotColor;
            target.PlayerShotAdditive = playerShotAdditive;
            target.PlayerShotAutoAim = playerShotAutoAim;
            target.PlayerShotHitRadius.CopyFrom(playerShotHitRadius);
            target.PlayerShotDestroyOnHit = playerShotDestroyOnHit;
            target.PlayerShotCancelEnemyBullets = playerShotCancelEnemyBullets;
            target.PlayerShotTargetChannel = playerShotTargetChannel;

            target.HpBarEnabled = hpBarEnabled;
            target.HpBarStyle = hpBarStyle;
            target.BossHp.CopyFrom(bossHp);
            target.BossMaxHp = bossMaxHp;
            target.DamagePerHit = damagePerHit;
            target.HpBarRadius.CopyFrom(hpBarRadius);
            target.HpBarWidth.CopyFrom(hpBarWidth);
            target.HpBarHeight.CopyFrom(hpBarHeight);
            target.HpBarX.CopyFrom(hpBarX);
            target.HpBarY.CopyFrom(hpBarY);
            target.HpBarThickness = hpBarThickness;
            target.HpBarColor = hpBarColor;
            target.HpBarDangerColor = hpBarDangerColor;
            target.HpBarDamageLagColor = hpBarDamageLagColor;
            target.HpBarBackgroundColor = hpBarBackgroundColor;
            target.HpBarPhaseCount = hpBarPhaseCount;
            target.HpBarGlow = hpBarGlow;
            target.HpBarOpacity.CopyFrom(hpBarOpacity);

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
