using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Settings;
using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Model;
using Ymm4DanmakuPlugin.Interop;

namespace Ymm4DanmakuPlugin.Parameters;

/// <summary>
/// 1 つのエミッター (発射源) の編集項目。
/// <para>
/// 開発計画書の 3 階層に対応する。
/// ・第 1 階層: <see cref="Pattern"/> ほかの GUI スライダー<br/>
/// ・第 2 階層: 速度 / 加速度 / ホーミングなどの物理パラメータ<br/>
/// ・第 3 階層: <see cref="SourceMode"/> を切り替えて JSON / BulletML / Lua を読み込む
/// </para>
/// <para>
/// <b>X / Y だけが <see cref="Animation"/></b> なのは意図的である。
/// キーフレームで動かしたい値はここに限られ、それ以外を Animation にすると
/// シミュレーションの再構築判定 (設定署名) が毎フレーム変化して極端に重くなる。
/// </para>
/// </summary>
public class EmitterParameter : Animatable
{
    // =====================================================================
    // 基本
    // =====================================================================

    [Display(GroupName = "エミッター", Name = "名前", Description = "このエミッターの識別名。プリセットの保存名の既定値にも使われます。")]
    [TextEditor]
    public string Name
    {
        get => name;
        set => Set(ref name, value ?? string.Empty);
    }
    private string name = "エミッター";

    [Display(GroupName = "エミッター", Name = "有効", Description = "このエミッターから弾を発射します。")]
    [ToggleSlider]
    public bool IsEnabled { get => isEnabled; set => Set(ref isEnabled, value); }
    private bool isEnabled = true;

    [Display(GroupName = "エミッター", Name = "X", Description = "発射位置 X。キーフレームで動かせます。プレビュー上のドラッグでも変更できます。")]
    [AnimationSlider("F1", "px", -960, 960)]
    public Animation X { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "エミッター", Name = "Y", Description = "発射位置 Y。キーフレームで動かせます。プレビュー上のドラッグでも変更できます。")]
    [AnimationSlider("F1", "px", -540, 540)]
    public Animation Y { get; } = new Animation(-200, -100000, 100000);

    [Display(GroupName = "エミッター", Name = "公転半径", Description = "エミッター自体を円運動させる半径。0 で静止します。")]
    [TextBoxSlider("F1", "px", 0, 600)]
    [DefaultValue(0d)]
    [Range(0, 100000)]
    public double OrbitRadius { get => orbitRadius; set => Set(ref orbitRadius, value); }
    private double orbitRadius;

    [Display(GroupName = "エミッター", Name = "公転速度", Description = "エミッターの円運動の速度。")]
    [TextBoxSlider("F1", "度/秒", -360, 360)]
    [DefaultValue(0d)]
    [Range(-100000, 100000)]
    public double OrbitSpeed { get => orbitSpeed; set => Set(ref orbitSpeed, value); }
    private double orbitSpeed;

    [Display(GroupName = "エミッター", Name = "公転位相", Description = "公転の初期角度。")]
    [TextBoxSlider("F1", "度", 0, 360)]
    [DefaultValue(0d)]
    [Range(-100000, 100000)]
    public double OrbitPhase { get => orbitPhase; set => Set(ref orbitPhase, value); }
    private double orbitPhase;

    [Display(GroupName = "エミッター", Name = "シードずらし", Description = "同じ設定のエミッターを複数置いたとき、乱数をずらして重なりを防ぎます。")]
    [TextBoxSlider("F0", "", 0, 100)]
    [DefaultValue(0)]
    [Range(-1000000, 1000000)]
    public int SeedOffset { get => seedOffset; set => Set(ref seedOffset, value); }
    private int seedOffset;

    // =====================================================================
    // プリセット
    // =====================================================================

    /// <summary>
    /// 選択中のプリセット名。
    /// <para>
    /// この値は「どのプリセットを選んでいたか」を覚えておくだけで、
    /// シミュレーションには一切影響しない。実際の設定はプリセット適用時に
    /// 各プロパティへ展開される (= 適用後に個別調整できる)。
    /// </para>
    /// </summary>
    [Display(GroupName = "プリセット", Name = "プリセット", Description = "東方風のサンプルを選んで [適用]。保存・読み込み・書き出しもここから行えます。")]
    [Presets.PresetSelector]
    public string PresetName
    {
        get => presetName;
        set => Set(ref presetName, value ?? string.Empty);
    }
    private string presetName = string.Empty;

    // =====================================================================
    // 弾幕データの供給元 (3 階層の切り替え)
    // =====================================================================

    [Display(GroupName = "弾幕データ", Name = "生成方法", Description = "パターン(GUI) / JSON / BulletML / Lua から選びます。")]
    [EnumComboBox]
    public DanmakuSourceMode SourceMode { get => sourceMode; set => Set(ref sourceMode, value); }
    private DanmakuSourceMode sourceMode = DanmakuSourceMode.Pattern;

    [Display(GroupName = "弾幕データ", Name = "ファイル", Description = "JSON / BulletML(XML) / Lua ファイルのパス。「本文」が空のときに使用されます。")]
    [FileSelector(FileGroupType.None)]
    public string SourcePath { get => sourcePath; set => Set(ref sourcePath, value); }
    private string sourcePath = string.Empty;

    [Display(GroupName = "弾幕データ", Name = "本文", Description = "外部ファイルを使わず直接記述する場合はこちらへ。ファイルより優先されます。")]
    [TextEditor(AcceptsReturn = true, PropertyEditorSize = PropertyEditorSize.FullWidth)]
    public string SourceText { get => sourceText; set => Set(ref sourceText, value); }
    private string sourceText = string.Empty;

    [Display(GroupName = "弾幕データ", Name = "速度換算", Description = "BulletML の速度 1 を何 px/秒 とみなすか。60 で「1px/フレーム(60fps)」相当です。")]
    [TextBoxSlider("F0", "px/秒", 1, 240)]
    [DefaultValue(60d)]
    [Range(0.01, 100000)]
    public double ScriptSpeedScale { get => scriptSpeedScale; set => Set(ref scriptSpeedScale, value); }
    private double scriptSpeedScale = 60;

    [Display(GroupName = "弾幕データ", Name = "難易度($rank)", Description = "BulletML の $rank に渡す値 (0〜1)。")]
    [TextBoxSlider("F2", "", 0, 1)]
    [DefaultValue(0.5d)]
    [Range(0, 1)]
    public double ScriptRank { get => scriptRank; set => Set(ref scriptRank, value); }
    private double scriptRank = 0.5;

    [Display(GroupName = "弾幕データ", Name = "繰り返し", Description = "スクリプトが終端に達したら最初から再生します。")]
    [ToggleSlider]
    public bool ScriptLoop { get => scriptLoop; set => Set(ref scriptLoop, value); }
    private bool scriptLoop = true;

    // =====================================================================
    // 第 1 階層: パターン形状
    // =====================================================================

    [Display(GroupName = "パターン", Name = "種類", Description = "弾の並べ方。")]
    [EnumComboBox]
    public PatternKind PatternKind { get => patternKind; set => Set(ref patternKind, value); }
    private PatternKind patternKind = Core.Configuration.PatternKind.Circle;

    [Display(GroupName = "パターン", Name = "way数", Description = "1 回の発射で撃つ弾の本数。")]
    [TextBoxSlider("F0", "本", 1, 72)]
    [DefaultValue(16)]
    [Range(1, 2000)]
    public int Way { get => way; set => Set(ref way, value); }
    private int way = 16;

    [Display(GroupName = "パターン", Name = "段数", Description = "速度差をつけて重ねる同心円の段数。way数 × 段数 が 1 回の発射数になります。")]
    [TextBoxSlider("F0", "段", 1, 12)]
    [DefaultValue(1)]
    [Range(1, 200)]
    public int Stack { get => stack; set => Set(ref stack, value); }
    private int stack = 1;

    [Display(GroupName = "パターン", Name = "段ごとの速度差")]
    [TextBoxSlider("F0", "px/秒", -200, 200)]
    [DefaultValue(40d)]
    [Range(-100000, 100000)]
    public double StackSpeedStep { get => stackSpeedStep; set => Set(ref stackSpeedStep, value); }
    private double stackSpeedStep = 40;

    [Display(GroupName = "パターン", Name = "段ごとの角度差")]
    [TextBoxSlider("F1", "度", -90, 90)]
    [DefaultValue(0d)]
    [Range(-100000, 100000)]
    public double StackAngleStep { get => stackAngleStep; set => Set(ref stackAngleStep, value); }
    private double stackAngleStep;

    [Display(GroupName = "パターン", Name = "基準角", Description = "発射の中心方向。キーフレームで自由に回転させられます。0 で下向き。")]
    [AnimationSlider("F1", "度", -360, 360)]
    public Animation BaseAngle { get; } = new Animation(-90, -100000, 100000);

    [Display(GroupName = "パターン", Name = "広がり角", Description = "弾を配置する扇の角度。360 で全方位。")]
    [TextBoxSlider("F1", "度", 0, 360)]
    [DefaultValue(360d)]
    [Range(0, 100000)]
    public double SpreadAngle { get => spreadAngle; set => Set(ref spreadAngle, value); }
    private double spreadAngle = 360;

    [Display(GroupName = "パターン", Name = "発射ごとの回転", Description = "1 回発射するたびに基準角へ加算する角度。螺旋弾の要です。")]
    [TextBoxSlider("F2", "度", -60, 60)]
    [DefaultValue(7d)]
    [Range(-100000, 100000)]
    public double AngleStepPerShot { get => angleStepPerShot; set => Set(ref angleStepPerShot, value); }
    private double angleStepPerShot = 7;

    [Display(GroupName = "パターン", Name = "角度ゆらぎ", Description = "基準角にかけるランダム幅 (±)。")]
    [TextBoxSlider("F1", "度", 0, 90)]
    [DefaultValue(0d)]
    [Range(0, 100000)]
    public double AngleJitter { get => angleJitter; set => Set(ref angleJitter, value); }
    private double angleJitter;

    [Display(GroupName = "パターン", Name = "発射間隔")]
    [TextBoxSlider("F3", "秒", 0.01, 2)]
    [DefaultValue(0.1d)]
    [Range(0.001, 10000)]
    public double FireInterval { get => fireInterval; set => Set(ref fireInterval, value); }
    private double fireInterval = 0.1;

    [Display(GroupName = "パターン", Name = "連射数", Description = "ひとかたまり (バースト) あたりの発射回数。")]
    [TextBoxSlider("F0", "回", 1, 20)]
    [DefaultValue(1)]
    [Range(1, 1000)]
    public int BurstCount { get => burstCount; set => Set(ref burstCount, value); }
    private int burstCount = 1;

    [Display(GroupName = "パターン", Name = "連射間隔")]
    [TextBoxSlider("F3", "秒", 0.005, 0.5)]
    [DefaultValue(0.02d)]
    [Range(0.001, 10000)]
    public double BurstInterval { get => burstInterval; set => Set(ref burstInterval, value); }
    private double burstInterval = 0.02;

    [Display(GroupName = "パターン", Name = "連射後の待機")]
    [TextBoxSlider("F3", "秒", 0, 3)]
    [DefaultValue(0d)]
    [Range(0, 10000)]
    public double BurstCooldown { get => burstCooldown; set => Set(ref burstCooldown, value); }
    private double burstCooldown;

    [Display(GroupName = "パターン", Name = "発射開始", Description = "アイテム先頭からの相対時間。")]
    [TextBoxSlider("F2", "秒", 0, 10)]
    [DefaultValue(0d)]
    [Range(0, 100000)]
    public double StartTime { get => startTime; set => Set(ref startTime, value); }
    private double startTime;

    [Display(GroupName = "パターン", Name = "発射終了", Description = "0 でアイテム終端まで撃ち続けます。")]
    [TextBoxSlider("F2", "秒", 0, 60)]
    [DefaultValue(0d)]
    [Range(0, 100000)]
    public double EndTime { get => endTime; set => Set(ref endTime, value); }
    private double endTime;

    [Display(GroupName = "パターン", Name = "発生半径", Description = "発射位置からこの距離だけ離した位置に弾を出します。")]
    [TextBoxSlider("F1", "px", 0, 300)]
    [DefaultValue(0d)]
    [Range(0, 100000)]
    public double SpawnRadius { get => spawnRadius; set => Set(ref spawnRadius, value); }
    private double spawnRadius;

    [Display(GroupName = "パターン", Name = "発生位置ゆらぎ")]
    [TextBoxSlider("F1", "px", 0, 200)]
    [DefaultValue(0d)]
    [Range(0, 100000)]
    public double SpawnJitter { get => spawnJitter; set => Set(ref spawnJitter, value); }
    private double spawnJitter;

    [Display(GroupName = "パターン", Name = "自機狙い", Description = "基準角をターゲット方向に合わせます。")]
    [ToggleSlider]
    public bool AimAtTarget { get => aimAtTarget; set => Set(ref aimAtTarget, value); }
    private bool aimAtTarget;

    [Display(GroupName = "パターン", Name = "壁の横幅", Description = "「壁弾」で弾を並べる横幅。")]
    [TextBoxSlider("F0", "px", 100, 3840)]
    [DefaultValue(1280d)]
    [Range(1, 100000)]
    public double WallWidth { get => wallWidth; set => Set(ref wallWidth, value); }
    private double wallWidth = 1280;

    [Display(GroupName = "パターン", Name = "レーザー間隔", Description = "「疑似レーザー」で弾を並べる間隔。")]
    [TextBoxSlider("F1", "px", 4, 120)]
    [DefaultValue(24d)]
    [Range(0.1, 100000)]
    public double LaserSpacing { get => laserSpacing; set => Set(ref laserSpacing, value); }
    private double laserSpacing = 24;

    [Display(GroupName = "パターン", Name = "鞭の振れ幅")]
    [TextBoxSlider("F1", "度", 0, 180)]
    [DefaultValue(60d)]
    [Range(0, 100000)]
    public double WhipAmplitude { get => whipAmplitude; set => Set(ref whipAmplitude, value); }
    private double whipAmplitude = 60;

    [Display(GroupName = "パターン", Name = "鞭の周期")]
    [TextBoxSlider("F2", "秒", 0.1, 6)]
    [DefaultValue(1.2d)]
    [Range(0.01, 100000)]
    public double WhipPeriod { get => whipPeriod; set => Set(ref whipPeriod, value); }
    private double whipPeriod = 1.2;

    // =====================================================================
    // 第 2 階層: 物理
    // =====================================================================

    [Display(GroupName = "弾の動き", Name = "初速")]
    [TextBoxSlider("F0", "px/秒", 0, 900)]
    [DefaultValue(220d)]
    [Range(-100000, 100000)]
    public double Speed { get => speed; set => Set(ref speed, value); }
    private double speed = 220;

    [Display(GroupName = "弾の動き", Name = "初速ゆらぎ")]
    [TextBoxSlider("F0", "px/秒", 0, 300)]
    [DefaultValue(0d)]
    [Range(0, 100000)]
    public double SpeedJitter { get => speedJitter; set => Set(ref speedJitter, value); }
    private double speedJitter;

    [Display(GroupName = "弾の動き", Name = "弾ごとの速度差", Description = "n-way の内側と外側で速度差をつけます。")]
    [TextBoxSlider("F1", "px/秒", -50, 50)]
    [DefaultValue(0d)]
    [Range(-100000, 100000)]
    public double SpeedStep { get => speedStep; set => Set(ref speedStep, value); }
    private double speedStep;

    [Display(GroupName = "弾の動き", Name = "加速度")]
    [TextBoxSlider("F1", "px/秒²", -400, 400)]
    [DefaultValue(0d)]
    [Range(-1000000, 1000000)]
    public double Acceleration { get => acceleration; set => Set(ref acceleration, value); }
    private double acceleration;

    [Display(GroupName = "弾の動き", Name = "旋回速度", Description = "正で時計回りに曲がります。")]
    [TextBoxSlider("F1", "度/秒", -360, 360)]
    [DefaultValue(0d)]
    [Range(-100000, 100000)]
    public double AngularVelocity { get => angularVelocity; set => Set(ref angularVelocity, value); }
    private double angularVelocity;

    [Display(GroupName = "弾の動き", Name = "旋回ゆらぎ")]
    [TextBoxSlider("F1", "度/秒", 0, 180)]
    [DefaultValue(0d)]
    [Range(0, 100000)]
    public double AngularVelocityJitter { get => angularVelocityJitter; set => Set(ref angularVelocityJitter, value); }
    private double angularVelocityJitter;

    [Display(GroupName = "弾の動き", Name = "減速", Description = "1 秒後に残る速度の割合。1 で減速なし。")]
    [TextBoxSlider("F2", "", 0, 1)]
    [DefaultValue(1d)]
    [Range(0, 1)]
    public double Damping { get => damping; set => Set(ref damping, value); }
    private double damping = 1.0;

    [Display(GroupName = "弾の動き", Name = "最低速度")]
    [TextBoxSlider("F0", "px/秒", 0, 300)]
    [DefaultValue(0d)]
    [Range(0, 100000)]
    public double MinSpeed { get => minSpeed; set => Set(ref minSpeed, value); }
    private double minSpeed;

    [Display(GroupName = "弾の動き", Name = "最高速度")]
    [TextBoxSlider("F0", "px/秒", 100, 3000)]
    [DefaultValue(2000d)]
    [Range(0, 1000000)]
    public double MaxSpeed { get => maxSpeed; set => Set(ref maxSpeed, value); }
    private double maxSpeed = 2000;

    [Display(GroupName = "弾の動き", Name = "重力", Description = "正で下向き。")]
    [TextBoxSlider("F0", "px/秒²", -600, 600)]
    [DefaultValue(0d)]
    [Range(-1000000, 1000000)]
    public double Gravity { get => gravity; set => Set(ref gravity, value); }
    private double gravity;

    [Display(GroupName = "弾の動き", Name = "風", Description = "正で右向き。")]
    [TextBoxSlider("F0", "px/秒²", -600, 600)]
    [DefaultValue(0d)]
    [Range(-1000000, 1000000)]
    public double Wind { get => wind; set => Set(ref wind, value); }
    private double wind;

    [Display(GroupName = "弾の動き", Name = "寿命", Description = "0 で無限 (画面外に出るまで残ります)。")]
    [TextBoxSlider("F2", "秒", 0, 20)]
    [DefaultValue(6d)]
    [Range(0, 100000)]
    public double Lifetime { get => lifetime; set => Set(ref lifetime, value); }
    private double lifetime = 6.0;

    [Display(GroupName = "弾の動き", Name = "寿命ゆらぎ")]
    [TextBoxSlider("F2", "秒", 0, 5)]
    [DefaultValue(0d)]
    [Range(0, 100000)]
    public double LifetimeJitter { get => lifetimeJitter; set => Set(ref lifetimeJitter, value); }
    private double lifetimeJitter;

    // ---- ホーミング ----

    [Display(GroupName = "ホーミング", Name = "追尾する", Description = "ターゲット (自機) を追いかけます。")]
    [ToggleSlider]
    public bool HomingEnabled { get => homingEnabled; set => Set(ref homingEnabled, value); }
    private bool homingEnabled;

    [Display(GroupName = "ホーミング", Name = "旋回力", Description = "大きいほど鋭く曲がります。")]
    [TextBoxSlider("F0", "度/秒", 0, 720)]
    [DefaultValue(90d)]
    [Range(0, 100000)]
    public double HomingTurnRate { get => homingTurnRate; set => Set(ref homingTurnRate, value); }
    private double homingTurnRate = 90;

    [Display(GroupName = "ホーミング", Name = "追尾時間", Description = "0 で寿命いっぱい追尾します。")]
    [TextBoxSlider("F2", "秒", 0, 10)]
    [DefaultValue(1.5d)]
    [Range(0, 100000)]
    public double HomingDuration { get => homingDuration; set => Set(ref homingDuration, value); }
    private double homingDuration = 1.5;

    [Display(GroupName = "ホーミング", Name = "追尾開始まで")]
    [TextBoxSlider("F2", "秒", 0, 3)]
    [DefaultValue(0d)]
    [Range(0, 100000)]
    public double HomingDelay { get => homingDelay; set => Set(ref homingDelay, value); }
    private double homingDelay;

    // =====================================================================
    // 見た目
    // =====================================================================

    [Display(GroupName = "見た目", Name = "弾の形")]
    [EnumComboBox]
    public BulletShape Shape { get => shape; set => Set(ref shape, value); }
    private BulletShape shape = BulletShape.Circle;

    [Display(GroupName = "見た目", Name = "画像", Description = "指定すると「弾の形」の代わりにこの画像を使います。")]
    [FileSelector(YukkuriMovieMaker.Settings.FileGroupType.ImageItem)]
    public string ImagePath { get => imagePath; set => Set(ref imagePath, value); }
    private string imagePath = string.Empty;

    [Display(GroupName = "見た目", Name = "大きさ")]
    [TextBoxSlider("F2", "倍", 0.1, 4)]
    [DefaultValue(1d)]
    [Range(0.001, 1000)]
    public double Scale { get => scale; set => Set(ref scale, value); }
    private double scale = 1.0;

    [Display(GroupName = "見た目", Name = "大きさゆらぎ")]
    [TextBoxSlider("F2", "倍", 0, 1)]
    [DefaultValue(0d)]
    [Range(0, 1000)]
    public double ScaleJitter { get => scaleJitter; set => Set(ref scaleJitter, value); }
    private double scaleJitter;

    [Display(GroupName = "見た目", Name = "拡縮速度", Description = "1 秒あたりの大きさの変化量。負で縮みます。")]
    [TextBoxSlider("F2", "倍/秒", -2, 2)]
    [DefaultValue(0d)]
    [Range(-1000, 1000)]
    public double ScaleVelocity { get => scaleVelocity; set => Set(ref scaleVelocity, value); }
    private double scaleVelocity;

    [Display(GroupName = "見た目", Name = "回転速度")]
    [TextBoxSlider("F0", "度/秒", -720, 720)]
    [DefaultValue(0d)]
    [Range(-100000, 100000)]
    public double RotationVelocity { get => rotationVelocity; set => Set(ref rotationVelocity, value); }
    private double rotationVelocity;

    [Display(GroupName = "見た目", Name = "進行方向を向く")]
    [ToggleSlider]
    public bool AlignToDirection { get => alignToDirection; set => Set(ref alignToDirection, value); }
    private bool alignToDirection = true;

    [Display(GroupName = "見た目", Name = "色の決め方")]
    [EnumComboBox]
    public ColorMode ColorMode { get => colorMode; set => Set(ref colorMode, value); }
    private ColorMode colorMode = Core.Configuration.ColorMode.Single;

    [Display(GroupName = "見た目", Name = "色1")]
    [ColorPicker]
    public Color PrimaryColor { get => primaryColor; set => Set(ref primaryColor, value); }
    private Color primaryColor = Color.FromRgb(255, 90, 140);

    [Display(GroupName = "見た目", Name = "色2", Description = "「グラデーション」のときの終端色。")]
    [ColorPicker]
    public Color SecondaryColor { get => secondaryColor; set => Set(ref secondaryColor, value); }
    private Color secondaryColor = Color.FromRgb(102, 178, 255);

    [Display(GroupName = "見た目", Name = "色相の速度", Description = "「虹色」のときの色の流れる速さ。")]
    [TextBoxSlider("F0", "度/秒", -720, 720)]
    [DefaultValue(120d)]
    [Range(-100000, 100000)]
    public double HueVelocity { get => hueVelocity; set => Set(ref hueVelocity, value); }
    private double hueVelocity = 120;

    [Display(GroupName = "見た目", Name = "弾ごとの色相差")]
    [TextBoxSlider("F1", "度", 0, 180)]
    [DefaultValue(15d)]
    [Range(-100000, 100000)]
    public double HueStep { get => hueStep; set => Set(ref hueStep, value); }
    private double hueStep = 15;

    [Display(GroupName = "見た目", Name = "発光 (加算合成)", Description = "東方風の光る弾にします。")]
    [ToggleSlider]
    public bool Additive { get => additive; set => Set(ref additive, value); }
    private bool additive = true;

    [Display(GroupName = "見た目", Name = "発光の強さ")]
    [TextBoxSlider("F2", "倍", 0, 3)]
    [DefaultValue(1d)]
    [Range(0, 100)]
    public double GlowIntensity { get => glowIntensity; set => Set(ref glowIntensity, value); }
    private double glowIntensity = 1.0;

    [Display(GroupName = "見た目", Name = "不透明度")]
    [TextBoxSlider("F2", "", 0, 1)]
    [DefaultValue(1d)]
    [Range(0, 1)]
    public double Opacity { get => opacity; set => Set(ref opacity, value); }
    private double opacity = 1.0;

    [Display(GroupName = "見た目", Name = "フェードイン")]
    [TextBoxSlider("F2", "秒", 0, 1)]
    [DefaultValue(0.05d)]
    [Range(0, 1000)]
    public double FadeInDuration { get => fadeInDuration; set => Set(ref fadeInDuration, value); }
    private double fadeInDuration = 0.05;

    [Display(GroupName = "見た目", Name = "フェードアウト")]
    [TextBoxSlider("F2", "秒", 0, 2)]
    [DefaultValue(0.15d)]
    [Range(0, 1000)]
    public double FadeOutDuration { get => fadeOutDuration; set => Set(ref fadeOutDuration, value); }
    private double fadeOutDuration = 0.15;

    // ---- トレイル (残像) ----

    [Display(GroupName = "残像", Name = "残像の数", Description = "0 で残像なし。")]
    [TextBoxSlider("F0", "個", 0, 32)]
    [DefaultValue(0)]
    [Range(0, 48)]
    public int TrailLength { get => trailLength; set => Set(ref trailLength, value); }
    private int trailLength;

    [Display(GroupName = "残像", Name = "残像の間隔")]
    [TextBoxSlider("F3", "秒", 0.005, 0.2)]
    [DefaultValue(0.0166d)]
    [Range(0.001, 100)]
    public double TrailInterval { get => trailInterval; set => Set(ref trailInterval, value); }
    private double trailInterval = 1.0 / 60.0;

    [Display(GroupName = "残像", Name = "末端の濃さ")]
    [TextBoxSlider("F2", "", 0, 1)]
    [DefaultValue(0d)]
    [Range(0, 1)]
    public double TrailFade { get => trailFade; set => Set(ref trailFade, value); }
    private double trailFade;

    [Display(GroupName = "残像", Name = "末端の大きさ")]
    [TextBoxSlider("F2", "倍", 0, 1.5)]
    [DefaultValue(0.6d)]
    [Range(0, 100)]
    public double TrailScale { get => trailScale; set => Set(ref trailScale, value); }
    private double trailScale = 0.6;

    // =====================================================================
    // 分裂 (多段弾幕)
    // =====================================================================

    [Display(GroupName = "分裂", Name = "分裂する")]
    [ToggleSlider]
    public bool SplitEnabled { get => splitEnabled; set => Set(ref splitEnabled, value); }
    private bool splitEnabled;

    [Display(GroupName = "分裂", Name = "分裂までの時間")]
    [TextBoxSlider("F2", "秒", 0.05, 5)]
    [DefaultValue(0.6d)]
    [Range(0.01, 100000)]
    public double SplitDelay { get => splitDelay; set => Set(ref splitDelay, value); }
    private double splitDelay = 0.6;

    [Display(GroupName = "分裂", Name = "分裂数")]
    [TextBoxSlider("F0", "個", 1, 32)]
    [DefaultValue(8)]
    [Range(1, 500)]
    public int SplitCount { get => splitCount; set => Set(ref splitCount, value); }
    private int splitCount = 8;

    [Display(GroupName = "分裂", Name = "分裂の広がり角")]
    [TextBoxSlider("F1", "度", 0, 360)]
    [DefaultValue(360d)]
    [Range(0, 100000)]
    public double SplitSpread { get => splitSpread; set => Set(ref splitSpread, value); }
    private double splitSpread = 360;

    [Display(GroupName = "分裂", Name = "分裂後の速度")]
    [TextBoxSlider("F0", "px/秒", 0, 800)]
    [DefaultValue(180d)]
    [Range(-100000, 100000)]
    public double SplitSpeed { get => splitSpeed; set => Set(ref splitSpeed, value); }
    private double splitSpeed = 180;

    [Display(GroupName = "分裂", Name = "分裂後の大きさ")]
    [TextBoxSlider("F2", "倍", 0.1, 2)]
    [DefaultValue(0.8d)]
    [Range(0.001, 100)]
    public double SplitScaleFactor { get => splitScaleFactor; set => Set(ref splitScaleFactor, value); }
    private double splitScaleFactor = 0.8;

    [Display(GroupName = "分裂", Name = "親を消す")]
    [ToggleSlider]
    public bool SplitDestroyParent { get => splitDestroyParent; set => Set(ref splitDestroyParent, value); }
    private bool splitDestroyParent = true;

    [Display(GroupName = "分裂", Name = "多段の世代数", Description = "2 以上でさらに分裂を繰り返します。")]
    [TextBoxSlider("F0", "世代", 1, 5)]
    [DefaultValue(1)]
    [Range(1, 10)]
    public int SplitMaxGeneration { get => splitMaxGeneration; set => Set(ref splitMaxGeneration, value); }
    private int splitMaxGeneration = 1;

    // =====================================================================
    // 当たり判定
    // =====================================================================

    [Display(GroupName = "当たり判定", Name = "弾の判定半径", Description = "0 で判定なし。全体設定の「当たり判定」も有効にしてください。")]
    [TextBoxSlider("F1", "px", 0, 40)]
    [DefaultValue(0d)]
    [Range(0, 100000)]
    public double HitRadius { get => hitRadius; set => Set(ref hitRadius, value); }
    private double hitRadius;

    [Display(GroupName = "当たり判定", Name = "当たったら消える")]
    [ToggleSlider]
    public bool DestroyOnHit { get => destroyOnHit; set => Set(ref destroyOnHit, value); }
    private bool destroyOnHit = true;

    // =====================================================================
    // 変換
    // =====================================================================

    /// <summary>
    /// 編集項目をコアエンジンの設定へ変換する。
    /// <para>
    /// X / Y はキーフレームで動くため <b>ここでは既定値のみ</b>を入れ、
    /// 実際の値は <c>LiveValueSource</c> 経由で毎ステップ供給する。
    /// </para>
    /// </summary>
    /// <param name="emitterIndex">このエミッターの番号 (画像スロットの決定に使用)。</param>
    public EmitterSettings ToSettings(int emitterIndex) => new()
    {
        Name = string.IsNullOrWhiteSpace(Name) ? $"エミッター{emitterIndex + 1}" : Name,
        IsEnabled = IsEnabled,
        X = 0,
        Y = 0,
        OrbitRadius = OrbitRadius,
        OrbitSpeed = OrbitSpeed,
        OrbitPhase = OrbitPhase,
        SeedOffset = SeedOffset,

        SourceMode = SourceMode,
        SourcePath = string.IsNullOrWhiteSpace(SourcePath) ? null : SourcePath,
        SourceText = string.IsNullOrWhiteSpace(SourceText) ? null : SourceText,
        ScriptSpeedScale = ScriptSpeedScale,
        ScriptRank = ScriptRank,
        ScriptLoop = ScriptLoop,
        ImagePath = string.IsNullOrWhiteSpace(ImagePath) ? null : ImagePath,

        Pattern = new PatternSettings
        {
            Kind = PatternKind,
            Way = Way,
            Stack = Stack,
            StackSpeedStep = StackSpeedStep,
            StackAngleStep = StackAngleStep,
            BaseAngle = BaseAngle.GetFirstValue(),
            SpreadAngle = SpreadAngle,
            AngleStepPerShot = AngleStepPerShot,
            AngleJitter = AngleJitter,
            FireInterval = FireInterval,
            BurstCount = BurstCount,
            BurstInterval = BurstInterval,
            BurstCooldown = BurstCooldown,
            StartTime = StartTime,
            EndTime = EndTime,
            SpawnRadius = SpawnRadius,
            SpawnJitter = SpawnJitter,
            AimAtTarget = AimAtTarget,
            WallWidth = WallWidth,
            LaserSpacing = LaserSpacing,
            WhipAmplitude = WhipAmplitude,
            WhipPeriod = WhipPeriod,
        },

        Physics = new BulletPhysics
        {
            Speed = Speed,
            SpeedJitter = SpeedJitter,
            SpeedStep = SpeedStep,
            Acceleration = Acceleration,
            AngularVelocity = AngularVelocity,
            AngularVelocityJitter = AngularVelocityJitter,
            Damping = Damping,
            MinSpeed = MinSpeed,
            MaxSpeed = MaxSpeed,
            Gravity = Gravity,
            Wind = Wind,
            Lifetime = Lifetime,
            LifetimeJitter = LifetimeJitter,
            HomingEnabled = HomingEnabled,
            HomingTurnRate = HomingTurnRate,
            HomingDuration = HomingDuration,
            HomingDelay = HomingDelay,
            HitRadius = HitRadius,
            DestroyOnHit = DestroyOnHit,
        },

        Appearance = new BulletAppearance
        {
            // 画像が指定されていればユーザー画像スロット、なければ組み込み形状
            SpriteIndex = HasCustomImage ? SpriteSlots.CustomSlotOf(emitterIndex) : (int)Shape,
            SpriteCycleCount = 1,
            Scale = Scale,
            ScaleJitter = ScaleJitter,
            ScaleVelocity = ScaleVelocity,
            RotationVelocity = RotationVelocity,
            AlignToDirection = AlignToDirection,
            ColorMode = ColorMode,
            PrimaryColor = PrimaryColor.ToBulletColor(),
            SecondaryColor = SecondaryColor.ToBulletColor(),
            HueVelocity = HueVelocity,
            HueStep = HueStep,
            Additive = Additive,
            GlowIntensity = GlowIntensity,
            Opacity = Opacity,
            FadeInDuration = FadeInDuration,
            FadeOutDuration = FadeOutDuration,
            TrailLength = TrailLength,
            TrailInterval = TrailInterval,
            TrailFade = TrailFade,
            TrailScale = TrailScale,
        },

        Split = SplitEnabled ? BuildSplit(SplitMaxGeneration) : null,
        SplitDelay = SplitDelay,
    };

    /// <summary>ユーザー指定画像を使うかどうか。</summary>
    public bool HasCustomImage => !string.IsNullOrWhiteSpace(ImagePath);

    /// <summary>多段分裂の設定を世代数ぶん入れ子にして組み立てる。</summary>
    private SplitSpec BuildSplit(int remainingGenerations)
    {
        var generations = Math.Clamp(SplitMaxGeneration, 1, 10);
        return new SplitSpec
        {
            Count = SplitCount,
            SpreadDegrees = SplitSpread,
            Speed = SplitSpeed,
            ScaleFactor = SplitScaleFactor,
            DestroyParent = SplitDestroyParent,
            MaxGeneration = generations,
            NextDelay = SplitDelay,
            // 世代数が 2 以上なら、同じ設定を次段として連結する。
            // MaxGeneration による打ち切りがあるため無限再帰にはならない。
            Next = remainingGenerations > 1 ? BuildSplit(remainingGenerations - 1) : null,
        };
    }

    /// <summary>設定内容を別のインスタンスへ複製する (共有データの保存/復元に使用)。</summary>
    public void CopyTo(EmitterParameter other)
    {
        other.Name = Name;
        other.PresetName = PresetName;
        other.IsEnabled = IsEnabled;
        other.X.CopyFrom(X);
        other.Y.CopyFrom(Y);
        other.OrbitRadius = OrbitRadius;
        other.OrbitSpeed = OrbitSpeed;
        other.OrbitPhase = OrbitPhase;
        other.SeedOffset = SeedOffset;

        other.SourceMode = SourceMode;
        other.SourcePath = SourcePath;
        other.SourceText = SourceText;
        other.ScriptSpeedScale = ScriptSpeedScale;
        other.ScriptRank = ScriptRank;
        other.ScriptLoop = ScriptLoop;

        other.PatternKind = PatternKind;
        other.Way = Way;
        other.Stack = Stack;
        other.StackSpeedStep = StackSpeedStep;
        other.StackAngleStep = StackAngleStep;
        other.BaseAngle.CopyFrom(BaseAngle);
        other.SpreadAngle = SpreadAngle;
        other.AngleStepPerShot = AngleStepPerShot;
        other.AngleJitter = AngleJitter;
        other.FireInterval = FireInterval;
        other.BurstCount = BurstCount;
        other.BurstInterval = BurstInterval;
        other.BurstCooldown = BurstCooldown;
        other.StartTime = StartTime;
        other.EndTime = EndTime;
        other.SpawnRadius = SpawnRadius;
        other.SpawnJitter = SpawnJitter;
        other.AimAtTarget = AimAtTarget;
        other.WallWidth = WallWidth;
        other.LaserSpacing = LaserSpacing;
        other.WhipAmplitude = WhipAmplitude;
        other.WhipPeriod = WhipPeriod;

        other.Speed = Speed;
        other.SpeedJitter = SpeedJitter;
        other.SpeedStep = SpeedStep;
        other.Acceleration = Acceleration;
        other.AngularVelocity = AngularVelocity;
        other.AngularVelocityJitter = AngularVelocityJitter;
        other.Damping = Damping;
        other.MinSpeed = MinSpeed;
        other.MaxSpeed = MaxSpeed;
        other.Gravity = Gravity;
        other.Wind = Wind;
        other.Lifetime = Lifetime;
        other.LifetimeJitter = LifetimeJitter;

        other.HomingEnabled = HomingEnabled;
        other.HomingTurnRate = HomingTurnRate;
        other.HomingDuration = HomingDuration;
        other.HomingDelay = HomingDelay;

        other.Shape = Shape;
        other.ImagePath = ImagePath;
        other.Scale = Scale;
        other.ScaleJitter = ScaleJitter;
        other.ScaleVelocity = ScaleVelocity;
        other.RotationVelocity = RotationVelocity;
        other.AlignToDirection = AlignToDirection;
        other.ColorMode = ColorMode;
        other.PrimaryColor = PrimaryColor;
        other.SecondaryColor = SecondaryColor;
        other.HueVelocity = HueVelocity;
        other.HueStep = HueStep;
        other.Additive = Additive;
        other.GlowIntensity = GlowIntensity;
        other.Opacity = Opacity;
        other.FadeInDuration = FadeInDuration;
        other.FadeOutDuration = FadeOutDuration;

        other.TrailLength = TrailLength;
        other.TrailInterval = TrailInterval;
        other.TrailFade = TrailFade;
        other.TrailScale = TrailScale;

        other.SplitEnabled = SplitEnabled;
        other.SplitDelay = SplitDelay;
        other.SplitCount = SplitCount;
        other.SplitSpread = SplitSpread;
        other.SplitSpeed = SplitSpeed;
        other.SplitScaleFactor = SplitScaleFactor;
        other.SplitDestroyParent = SplitDestroyParent;
        other.SplitMaxGeneration = SplitMaxGeneration;

        other.HitRadius = HitRadius;
        other.DestroyOnHit = DestroyOnHit;
    }

    /// <summary>
    /// プリセットの内容をこのエミッターへ適用する。
    /// <para>
    /// X / Y (配置) と公転・シードずらしは<b>意図的に上書きしない</b>。
    /// プリセットは「弾幕の見た目と挙動」だけを表し、画面上のどこから撃つかは
    /// ユーザーがタイムライン上で決めた値を尊重する。
    /// </para>
    /// </summary>
    public void ApplyPreset(Core.Presets.DanmakuPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        PresetName = preset.Name;

        // プリセットは GUI パターン方式の設定を持つので、外部データ読み込み中でも
        // パターン方式へ戻しておく (そうしないと適用しても見た目が変わらない)。
        SourceMode = DanmakuSourceMode.Pattern;

        var pattern = preset.Pattern;
        PatternKind = pattern.Kind;
        Way = pattern.Way;
        Stack = pattern.Stack;
        StackSpeedStep = pattern.StackSpeedStep;
        StackAngleStep = pattern.StackAngleStep;
        BaseAngle.SetFirstValue(pattern.BaseAngle);
        SpreadAngle = pattern.SpreadAngle;
        AngleStepPerShot = pattern.AngleStepPerShot;
        AngleJitter = pattern.AngleJitter;
        FireInterval = pattern.FireInterval;
        BurstCount = pattern.BurstCount;
        BurstInterval = pattern.BurstInterval;
        BurstCooldown = pattern.BurstCooldown;
        SpawnRadius = pattern.SpawnRadius;
        SpawnJitter = pattern.SpawnJitter;
        AimAtTarget = pattern.AimAtTarget;
        WallWidth = pattern.WallWidth;
        LaserSpacing = pattern.LaserSpacing;
        WhipAmplitude = pattern.WhipAmplitude;
        WhipPeriod = pattern.WhipPeriod;

        var physics = preset.Physics;
        Speed = physics.Speed;
        SpeedJitter = physics.SpeedJitter;
        SpeedStep = physics.SpeedStep;
        Acceleration = physics.Acceleration;
        AngularVelocity = physics.AngularVelocity;
        AngularVelocityJitter = physics.AngularVelocityJitter;
        Damping = physics.Damping;
        MinSpeed = physics.MinSpeed;
        MaxSpeed = physics.MaxSpeed;
        Gravity = physics.Gravity;
        Wind = physics.Wind;
        Lifetime = physics.Lifetime;
        LifetimeJitter = physics.LifetimeJitter;
        HomingEnabled = physics.HomingEnabled;
        HomingTurnRate = physics.HomingTurnRate;
        HomingDuration = physics.HomingDuration;
        HomingDelay = physics.HomingDelay;

        var appearance = preset.Appearance;
        Shape = Enum.IsDefined((BulletShape)appearance.SpriteIndex)
            ? (BulletShape)appearance.SpriteIndex
            : BulletShape.Circle;
        Scale = appearance.Scale;
        ScaleJitter = appearance.ScaleJitter;
        ScaleVelocity = appearance.ScaleVelocity;
        RotationVelocity = appearance.RotationVelocity;
        AlignToDirection = appearance.AlignToDirection;
        ColorMode = appearance.ColorMode;
        PrimaryColor = appearance.PrimaryColor.ToMediaColor();
        SecondaryColor = appearance.SecondaryColor.ToMediaColor();
        HueVelocity = appearance.HueVelocity;
        HueStep = appearance.HueStep;
        Additive = appearance.Additive;
        GlowIntensity = appearance.GlowIntensity;
        Opacity = appearance.Opacity;
        FadeInDuration = appearance.FadeInDuration;
        FadeOutDuration = appearance.FadeOutDuration;
        TrailLength = appearance.TrailLength;
        TrailInterval = appearance.TrailInterval;
        TrailFade = appearance.TrailFade;
        TrailScale = appearance.TrailScale;

        SplitEnabled = preset.Split is not null;
        SplitDelay = preset.SplitDelay;
        if (preset.Split is { } split)
        {
            SplitCount = split.Count;
            SplitSpread = split.SpreadDegrees;
            SplitSpeed = split.Speed;
            SplitScaleFactor = split.ScaleFactor;
            SplitDestroyParent = split.DestroyParent;
            SplitMaxGeneration = split.MaxGeneration;
        }
    }

    /// <summary>現在の設定からプリセットを作る。</summary>
    public Core.Presets.DanmakuPreset ToPreset(string name, string description = "")
        => Core.Presets.DanmakuPreset.FromEmitter(ToSettings(0), name, description);

    protected override IEnumerable<IAnimatable> GetAnimatables() => [X, Y, BaseAngle];
}
