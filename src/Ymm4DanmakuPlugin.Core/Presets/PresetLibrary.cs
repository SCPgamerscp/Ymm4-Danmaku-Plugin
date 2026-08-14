using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Model;
using Ymm4DanmakuPlugin.Core.Serialization;

namespace Ymm4DanmakuPlugin.Core.Presets;

/// <summary>
/// 東方 Project 風の弾幕プリセット集と、その保存・読み込み機能。
/// </summary>
public static class PresetLibrary
{
    private static readonly Lazy<IReadOnlyList<DanmakuPreset>> BuiltInPresets = new(CreateBuiltIn);

    /// <summary>同梱の組み込みプリセット。</summary>
    public static IReadOnlyList<DanmakuPreset> BuiltIn => BuiltInPresets.Value;

    /// <summary>名前でプリセットを検索する。</summary>
    public static DanmakuPreset? Find(string name) =>
        BuiltIn.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal));

    /// <summary>プリセットを JSON ファイルへ保存する。</summary>
    public static void Save(DanmakuPreset preset, string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, preset.ToJson());
    }

    /// <summary>プリセット集を JSON ファイルへ保存する。</summary>
    public static void SaveCollection(DanmakuPresetCollection collection, string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, collection.ToJson());
    }

    /// <summary>JSON ファイルからプリセットを読み込む。単体・コレクションの両方に対応。</summary>
    public static IReadOnlyList<DanmakuPreset> Load(string path)
    {
        if (!File.Exists(path)) return [];

        var json = File.ReadAllText(path);

        try
        {
            var collection = DanmakuJson.Deserialize<DanmakuPresetCollection>(json);
            if (collection is { Presets.Length: > 0 }) return collection.Presets;
        }
        catch (System.Text.Json.JsonException)
        {
            // 単体プリセットとして読み直す
        }

        try
        {
            var preset = DanmakuJson.Deserialize<DanmakuPreset>(json);
            return preset is null ? [] : [preset];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    /// <summary>組み込みプリセット一式を 1 ファイルへ書き出す。</summary>
    public static void ExportBuiltIn(string path) =>
        SaveCollection(new DanmakuPresetCollection
        {
            Name = "東方風弾幕サンプルプリセット集",
            Presets = [.. BuiltIn],
        }, path);

    private static IReadOnlyList<DanmakuPreset> CreateBuiltIn() =>
    [
        CreateFullCircle(),
        CreateSpiral(),
        CreateAimedFan(),
        CreateFlowerPetal(),
        CreateRain(),
        CreateHomingButterfly(),
        CreateSplitStar(),
        CreateLaserFan(),
        CreateScatterStorm(),
        CreateRainbowRing(),
    ];

    // -----------------------------------------------------------------------
    // 組み込みプリセット定義
    // -----------------------------------------------------------------------

    private static DanmakuPreset CreateFullCircle() => new()
    {
        Name = "全方位リング",
        Description = "基本となる全方位弾。等間隔に配置した弾を一定周期で放つ。",
        Tags = ["基本", "全方位"],
        Pattern = new PatternSettings
        {
            Kind = PatternKind.Circle,
            Way = 24,
            Stack = 1,
            SpreadAngle = 360,
            FireInterval = 0.35,
            BaseAngle = -90,
        },
        Physics = new BulletPhysics { Speed = 260, Lifetime = 6, MaxSpeed = 900 },
        Appearance = new BulletAppearance
        {
            ColorMode = ColorMode.Single,
            PrimaryColor = new BulletColor(1f, 0.35f, 0.55f, 1f),
            Scale = 1.0,
            Additive = true,
        },
    };

    private static DanmakuPreset CreateSpiral() => new()
    {
        Name = "螺旋乱舞",
        Description = "発射のたびに基準角をずらして描く螺旋弾幕。AngleStepPerShot が回転量。",
        Tags = ["螺旋", "回転"],
        Pattern = new PatternSettings
        {
            Kind = PatternKind.Spiral,
            Way = 5,
            Stack = 1,
            SpreadAngle = 360,
            AngleStepPerShot = 13,
            FireInterval = 0.06,
            BaseAngle = -90,
        },
        Physics = new BulletPhysics { Speed = 230, Lifetime = 7, MaxSpeed = 900 },
        Appearance = new BulletAppearance
        {
            ColorMode = ColorMode.Gradient,
            PrimaryColor = new BulletColor(0.4f, 0.8f, 1f, 1f),
            SecondaryColor = new BulletColor(0.9f, 0.4f, 1f, 1f),
            ColorGradientSteps = 5,
            TrailLength = 6,
            TrailScale = 0.5,
            Additive = true,
        },
    };

    private static DanmakuPreset CreateAimedFan() => new()
    {
        Name = "自機狙い扇",
        Description = "ターゲットを狙って扇状に撃つ。3 連バーストで圧力をかける。",
        Tags = ["狙い弾", "扇"],
        Pattern = new PatternSettings
        {
            Kind = PatternKind.Aimed,
            Way = 5,
            SpreadAngle = 34,
            FireInterval = 0.8,
            BurstCount = 3,
            BurstInterval = 0.09,
            AimAtTarget = true,
        },
        Physics = new BulletPhysics { Speed = 420, Lifetime = 5, MaxSpeed = 1200 },
        Appearance = new BulletAppearance
        {
            ColorMode = ColorMode.Single,
            PrimaryColor = new BulletColor(1f, 0.9f, 0.35f, 1f),
            Scale = 0.85,
            AlignToDirection = true,
        },
    };

    private static DanmakuPreset CreateFlowerPetal() => new()
    {
        Name = "花弁",
        Description = "黄金角で配置し、速度差で花のように広がる弾幕。",
        Tags = ["花", "装飾"],
        Pattern = new PatternSettings
        {
            Kind = PatternKind.Rose,
            Way = 36,
            SpreadAngle = 360,
            StackSpeedStep = 22,
            AngleStepPerShot = 9,
            FireInterval = 0.5,
        },
        Physics = new BulletPhysics
        {
            Speed = 120,
            Acceleration = 30,
            Lifetime = 8,
            Damping = 0.9,
            MaxSpeed = 700,
        },
        Appearance = new BulletAppearance
        {
            ColorMode = ColorMode.Rainbow,
            HueVelocity = 60,
            HueStep = 10,
            Scale = 0.9,
            TrailLength = 10,
            TrailScale = 0.35,
            TrailFade = 0.05,
        },
    };

    private static DanmakuPreset CreateRain() => new()
    {
        Name = "雨弾",
        Description = "画面上部から降り注ぐ壁弾。重力で加速する。",
        Tags = ["壁", "重力"],
        Pattern = new PatternSettings
        {
            Kind = PatternKind.Wall,
            Way = 18,
            WallWidth = 1800,
            BaseAngle = 90,
            FireInterval = 0.28,
            AngleJitter = 6,
        },
        Physics = new BulletPhysics
        {
            Speed = 180,
            Gravity = 220,
            Lifetime = 8,
            SpeedJitter = 30,
            MaxSpeed = 1100,
        },
        Appearance = new BulletAppearance
        {
            ColorMode = ColorMode.Single,
            PrimaryColor = new BulletColor(0.55f, 0.8f, 1f, 0.95f),
            Scale = 0.7,
            AlignToDirection = true,
            TrailLength = 5,
            TrailScale = 0.4,
        },
    };

    private static DanmakuPreset CreateHomingButterfly() => new()
    {
        Name = "追尾蝶",
        Description = "ゆるやかに旋回しながら追尾するホーミング弾。",
        Tags = ["ホーミング", "追尾"],
        Pattern = new PatternSettings
        {
            Kind = PatternKind.Circle,
            Way = 8,
            SpreadAngle = 360,
            FireInterval = 0.9,
            SpawnRadius = 40,
        },
        Physics = new BulletPhysics
        {
            Speed = 150,
            Acceleration = 60,
            Lifetime = 7,
            HomingEnabled = true,
            HomingTurnRate = 110,
            HomingDuration = 3.0,
            HomingDelay = 0.4,
            MaxSpeed = 620,
        },
        Appearance = new BulletAppearance
        {
            ColorMode = ColorMode.Palette,
            Scale = 0.95,
            AnimationFps = 12,
            TrailLength = 14,
            TrailInterval = 1.0 / 60.0,
            TrailScale = 0.3,
            AlignToDirection = true,
        },
    };

    private static DanmakuPreset CreateSplitStar() => new()
    {
        Name = "多段分裂星",
        Description = "一定時間後に分裂し、さらに分裂する多段弾幕。",
        Tags = ["分裂", "多段"],
        Pattern = new PatternSettings
        {
            Kind = PatternKind.Circle,
            Way = 6,
            SpreadAngle = 360,
            FireInterval = 1.1,
            AngleStepPerShot = 11,
        },
        Physics = new BulletPhysics { Speed = 300, Damping = 0.35, Lifetime = 7, MinSpeed = 40 },
        Appearance = new BulletAppearance
        {
            ColorMode = ColorMode.Gradient,
            PrimaryColor = new BulletColor(1f, 0.95f, 0.6f, 1f),
            SecondaryColor = new BulletColor(1f, 0.4f, 0.2f, 1f),
            Scale = 1.15,
        },
        SplitDelay = 0.75,
        Split = new SplitSpec
        {
            Count = 8,
            SpreadDegrees = 360,
            Speed = 230,
            ScaleFactor = 0.7,
            MaxGeneration = 2,
            NextDelay = 0.6,
            DestroyParent = true,
            Next = new SplitSpec
            {
                Count = 6,
                SpreadDegrees = 360,
                Speed = 170,
                ScaleFactor = 0.65,
                MaxGeneration = 2,
                DestroyParent = true,
            },
        },
    };

    private static DanmakuPreset CreateLaserFan() => new()
    {
        Name = "疑似レーザー扇",
        Description = "直線状に密に並べた弾でレーザーのように見せる。",
        Tags = ["レーザー", "直線"],
        Pattern = new PatternSettings
        {
            Kind = PatternKind.Laser,
            Way = 26,
            LaserSpacing = 26,
            SpawnRadius = 30,
            AngleStepPerShot = 24,
            FireInterval = 0.22,
        },
        Physics = new BulletPhysics { Speed = 90, Lifetime = 2.2, MaxSpeed = 400 },
        Appearance = new BulletAppearance
        {
            ColorMode = ColorMode.Single,
            PrimaryColor = new BulletColor(0.6f, 1f, 0.9f, 1f),
            Scale = 0.6,
            AlignToDirection = true,
            FadeInDuration = 0.08,
            FadeOutDuration = 0.4,
            GlowIntensity = 1.6,
        },
    };

    private static DanmakuPreset CreateScatterStorm() => new()
    {
        Name = "乱れ撃ち",
        Description = "ランダムな方向・速度でばら撒く嵐のような弾幕。",
        Tags = ["ランダム", "嵐"],
        Pattern = new PatternSettings
        {
            Kind = PatternKind.Scatter,
            Way = 6,
            SpreadAngle = 360,
            FireInterval = 0.05,
            SpawnJitter = 24,
        },
        Physics = new BulletPhysics
        {
            Speed = 240,
            SpeedJitter = 140,
            AngularVelocityJitter = 40,
            Lifetime = 5,
            LifetimeJitter = 1.5,
            MaxSpeed = 900,
        },
        Appearance = new BulletAppearance
        {
            ColorMode = ColorMode.Random,
            Scale = 0.75,
            ScaleJitter = 0.25,
            Additive = true,
        },
    };

    private static DanmakuPreset CreateRainbowRing() => new()
    {
        Name = "虹輪",
        Description = "多重リングを速度差で展開し、色相を回し続ける華やかな弾幕。",
        Tags = ["全方位", "虹", "多重"],
        Pattern = new PatternSettings
        {
            Kind = PatternKind.Bloom,
            Way = 20,
            Stack = 4,
            StackSpeedStep = 55,
            SpreadAngle = 360,
            FireInterval = 0.85,
            AngleStepPerShot = 6,
        },
        Physics = new BulletPhysics { Speed = 170, Lifetime = 7.5, MaxSpeed = 800 },
        Appearance = new BulletAppearance
        {
            ColorMode = ColorMode.Rainbow,
            HueVelocity = 150,
            HueStep = 18,
            Scale = 0.95,
            Additive = true,
            TrailLength = 4,
            TrailScale = 0.5,
        },
    };
}
