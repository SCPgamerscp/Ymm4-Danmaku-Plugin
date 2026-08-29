using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Settings;
using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Model;
using Ymm4DanmakuPlugin.Core.Scripting;
using Ymm4DanmakuPlugin.Interop;

namespace Ymm4DanmakuPlugin.Parameters;

/// <summary>
/// 単一エミッター (弾の発生源) の編集パラメータ。
/// </summary>
public class EmitterParameter : Animatable
{
    public EmitterParameter()
    {
        // アニメーション項目の Undo/Redo & プロパティ変更購読
        SubscribeAnimatable(X, nameof(X));
        SubscribeAnimatable(Y, nameof(Y));
        SubscribeAnimatable(OrbitRadius, nameof(OrbitRadius));
        SubscribeAnimatable(OrbitSpeed, nameof(OrbitSpeed));
        SubscribeAnimatable(OrbitPhase, nameof(OrbitPhase));
        SubscribeAnimatable(SeedOffset, nameof(SeedOffset));

        SubscribeAnimatable(ScriptSpeedScale, nameof(ScriptSpeedScale));
        SubscribeAnimatable(ScriptRank, nameof(ScriptRank));

        SubscribeAnimatable(Way, nameof(Way));
        SubscribeAnimatable(Stack, nameof(Stack));
        SubscribeAnimatable(StackSpeedStep, nameof(StackSpeedStep));
        SubscribeAnimatable(StackAngleStep, nameof(StackAngleStep));
        SubscribeAnimatable(BaseAngle, nameof(BaseAngle));
        SubscribeAnimatable(SpreadAngle, nameof(SpreadAngle));
        SubscribeAnimatable(AngleStepPerShot, nameof(AngleStepPerShot));
        SubscribeAnimatable(AngleJitter, nameof(AngleJitter));
        SubscribeAnimatable(FireInterval, nameof(FireInterval));
        SubscribeAnimatable(BurstCount, nameof(BurstCount));
        SubscribeAnimatable(BurstInterval, nameof(BurstInterval));
        SubscribeAnimatable(BurstCooldown, nameof(BurstCooldown));
        SubscribeAnimatable(StartTime, nameof(StartTime));
        SubscribeAnimatable(EndTime, nameof(EndTime));
        SubscribeAnimatable(SpawnRadius, nameof(SpawnRadius));
        SubscribeAnimatable(SpawnJitter, nameof(SpawnJitter));
        SubscribeAnimatable(AimRate, nameof(AimRate));
        SubscribeAnimatable(WallWidth, nameof(WallWidth));
        SubscribeAnimatable(LaserSpacing, nameof(LaserSpacing));
        SubscribeAnimatable(WhipAmplitude, nameof(WhipAmplitude));
        SubscribeAnimatable(WhipPeriod, nameof(WhipPeriod));

        SubscribeAnimatable(Speed, nameof(Speed));
        SubscribeAnimatable(SpeedJitter, nameof(SpeedJitter));
        SubscribeAnimatable(SpeedStep, nameof(SpeedStep));
        SubscribeAnimatable(Acceleration, nameof(Acceleration));
        SubscribeAnimatable(AngularVelocity, nameof(AngularVelocity));
        SubscribeAnimatable(AngularVelocityJitter, nameof(AngularVelocityJitter));
        SubscribeAnimatable(Damping, nameof(Damping));
        SubscribeAnimatable(MinSpeed, nameof(MinSpeed));
        SubscribeAnimatable(MaxSpeed, nameof(MaxSpeed));
        SubscribeAnimatable(Gravity, nameof(Gravity));
        SubscribeAnimatable(Wind, nameof(Wind));
        SubscribeAnimatable(Lifetime, nameof(Lifetime));
        SubscribeAnimatable(LifetimeJitter, nameof(LifetimeJitter));
        SubscribeAnimatable(HomingTurnRate, nameof(HomingTurnRate));
        SubscribeAnimatable(HomingDuration, nameof(HomingDuration));
        SubscribeAnimatable(HomingDelay, nameof(HomingDelay));

        SubscribeAnimatable(Scale, nameof(Scale));
        SubscribeAnimatable(ScaleJitter, nameof(ScaleJitter));
        SubscribeAnimatable(ScaleVelocity, nameof(ScaleVelocity));
        SubscribeAnimatable(RotationVelocity, nameof(RotationVelocity));
        SubscribeAnimatable(HueVelocity, nameof(HueVelocity));
        SubscribeAnimatable(HueStep, nameof(HueStep));
        SubscribeAnimatable(GlowIntensity, nameof(GlowIntensity));
        SubscribeAnimatable(Opacity, nameof(Opacity));
        SubscribeAnimatable(FadeInDuration, nameof(FadeInDuration));
        SubscribeAnimatable(FadeOutDuration, nameof(FadeOutDuration));

        SubscribeAnimatable(TrailLength, nameof(TrailLength));
        SubscribeAnimatable(TrailInterval, nameof(TrailInterval));
        SubscribeAnimatable(TrailFade, nameof(TrailFade));
        SubscribeAnimatable(TrailScale, nameof(TrailScale));

        SubscribeAnimatable(SplitDelay, nameof(SplitDelay));
        SubscribeAnimatable(SplitCount, nameof(SplitCount));
        SubscribeAnimatable(SplitSpread, nameof(SplitSpread));
        SubscribeAnimatable(SplitSpeed, nameof(SplitSpeed));
        SubscribeAnimatable(SplitScaleFactor, nameof(SplitScaleFactor));
        SubscribeAnimatable(SplitMaxGeneration, nameof(SplitMaxGeneration));

        SubscribeAnimatable(EnemyScale, nameof(EnemyScale));
        SubscribeAnimatable(EnemyRotation, nameof(EnemyRotation));
        SubscribeAnimatable(EnemyOpacity, nameof(EnemyOpacity));
        SubscribeAnimatable(MagicCircleScale, nameof(MagicCircleScale));
        SubscribeAnimatable(MagicCircleRotationSpeed, nameof(MagicCircleRotationSpeed));
        SubscribeAnimatable(MagicCircleOpacity, nameof(MagicCircleOpacity));
        SubscribeAnimatable(AuraIntensity, nameof(AuraIntensity));

        SubscribeAnimatable(HitRadius, nameof(HitRadius));
    }

    private void SubscribeAnimatable(Animation anim, string propertyName)
    {
        SubscribeChildUndoRedoable(anim);
        anim.PropertyChanged += (_, _) => OnPropertyChanged(propertyName);
    }

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
    [AnimationSlider("F1", "px", -1920, 1920)]
    public Animation X { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "エミッター", Name = "Y", Description = "発射位置 Y。キーフレームで動かせます。プレビュー上のドラッグでも変更できます。")]
    [AnimationSlider("F1", "px", -1080, 1080)]
    public Animation Y { get; } = new Animation(-200, -100000, 100000);

    [Display(GroupName = "エミッター", Name = "公転半径", Description = "エミッター自体を円運動させる半径。負の値で反対側を基準にします。")]
    [AnimationSlider("F1", "px", -600, 600)]
    public Animation OrbitRadius { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "エミッター", Name = "公転速度", Description = "エミッターの円運動の速度。")]
    [AnimationSlider("F1", "度/秒", -360, 360)]
    public Animation OrbitSpeed { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "エミッター", Name = "公転位相", Description = "公転の初期角度。")]
    [AnimationSlider("F1", "度", -360, 360)]
    public Animation OrbitPhase { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "エミッター", Name = "シードずらし", Description = "同じ設定のエミッターを複数置いたとき、乱数をずらして重なりを防ぎます。")]
    [AnimationSlider("F0", "", -100, 100)]
    public Animation SeedOffset { get; } = new Animation(0, -1000000, 1000000);

    // =====================================================================
    // プリセット
    // =====================================================================

    [Display(GroupName = "プリセット", Name = "プリセット", Description = "東方風のサンプルを選んで [適用]。保存・読み込み・書き出しもここから行えます。")]
    [Presets.PresetSelector]
    public string PresetName
    {
        get => presetName;
        set => Set(ref presetName, value ?? string.Empty);
    }
    private string presetName = "全方位リング";

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
    [AnimationSlider("F0", "px/秒", -240, 240)]
    public Animation ScriptSpeedScale { get; } = new Animation(60, -100000, 100000);

    [Display(GroupName = "弾幕データ", Name = "難易度($rank)", Description = "BulletML の $rank に渡す値 (0〜1)。")]
    [AnimationSlider("F2", "", -1, 1)]
    public Animation ScriptRank { get; } = new Animation(0.5, -100, 100);

    [Display(GroupName = "弾幕データ", Name = "繰り返し", Description = "スクリプトが終端に達したら最初から再生します。")]
    [ToggleSlider]
    public bool ScriptLoop { get => scriptLoop; set => Set(ref scriptLoop, value); }
    private bool scriptLoop = true;

    // =====================================================================
    // 第 1 階層: パターン形状
    // =====================================================================

    [Display(GroupName = "パターン", Name = "種類", Description = "弾の並べ方。切り替えるとおすすめの初期値が自動でセットされます。")]
    [EnumComboBox]
    public PatternKind PatternKind
    {
        get => patternKind;
        set
        {
            if (Set(ref patternKind, value))
            {
                ApplyPatternDefaults(value);
            }
        }
    }
    private PatternKind patternKind = Core.Configuration.PatternKind.Circle;

    [Display(GroupName = "パターン", Name = "way数", Description = "1 回の発射で撃つ弾の本数。0 で発射しません。")]
    [AnimationSlider("F0", "本", 0, 360)]
    public Animation Way { get; } = new Animation(24, 0, 10000);

    [Display(GroupName = "パターン", Name = "段数", Description = "速度差をつけて重ねる同心円の段数。way数 × 段数 が 1 回の発射数になります。0 で発射しません。")]
    [AnimationSlider("F0", "段", 0, 64)]
    public Animation Stack { get; } = new Animation(1, 0, 1000);

    [Display(GroupName = "パターン", Name = "段ごとの\n速度差")]
    [AnimationSlider("F0", "px/秒", -500, 500)]
    public Animation StackSpeedStep { get; } = new Animation(40, -100000, 100000);

    [Display(GroupName = "パターン", Name = "段ごとの\n角度差")]
    [AnimationSlider("F1", "度", -180, 180)]
    public Animation StackAngleStep { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "パターン", Name = "基準角", Description = "発射の中心方向。キーフレームで自由に回転させられます。0 で下向き。")]
    [AnimationSlider("F1", "度", -360, 360)]
    public Animation BaseAngle { get; } = new Animation(-90, -100000, 100000);

    [Display(GroupName = "パターン", Name = "広がり角", Description = "弾を配置する扇の角度。負の値で逆方向に展開。360 で全方位。")]
    [AnimationSlider("F1", "度", -360, 360)]
    public Animation SpreadAngle { get; } = new Animation(360, -100000, 100000);

    [Display(GroupName = "パターン", Name = "発射ごとの\n回転", Description = "1 回発射するたびに基準角へ加算する角度。螺旋弾の要です。")]
    [AnimationSlider("F1", "度/発", -180, 180)]
    public Animation AngleStepPerShot { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "パターン", Name = "角度ゆらぎ", Description = "基準角にかけるランダム幅 (±)。")]
    [AnimationSlider("F1", "度", -180, 180)]
    public Animation AngleJitter { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "パターン", Name = "発射間隔")]
    [AnimationSlider("F4", "秒", 0, 5)]
    public Animation FireInterval { get; } = new Animation(0.35, 0, 10000);

    [Display(GroupName = "パターン", Name = "連射数", Description = "ひとかたまり (バースト) あたりの発射回数。")]
    [AnimationSlider("F0", "回", 0, 100)]
    public Animation BurstCount { get; } = new Animation(1, 0, 10000);

    [Display(GroupName = "パターン", Name = "連射間隔")]
    [AnimationSlider("F4", "秒", 0, 1)]
    public Animation BurstInterval { get; } = new Animation(0.02, 0, 10000);

    [Display(GroupName = "パターン", Name = "連射後の\n待機")]
    [AnimationSlider("F3", "秒", 0, 10)]
    public Animation BurstCooldown { get; } = new Animation(0, 0, 10000);

    [Display(GroupName = "パターン", Name = "発射開始", Description = "アイテム先頭からの相対時間。")]
    [AnimationSlider("F2", "秒", -10, 10)]
    public Animation StartTime { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "パターン", Name = "発射終了", Description = "0 でアイテム終端まで撃ち続けます。")]
    [AnimationSlider("F2", "秒", 0, 60)]
    public Animation EndTime { get; } = new Animation(0, 0, 100000);

    [Display(GroupName = "パターン", Name = "発生半径", Description = "発射位置からこの距離だけ離した位置に弾を出します。負の値で後方から発生。")]
    [AnimationSlider("F1", "px", -300, 300)]
    public Animation SpawnRadius { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "パターン", Name = "発生位置\nゆらぎ")]
    [AnimationSlider("F1", "px", -200, 200)]
    public Animation SpawnJitter { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "パターン", Name = "自機狙い度", Description = "ターゲット (自機) 方向へ向ける割合。0% で固定角、100% で完全自機狙い、-100% で自機の真反対へ発射。")]
    [AnimationSlider("F1", "%", -100, 100)]
    public Animation AimRate { get; } = new Animation(0, -100, 100);

    /// <summary>後方互換性用ヘルパー。</summary>
    public bool AimAtTarget
    {
        get => AimRate.GetFirstValue() > 0;
        set => AimRate.SetFirstValue(value ? 100 : 0);
    }

    [Display(GroupName = "パターン", Name = "壁の横幅", Description = "横一列に並べて配置する横幅。0 で点発生。")]
    [AnimationSlider("F0", "px", -3840, 3840)]
    public Animation WallWidth { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "パターン", Name = "レーザー間隔", Description = "進行方向に弾を並べる間隔。0 で前後オフセットなし。")]
    [AnimationSlider("F1", "px", -120, 120)]
    public Animation LaserSpacing { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "パターン", Name = "鞭の振れ幅", Description = "左右に首を振る振れ幅。0 で首振りなし。")]
    [AnimationSlider("F1", "度", -180, 180)]
    public Animation WhipAmplitude { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "パターン", Name = "鞭の周期", Description = "首振りが1往復する周期。")]
    [AnimationSlider("F2", "秒", -6, 6)]
    public Animation WhipPeriod { get; } = new Animation(1.2, -100000, 100000);

    // =====================================================================
    // 第 2 階層: 物理
    // =====================================================================

    [Display(GroupName = "弾の動き", Name = "初速")]
    [AnimationSlider("F0", "px/秒", -3000, 3000)]
    public Animation Speed { get; } = new Animation(260, -100000, 100000);

    [Display(GroupName = "弾の動き", Name = "初速ゆらぎ")]
    [AnimationSlider("F0", "px/秒", -1000, 1000)]
    public Animation SpeedJitter { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "弾の動き", Name = "弾ごとの\n速度差", Description = "n-way の内側と外側で速度差をつけます。")]
    [AnimationSlider("F1", "px/秒", -100, 100)]
    public Animation SpeedStep { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "弾の動き", Name = "加速度")]
    [AnimationSlider("F1", "px/秒²", -1000, 1000)]
    public Animation Acceleration { get; } = new Animation(0, -1000000, 1000000);

    [Display(GroupName = "弾の動き", Name = "旋回速度", Description = "正で時計回りに曲がります。")]
    [AnimationSlider("F1", "度/秒", -720, 720)]
    public Animation AngularVelocity { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "弾の動き", Name = "旋回の\nゆらぎ")]
    [AnimationSlider("F1", "度/秒", -360, 360)]
    public Animation AngularVelocityJitter { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "弾の動き", Name = "減速", Description = "1 秒後に残る速度の割合。1 で減速なし。0 で瞬時に静止。")]
    [AnimationSlider("F2", "", -1, 1)]
    public Animation Damping { get; } = new Animation(1.0, -100, 100);

    [Display(GroupName = "弾の動き", Name = "最低速度")]
    [AnimationSlider("F0", "px/秒", -5000, 5000)]
    public Animation MinSpeed { get; } = new Animation(0, -1000000, 1000000);

    [Display(GroupName = "弾の動き", Name = "最高速度")]
    [AnimationSlider("F0", "px/秒", -5000, 5000)]
    public Animation MaxSpeed { get; } = new Animation(3000, -1000000, 1000000);

    [Display(GroupName = "弾の動き", Name = "重力", Description = "正で下向き。")]
    [AnimationSlider("F0", "px/秒²", -1000, 1000)]
    public Animation Gravity { get; } = new Animation(0, -1000000, 1000000);

    [Display(GroupName = "弾の動き", Name = "風", Description = "正で右向き。")]
    [AnimationSlider("F0", "px/秒²", -1000, 1000)]
    public Animation Wind { get; } = new Animation(0, -1000000, 1000000);

    [Display(GroupName = "弾の動き", Name = "寿命", Description = "0 で無限 (画面外に出るまで残ります)。")]
    [AnimationSlider("F1", "秒", 0, 100)]
    public Animation Lifetime { get; } = new Animation(30.0, 0, 100000);

    [Display(GroupName = "弾の動き", Name = "寿命ゆらぎ")]
    [AnimationSlider("F1", "秒", -10, 10)]
    public Animation LifetimeJitter { get; } = new Animation(0, -100000, 100000);

    // ---- ホーミング ----

    [Display(GroupName = "ホーミング", Name = "追尾する", Description = "ターゲット (自機) を追いかけます。")]
    [ToggleSlider]
    public bool HomingEnabled { get => homingEnabled; set => Set(ref homingEnabled, value); }
    private bool homingEnabled;

    [Display(GroupName = "ホーミング", Name = "旋回力", Description = "正で自機を追尾、負で自機から逃げるように反発旋回します。")]
    [AnimationSlider("F0", "度/秒", -720, 720)]
    public Animation HomingTurnRate { get; } = new Animation(90, -100000, 100000);

    [Display(GroupName = "ホーミング", Name = "追尾時間", Description = "0 で寿命いっぱい追尾します。")]
    [AnimationSlider("F2", "秒", 0, 10)]
    public Animation HomingDuration { get; } = new Animation(1.5, 0, 100000);

    [Display(GroupName = "ホーミング", Name = "追尾開始\nまで")]
    [AnimationSlider("F2", "秒", 0, 3)]
    public Animation HomingDelay { get; } = new Animation(0, 0, 100000);

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

    [Display(GroupName = "見た目", Name = "大きさ", Description = "拡大倍率。負の値で画像を反転 (ミラー) 描画します。")]
    [AnimationSlider("F2", "倍", -4, 4)]
    public Animation Scale { get; } = new Animation(1.0, -1000, 1000);

    [Display(GroupName = "見た目", Name = "大きさの\nゆらぎ")]
    [AnimationSlider("F2", "倍", -1, 1)]
    public Animation ScaleJitter { get; } = new Animation(0, -1000, 1000);

    [Display(GroupName = "見た目", Name = "拡縮速度", Description = "1 秒あたりの大きさの変化量。負で縮みます。")]
    [AnimationSlider("F2", "倍/秒", -2, 2)]
    public Animation ScaleVelocity { get; } = new Animation(0, -1000, 1000);

    [Display(GroupName = "見た目", Name = "回転速度")]
    [AnimationSlider("F0", "度/秒", -720, 720)]
    public Animation RotationVelocity { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "見た目", Name = "進行方向を\n向く")]
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
    [AnimationSlider("F0", "度/秒", -720, 720)]
    public Animation HueVelocity { get; } = new Animation(120, -100000, 100000);

    [Display(GroupName = "見た目", Name = "弾ごとの\n色相差")]
    [AnimationSlider("F1", "度", -180, 180)]
    public Animation HueStep { get; } = new Animation(15, -100000, 100000);

    [Display(GroupName = "見た目", Name = "発光\n(加算合成)", Description = "東方風の光る弾にします。")]
    [ToggleSlider]
    public bool Additive { get => additive; set => Set(ref additive, value); }
    private bool additive = true;

    [Display(GroupName = "見た目", Name = "発光の強さ")]
    [AnimationSlider("F2", "倍", 0, 3)]
    public Animation GlowIntensity { get; } = new Animation(1.0, 0, 100);

    [Display(GroupName = "見た目", Name = "不透明度")]
    [AnimationSlider("F2", "", -1, 1)]
    public Animation Opacity { get; } = new Animation(1.0, -1, 1);

    [Display(GroupName = "見た目", Name = "フェード\nイン")]
    [AnimationSlider("F2", "秒", 0, 1)]
    public Animation FadeInDuration { get; } = new Animation(0.05, 0, 1000);

    [Display(GroupName = "見た目", Name = "フェード\nアウト")]
    [AnimationSlider("F2", "秒", 0, 2)]
    public Animation FadeOutDuration { get; } = new Animation(0.15, 0, 1000);

    // ---- トレイル (残像) ----

    [Display(GroupName = "残像", Name = "残像の数", Description = "0 で残像なし。")]
    [AnimationSlider("F0", "個", 0, 32)]
    public Animation TrailLength { get; } = new Animation(0, 0, 48);

    [Display(GroupName = "残像", Name = "残像の\n間隔")]
    [AnimationSlider("F3", "秒", 0, 0.2)]
    public Animation TrailInterval { get; } = new Animation(1.0 / 60.0, 0, 100);

    [Display(GroupName = "残像", Name = "末端の\n濃さ")]
    [AnimationSlider("F2", "", 0, 1)]
    public Animation TrailFade { get; } = new Animation(0, 0, 1);

    [Display(GroupName = "残像", Name = "末端の\n大きさ")]
    [AnimationSlider("F2", "倍", -1.5, 1.5)]
    public Animation TrailScale { get; } = new Animation(0.6, -100, 100);

    // =====================================================================
    // 分裂 (多段弾幕)
    // =====================================================================

    [Display(GroupName = "分裂", Name = "分裂する")]
    [ToggleSlider]
    public bool SplitEnabled { get => splitEnabled; set => Set(ref splitEnabled, value); }
    private bool splitEnabled;

    [Display(GroupName = "分裂", Name = "分裂までの\n時間")]
    [AnimationSlider("F2", "秒", 0, 5)]
    public Animation SplitDelay { get; } = new Animation(0.6, 0, 100000);

    [Display(GroupName = "分裂", Name = "分裂数")]
    [AnimationSlider("F0", "個", 0, 32)]
    public Animation SplitCount { get; } = new Animation(8, 0, 500);

    [Display(GroupName = "分裂", Name = "分裂の\n広がり角")]
    [AnimationSlider("F1", "度", -360, 360)]
    public Animation SplitSpread { get; } = new Animation(360, -100000, 100000);

    [Display(GroupName = "分裂", Name = "分裂後の\n速度")]
    [AnimationSlider("F0", "px/秒", -800, 800)]
    public Animation SplitSpeed { get; } = new Animation(180, -100000, 100000);

    [Display(GroupName = "分裂", Name = "分裂後の\n大きさ")]
    [AnimationSlider("F2", "倍", -2, 2)]
    public Animation SplitScaleFactor { get; } = new Animation(0.8, -100, 100);

    [Display(GroupName = "分裂", Name = "親を消す")]
    [ToggleSlider]
    public bool SplitDestroyParent { get => splitDestroyParent; set => Set(ref splitDestroyParent, value); }
    private bool splitDestroyParent = true;

    [Display(GroupName = "分裂", Name = "多段の\n世代数", Description = "2 以上でさらに分裂を繰り返します。")]
    [AnimationSlider("F0", "世代", 0, 5)]
    public Animation SplitMaxGeneration { get; } = new Animation(1, 0, 10);

    // =====================================================================
    // エネミー (敵) & 魔法陣 & オーラ
    // =====================================================================

    [Display(GroupName = "エネミー (敵)", Name = "エネミー画像", Description = "発射位置 (エミッター) の中心に表示するキャラクターやボスの画像。")]
    [FileSelector(FileGroupType.ImageItem)]
    public string EnemyImagePath { get => enemyImagePath; set => Set(ref enemyImagePath, value ?? string.Empty); }
    private string enemyImagePath = string.Empty;

    public bool HasEnemyImage => !string.IsNullOrWhiteSpace(EnemyImagePath);

    [Display(GroupName = "エネミー (敵)", Name = "画像サイズ", Description = "エネミー画像の拡大倍率。負の値で左右/上下反転。")]
    [AnimationSlider("F2", "倍", -10, 10)]
    public Animation EnemyScale { get; } = new Animation(1.0, -1000, 1000);

    [Display(GroupName = "エネミー (敵)", Name = "画像の回転", Description = "エネミー画像の回転角度。")]
    [AnimationSlider("F1", "度", -360, 360)]
    public Animation EnemyRotation { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "エネミー (敵)", Name = "不透明度")]
    [AnimationSlider("F2", "", -1, 1)]
    public Animation EnemyOpacity { get; } = new Animation(1.0, -1, 1);

    [Display(GroupName = "エネミー (敵)", Name = "弾の奥に\n描画", Description = "オンで弾幕の背後に配置、オフで弾幕の手前に配置します。")]
    [ToggleSlider]
    public bool EnemyBehindBullets { get => enemyBehindBullets; set => Set(ref enemyBehindBullets, value); }
    private bool enemyBehindBullets = true;

    [Display(GroupName = "エネミー (敵)", Name = "魔法陣を\n有効化", Description = "ボスの背後に東方風の魔法陣を展開します。")]
    [ToggleSlider]
    public bool MagicCircleEnabled { get => magicCircleEnabled; set => Set(ref magicCircleEnabled, value); }
    private bool magicCircleEnabled;

    [Display(GroupName = "エネミー (敵)", Name = "魔法陣画像", Description = "カスタム魔法陣画像。未指定時は組み込みの東方風幾何学魔法陣が描かれます。")]
    [FileSelector(FileGroupType.ImageItem)]
    public string MagicCircleImagePath { get => magicCircleImagePath; set => Set(ref magicCircleImagePath, value ?? string.Empty); }
    private string magicCircleImagePath = string.Empty;

    public bool HasCustomMagicCircleImage => !string.IsNullOrWhiteSpace(MagicCircleImagePath);

    [Display(GroupName = "エネミー (敵)", Name = "魔法陣\nサイズ")]
    [AnimationSlider("F2", "倍", -10, 10)]
    public Animation MagicCircleScale { get; } = new Animation(1.5, -1000, 1000);

    [Display(GroupName = "エネミー (敵)", Name = "魔法陣の\n回転速度", Description = "1 秒あたりの回転角度。正で時計回り、負で反時計回り。")]
    [AnimationSlider("F1", "度/秒", -720, 720)]
    public Animation MagicCircleRotationSpeed { get; } = new Animation(45, -100000, 100000);

    [Display(GroupName = "エネミー (敵)", Name = "魔法陣の色")]
    [ColorPicker]
    public Color MagicCircleColor { get => magicCircleColor; set => Set(ref magicCircleColor, value); }
    private Color magicCircleColor = Color.FromRgb(150, 220, 255);

    [Display(GroupName = "エネミー (敵)", Name = "魔法陣の\n不透明度")]
    [AnimationSlider("F2", "", -1, 1)]
    public Animation MagicCircleOpacity { get; } = new Animation(0.8, -1, 1);

    [Display(GroupName = "エネミー (敵)", Name = "魔法陣を\n加算合成")]
    [ToggleSlider]
    public bool MagicCircleAdditive { get => magicCircleAdditive; set => Set(ref magicCircleAdditive, value); }
    private bool magicCircleAdditive = true;

    [Display(GroupName = "エネミー (敵)", Name = "オーラを\n有効化", Description = "ボスの周囲に発光オーラを纏わせます。")]
    [ToggleSlider]
    public bool AuraEnabled { get => auraEnabled; set => Set(ref auraEnabled, value); }
    private bool auraEnabled;

    [Display(GroupName = "エネミー (敵)", Name = "オーラ強度")]
    [AnimationSlider("F2", "倍", -5, 5)]
    public Animation AuraIntensity { get; } = new Animation(1.5, -100, 100);

    [Display(GroupName = "エネミー (敵)", Name = "オーラの色")]
    [ColorPicker]
    public Color AuraColor { get => auraColor; set => Set(ref auraColor, value); }
    private Color auraColor = Color.FromRgb(180, 230, 255);

    // =====================================================================
    // 当たり判定
    // =====================================================================

    [Display(GroupName = "当たり判定", Name = "弾の判定半径", Description = "0 で判定なし。全体設定の「当たり判定」も有効にしてください。")]
    [AnimationSlider("F1", "px", -40, 40)]
    public Animation HitRadius { get; } = new Animation(0, -100000, 100000);

    [Display(GroupName = "当たり判定", Name = "被弾時に\n弾を消す")]
    [ToggleSlider]
    public bool DestroyOnHit { get => destroyOnHit; set => Set(ref destroyOnHit, value); }
    private bool destroyOnHit = true;

    // =====================================================================
    // 変換
    // =====================================================================

    /// <summary>
    /// 編集項目をコアエンジンの設定へ変換する。
    /// </summary>
    public EmitterSettings ToSettings(int emitterIndex) => new()
    {
        Name = string.IsNullOrWhiteSpace(Name) ? $"エミッター{emitterIndex + 1}" : Name,
        IsEnabled = IsEnabled,
        X = 0,
        Y = 0,
        OrbitRadius = OrbitRadius.GetFirstValue(),
        OrbitSpeed = OrbitSpeed.GetFirstValue(),
        OrbitPhase = OrbitPhase.GetFirstValue(),
        SeedOffset = (int)Math.Round(SeedOffset.GetFirstValue()),

        SourceMode = SourceMode,
        SourcePath = string.IsNullOrWhiteSpace(SourcePath) ? null : SourcePath,
        SourceText = string.IsNullOrWhiteSpace(SourceText) ? null : SourceText,
        ScriptSpeedScale = ScriptSpeedScale.GetFirstValue(),
        ScriptRank = ScriptRank.GetFirstValue(),
        ScriptLoop = ScriptLoop,
        ImagePath = string.IsNullOrWhiteSpace(ImagePath) ? null : ImagePath,

        Pattern = new PatternSettings
        {
            Kind = PatternKind,
            Way = Math.Max(0, (int)Math.Round(Way.GetFirstValue())),
            Stack = Math.Max(0, (int)Math.Round(Stack.GetFirstValue())),
            StackSpeedStep = StackSpeedStep.GetFirstValue(),
            StackAngleStep = StackAngleStep.GetFirstValue(),
            BaseAngle = BaseAngle.GetFirstValue(),
            SpreadAngle = SpreadAngle.GetFirstValue(),
            AngleStepPerShot = AngleStepPerShot.GetFirstValue(),
            AngleJitter = AngleJitter.GetFirstValue(),
            FireInterval = FireInterval.GetFirstValue(),
            BurstCount = Math.Max(0, (int)Math.Round(BurstCount.GetFirstValue())),
            BurstInterval = BurstInterval.GetFirstValue(),
            BurstCooldown = BurstCooldown.GetFirstValue(),
            StartTime = StartTime.GetFirstValue(),
            EndTime = EndTime.GetFirstValue(),
            SpawnRadius = SpawnRadius.GetFirstValue(),
            SpawnJitter = SpawnJitter.GetFirstValue(),
            AimRate = AimRate.GetFirstValue(),
            WallWidth = WallWidth.GetFirstValue(),
            LaserSpacing = LaserSpacing.GetFirstValue(),
            WhipAmplitude = WhipAmplitude.GetFirstValue(),
            WhipPeriod = WhipPeriod.GetFirstValue(),
        },

        Physics = new BulletPhysics
        {
            Speed = Speed.GetFirstValue(),
            SpeedJitter = SpeedJitter.GetFirstValue(),
            SpeedStep = SpeedStep.GetFirstValue(),
            Acceleration = Acceleration.GetFirstValue(),
            AngularVelocity = AngularVelocity.GetFirstValue(),
            AngularVelocityJitter = AngularVelocityJitter.GetFirstValue(),
            Damping = Damping.GetFirstValue(),
            MinSpeed = MinSpeed.GetFirstValue(),
            MaxSpeed = MaxSpeed.GetFirstValue(),
            Gravity = Gravity.GetFirstValue(),
            Wind = Wind.GetFirstValue(),
            Lifetime = Lifetime.GetFirstValue(),
            LifetimeJitter = LifetimeJitter.GetFirstValue(),
            HomingEnabled = HomingEnabled,
            HomingTurnRate = HomingTurnRate.GetFirstValue(),
            HomingDuration = HomingDuration.GetFirstValue(),
            HomingDelay = HomingDelay.GetFirstValue(),
            HitRadius = HitRadius.GetFirstValue(),
            DestroyOnHit = DestroyOnHit,
        },

        Appearance = new BulletAppearance
        {
            // 画像が指定されていればユーザー画像スロット、なければ組み込み形状
            SpriteIndex = HasCustomImage ? SpriteSlots.CustomSlotOf(emitterIndex) : (int)Shape,
            SpriteCycleCount = 1,
            Scale = Scale.GetFirstValue(),
            ScaleJitter = ScaleJitter.GetFirstValue(),
            ScaleVelocity = ScaleVelocity.GetFirstValue(),
            RotationVelocity = RotationVelocity.GetFirstValue(),
            AlignToDirection = AlignToDirection,
            ColorMode = ColorMode,
            PrimaryColor = PrimaryColor.ToBulletColor(),
            SecondaryColor = SecondaryColor.ToBulletColor(),
            HueVelocity = HueVelocity.GetFirstValue(),
            HueStep = HueStep.GetFirstValue(),
            Additive = Additive,
            GlowIntensity = GlowIntensity.GetFirstValue(),
            Opacity = Opacity.GetFirstValue(),
            FadeInDuration = FadeInDuration.GetFirstValue(),
            FadeOutDuration = FadeOutDuration.GetFirstValue(),
            TrailLength = Math.Max(0, (int)Math.Round(TrailLength.GetFirstValue())),
            TrailInterval = TrailInterval.GetFirstValue(),
            TrailFade = TrailFade.GetFirstValue(),
            TrailScale = TrailScale.GetFirstValue(),
        },

        Split = SplitEnabled ? BuildSplit(Math.Max(0, (int)Math.Round(SplitMaxGeneration.GetFirstValue()))) : null,
        SplitDelay = SplitDelay.GetFirstValue(),
    };

    /// <summary>ユーザー指定画像を使うかどうか。</summary>
    public bool HasCustomImage => !string.IsNullOrWhiteSpace(ImagePath);

    /// <summary>多段分裂の設定を世代数ぶん入れ子にして組み立てる。</summary>
    private SplitSpec BuildSplit(int remainingGenerations)
    {
        var generations = Math.Clamp((int)Math.Round(SplitMaxGeneration.GetFirstValue()), 0, 10);
        return new SplitSpec
        {
            Count = Math.Max(0, (int)Math.Round(SplitCount.GetFirstValue())),
            SpreadDegrees = SplitSpread.GetFirstValue(),
            Speed = SplitSpeed.GetFirstValue(),
            ScaleFactor = SplitScaleFactor.GetFirstValue(),
            DestroyParent = SplitDestroyParent,
            MaxGeneration = generations,
            NextDelay = SplitDelay.GetFirstValue(),
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
        other.OrbitRadius.CopyFrom(OrbitRadius);
        other.OrbitSpeed.CopyFrom(OrbitSpeed);
        other.OrbitPhase.CopyFrom(OrbitPhase);
        other.SeedOffset.CopyFrom(SeedOffset);

        other.SourceMode = SourceMode;
        other.SourcePath = SourcePath;
        other.SourceText = SourceText;
        other.ScriptSpeedScale.CopyFrom(ScriptSpeedScale);
        other.ScriptRank.CopyFrom(ScriptRank);
        other.ScriptLoop = ScriptLoop;

        other.PatternKind = PatternKind;
        other.Way.CopyFrom(Way);
        other.Stack.CopyFrom(Stack);
        other.StackSpeedStep.CopyFrom(StackSpeedStep);
        other.StackAngleStep.CopyFrom(StackAngleStep);
        other.BaseAngle.CopyFrom(BaseAngle);
        other.SpreadAngle.CopyFrom(SpreadAngle);
        other.AngleStepPerShot.CopyFrom(AngleStepPerShot);
        other.AngleJitter.CopyFrom(AngleJitter);
        other.FireInterval.CopyFrom(FireInterval);
        other.BurstCount.CopyFrom(BurstCount);
        other.BurstInterval.CopyFrom(BurstInterval);
        other.BurstCooldown.CopyFrom(BurstCooldown);
        other.StartTime.CopyFrom(StartTime);
        other.EndTime.CopyFrom(EndTime);
        other.SpawnRadius.CopyFrom(SpawnRadius);
        other.SpawnJitter.CopyFrom(SpawnJitter);
        other.AimRate.CopyFrom(AimRate);
        other.WallWidth.CopyFrom(WallWidth);
        other.LaserSpacing.CopyFrom(LaserSpacing);
        other.WhipAmplitude.CopyFrom(WhipAmplitude);
        other.WhipPeriod.CopyFrom(WhipPeriod);

        other.Speed.CopyFrom(Speed);
        other.SpeedJitter.CopyFrom(SpeedJitter);
        other.SpeedStep.CopyFrom(SpeedStep);
        other.Acceleration.CopyFrom(Acceleration);
        other.AngularVelocity.CopyFrom(AngularVelocity);
        other.AngularVelocityJitter.CopyFrom(AngularVelocityJitter);
        other.Damping.CopyFrom(Damping);
        other.MinSpeed.CopyFrom(MinSpeed);
        other.MaxSpeed.CopyFrom(MaxSpeed);
        other.Gravity.CopyFrom(Gravity);
        other.Wind.CopyFrom(Wind);
        other.Lifetime.CopyFrom(Lifetime);
        other.LifetimeJitter.CopyFrom(LifetimeJitter);

        other.HomingEnabled = HomingEnabled;
        other.HomingTurnRate.CopyFrom(HomingTurnRate);
        other.HomingDuration.CopyFrom(HomingDuration);
        other.HomingDelay.CopyFrom(HomingDelay);

        other.Shape = Shape;
        other.ImagePath = ImagePath;
        other.Scale.CopyFrom(Scale);
        other.ScaleJitter.CopyFrom(ScaleJitter);
        other.ScaleVelocity.CopyFrom(ScaleVelocity);
        other.RotationVelocity.CopyFrom(RotationVelocity);
        other.AlignToDirection = AlignToDirection;
        other.ColorMode = ColorMode;
        other.PrimaryColor = PrimaryColor;
        other.SecondaryColor = SecondaryColor;
        other.HueVelocity.CopyFrom(HueVelocity);
        other.HueStep.CopyFrom(HueStep);
        other.Additive = Additive;
        other.GlowIntensity.CopyFrom(GlowIntensity);
        other.Opacity.CopyFrom(Opacity);
        other.FadeInDuration.CopyFrom(FadeInDuration);
        other.FadeOutDuration.CopyFrom(FadeOutDuration);

        other.TrailLength.CopyFrom(TrailLength);
        other.TrailInterval.CopyFrom(TrailInterval);
        other.TrailFade.CopyFrom(TrailFade);
        other.TrailScale.CopyFrom(TrailScale);

        other.SplitEnabled = SplitEnabled;
        other.SplitDelay.CopyFrom(SplitDelay);
        other.SplitCount.CopyFrom(SplitCount);
        other.SplitSpread.CopyFrom(SplitSpread);
        other.SplitSpeed.CopyFrom(SplitSpeed);
        other.SplitScaleFactor.CopyFrom(SplitScaleFactor);
        other.SplitDestroyParent = SplitDestroyParent;
        other.SplitMaxGeneration.CopyFrom(SplitMaxGeneration);

        other.EnemyImagePath = EnemyImagePath;
        other.EnemyScale.CopyFrom(EnemyScale);
        other.EnemyRotation.CopyFrom(EnemyRotation);
        other.EnemyOpacity.CopyFrom(EnemyOpacity);
        other.EnemyBehindBullets = EnemyBehindBullets;
        other.MagicCircleEnabled = MagicCircleEnabled;
        other.MagicCircleImagePath = MagicCircleImagePath;
        other.MagicCircleScale.CopyFrom(MagicCircleScale);
        other.MagicCircleRotationSpeed.CopyFrom(MagicCircleRotationSpeed);
        other.MagicCircleColor = MagicCircleColor;
        other.MagicCircleOpacity.CopyFrom(MagicCircleOpacity);
        other.MagicCircleAdditive = MagicCircleAdditive;
        other.AuraEnabled = AuraEnabled;
        other.AuraIntensity.CopyFrom(AuraIntensity);
        other.AuraColor = AuraColor;

        other.HitRadius.CopyFrom(HitRadius);
        other.DestroyOnHit = DestroyOnHit;
    }

    /// <summary>
    /// プリセットの内容をこのエミッターへ適用する。
    /// </summary>
    public void ApplyPreset(Core.Presets.DanmakuPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        PresetName = preset.Name;
        SourceMode = DanmakuSourceMode.Pattern;

        var pattern = preset.Pattern;
        PatternKind = pattern.Kind;
        Way.SetFirstValue(pattern.Way);
        Stack.SetFirstValue(pattern.Stack);
        StackSpeedStep.SetFirstValue(pattern.StackSpeedStep);
        StackAngleStep.SetFirstValue(pattern.StackAngleStep);
        BaseAngle.SetFirstValue(pattern.BaseAngle);
        SpreadAngle.SetFirstValue(pattern.SpreadAngle);
        AngleStepPerShot.SetFirstValue(pattern.AngleStepPerShot);
        AngleJitter.SetFirstValue(pattern.AngleJitter);
        FireInterval.SetFirstValue(pattern.FireInterval);
        BurstCount.SetFirstValue(pattern.BurstCount);
        BurstInterval.SetFirstValue(pattern.BurstInterval);
        BurstCooldown.SetFirstValue(pattern.BurstCooldown);
        SpawnRadius.SetFirstValue(pattern.SpawnRadius);
        SpawnJitter.SetFirstValue(pattern.SpawnJitter);
        AimRate.SetFirstValue(pattern.AimRate != 0 ? pattern.AimRate : (pattern.AimAtTarget ? 100 : 0));
        WallWidth.SetFirstValue(pattern.WallWidth);
        LaserSpacing.SetFirstValue(pattern.LaserSpacing);
        WhipAmplitude.SetFirstValue(pattern.WhipAmplitude);
        WhipPeriod.SetFirstValue(pattern.WhipPeriod);

        var physics = preset.Physics;
        Speed.SetFirstValue(physics.Speed);
        SpeedJitter.SetFirstValue(physics.SpeedJitter);
        SpeedStep.SetFirstValue(physics.SpeedStep);
        Acceleration.SetFirstValue(physics.Acceleration);
        AngularVelocity.SetFirstValue(physics.AngularVelocity);
        AngularVelocityJitter.SetFirstValue(physics.AngularVelocityJitter);
        Damping.SetFirstValue(physics.Damping);
        MinSpeed.SetFirstValue(physics.MinSpeed);
        MaxSpeed.SetFirstValue(physics.MaxSpeed);
        Gravity.SetFirstValue(physics.Gravity);
        Wind.SetFirstValue(physics.Wind);
        Lifetime.SetFirstValue(physics.Lifetime);
        LifetimeJitter.SetFirstValue(physics.LifetimeJitter);
        HomingEnabled = physics.HomingEnabled;
        HomingTurnRate.SetFirstValue(physics.HomingTurnRate);
        HomingDuration.SetFirstValue(physics.HomingDuration);
        HomingDelay.SetFirstValue(physics.HomingDelay);

        var appearance = preset.Appearance;
        Shape = Enum.IsDefined((BulletShape)appearance.SpriteIndex)
            ? (BulletShape)appearance.SpriteIndex
            : BulletShape.Circle;
        Scale.SetFirstValue(appearance.Scale);
        ScaleJitter.SetFirstValue(appearance.ScaleJitter);
        ScaleVelocity.SetFirstValue(appearance.ScaleVelocity);
        RotationVelocity.SetFirstValue(appearance.RotationVelocity);
        AlignToDirection = appearance.AlignToDirection;
        ColorMode = appearance.ColorMode;
        PrimaryColor = appearance.PrimaryColor.ToMediaColor();
        SecondaryColor = appearance.SecondaryColor.ToMediaColor();
        HueVelocity.SetFirstValue(appearance.HueVelocity);
        HueStep.SetFirstValue(appearance.HueStep);
        Additive = appearance.Additive;
        GlowIntensity.SetFirstValue(appearance.GlowIntensity);
        Opacity.SetFirstValue(appearance.Opacity);
        FadeInDuration.SetFirstValue(appearance.FadeInDuration);
        FadeOutDuration.SetFirstValue(appearance.FadeOutDuration);
        TrailLength.SetFirstValue(appearance.TrailLength);
        TrailInterval.SetFirstValue(appearance.TrailInterval);
        TrailFade.SetFirstValue(appearance.TrailFade);
        TrailScale.SetFirstValue(appearance.TrailScale);

        SplitEnabled = preset.Split is not null;
        SplitDelay.SetFirstValue(preset.SplitDelay);
        if (preset.Split is { } split)
        {
            SplitCount.SetFirstValue(split.Count);
            SplitSpread.SetFirstValue(split.SpreadDegrees);
            SplitSpeed.SetFirstValue(split.Speed);
            SplitScaleFactor.SetFirstValue(split.ScaleFactor);
            SplitDestroyParent = split.DestroyParent;
            SplitMaxGeneration.SetFirstValue(split.MaxGeneration);
        }
    }

    /// <summary>
    /// 発射パターンの種類が変更された際、そのパターンの代表的なおすすめ数値を自動でセットする。
    /// </summary>
    private void ApplyPatternDefaults(PatternKind kind)
    {
        Stack.SetFirstValue(1);
        StackSpeedStep.SetFirstValue(40);
        StackAngleStep.SetFirstValue(0);
        BaseAngle.SetFirstValue(-90);
        AngleJitter.SetFirstValue(0);
        SpawnRadius.SetFirstValue(0);
        SpawnJitter.SetFirstValue(0);
        AimRate.SetFirstValue(0);
        BurstCount.SetFirstValue(1);
        BurstInterval.SetFirstValue(0.02);
        BurstCooldown.SetFirstValue(0);
        WallWidth.SetFirstValue(0);
        LaserSpacing.SetFirstValue(0);
        WhipAmplitude.SetFirstValue(0);
        WhipPeriod.SetFirstValue(1.2);

        switch (kind)
        {
            case PatternKind.Circle:
                Way.SetFirstValue(24);
                SpreadAngle.SetFirstValue(360);
                AngleStepPerShot.SetFirstValue(0);
                FireInterval.SetFirstValue(0.35);
                break;

            case PatternKind.Fan:
                Way.SetFirstValue(5);
                SpreadAngle.SetFirstValue(60);
                AngleStepPerShot.SetFirstValue(0);
                FireInterval.SetFirstValue(0.25);
                break;

            case PatternKind.Spiral:
                Way.SetFirstValue(4);
                SpreadAngle.SetFirstValue(360);
                AngleStepPerShot.SetFirstValue(13);
                FireInterval.SetFirstValue(0.08);
                break;

            case PatternKind.Aimed:
                Way.SetFirstValue(5);
                SpreadAngle.SetFirstValue(34);
                AimRate.SetFirstValue(100);
                BurstCount.SetFirstValue(3);
                BurstInterval.SetFirstValue(0.09);
                FireInterval.SetFirstValue(0.8);
                AngleStepPerShot.SetFirstValue(0);
                break;

            case PatternKind.Scatter:
                Way.SetFirstValue(6);
                SpreadAngle.SetFirstValue(360);
                AngleStepPerShot.SetFirstValue(0);
                AngleJitter.SetFirstValue(15);
                SpawnJitter.SetFirstValue(20);
                FireInterval.SetFirstValue(0.06);
                break;

            case PatternKind.Wall:
                Way.SetFirstValue(16);
                WallWidth.SetFirstValue(1280);
                SpreadAngle.SetFirstValue(360);
                AngleStepPerShot.SetFirstValue(0);
                BaseAngle.SetFirstValue(90); // 下向きに降る
                FireInterval.SetFirstValue(0.28);
                break;

            case PatternKind.Bloom:
                Way.SetFirstValue(16);
                Stack.SetFirstValue(3);
                StackSpeedStep.SetFirstValue(30);
                SpreadAngle.SetFirstValue(360);
                AngleStepPerShot.SetFirstValue(6);
                FireInterval.SetFirstValue(0.4);
                break;

            case PatternKind.Rose:
                Way.SetFirstValue(32);
                StackSpeedStep.SetFirstValue(20);
                SpreadAngle.SetFirstValue(360);
                AngleStepPerShot.SetFirstValue(8);
                FireInterval.SetFirstValue(0.5);
                break;

            case PatternKind.Laser:
                Way.SetFirstValue(24);
                LaserSpacing.SetFirstValue(24);
                SpreadAngle.SetFirstValue(360);
                AngleStepPerShot.SetFirstValue(20);
                FireInterval.SetFirstValue(0.22);
                break;

            case PatternKind.Whip:
                Way.SetFirstValue(5);
                SpreadAngle.SetFirstValue(60);
                AngleStepPerShot.SetFirstValue(0);
                WhipAmplitude.SetFirstValue(45);
                WhipPeriod.SetFirstValue(1.5);
                FireInterval.SetFirstValue(0.05);
                break;
        }
    }

    /// <summary>現在の設定からプリセットを作る。</summary>
    public Core.Presets.DanmakuPreset ToPreset(string name, string description = "")
        => Core.Presets.DanmakuPreset.FromEmitter(ToSettings(0), name, description);

    protected override IEnumerable<IAnimatable> GetAnimatables() => [
        X, Y, OrbitRadius, OrbitSpeed, OrbitPhase, SeedOffset,
        ScriptSpeedScale, ScriptRank,
        Way, Stack, StackSpeedStep, StackAngleStep, BaseAngle, SpreadAngle, AngleStepPerShot, AngleJitter,
        FireInterval, BurstCount, BurstInterval, BurstCooldown, StartTime, EndTime, SpawnRadius, SpawnJitter,
        AimRate, WallWidth, LaserSpacing, WhipAmplitude, WhipPeriod,
        Speed, SpeedJitter, SpeedStep, Acceleration, AngularVelocity, AngularVelocityJitter, Damping,
        MinSpeed, MaxSpeed, Gravity, Wind, Lifetime, LifetimeJitter,
        HomingTurnRate, HomingDuration, HomingDelay,
        Scale, ScaleJitter, ScaleVelocity, RotationVelocity, HueVelocity, HueStep, GlowIntensity, Opacity,
        FadeInDuration, FadeOutDuration, TrailLength, TrailInterval, TrailFade, TrailScale,
        SplitDelay, SplitCount, SplitSpread, SplitSpeed, SplitScaleFactor, SplitMaxGeneration,
        EnemyScale, EnemyRotation, EnemyOpacity, MagicCircleScale, MagicCircleRotationSpeed, MagicCircleOpacity, AuraIntensity,
        HitRadius
    ];
}
