using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin;
using Ymm4DanmakuPlugin.Core.Configuration;
using CoreSoundSettings = Ymm4DanmakuPlugin.Core.Configuration.SoundSettings;

namespace Ymm4DanmakuPlugin.Settings;

/// <summary>
/// 効果音 1 種類ぶんのユーザー設定。
/// YMM4 の設定画面 (ファイル → 設定 → 弾幕効果音) から自由に変更できる。
/// </summary>
public class DanmakuSoundEntry : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>音声ファイルのパス。空のときは効果音を鳴らさない。</summary>
    public string FilePath
    {
        get => filePath;
        set => Set(ref filePath, value);
    }
    private string filePath = string.Empty;

    /// <summary>音量 (0〜1)。</summary>
    public double Volume
    {
        get => volume;
        set => Set(ref volume, Math.Clamp(value, 0.0, 1.0));
    }
    private double volume = 0.6;

    /// <summary>基準ピッチ (半音)。</summary>
    public double PitchSemitones
    {
        get => pitchSemitones;
        set => Set(ref pitchSemitones, Math.Clamp(value, -24.0, 24.0));
    }
    private double pitchSemitones;

    /// <summary>
    /// ピッチのランダム変調幅 (± 半音)。
    /// 同時に大量の弾が出るとき、まったく同じ音が重なると機械的に聞こえるため、
    /// わずかにピッチをずらして東方風の「シャラシャラ」した質感を作る。
    /// </summary>
    public double PitchJitterSemitones
    {
        get => pitchJitterSemitones;
        set => Set(ref pitchJitterSemitones, Math.Clamp(value, 0.0, 12.0));
    }
    private double pitchJitterSemitones = 1.5;

    /// <summary>1 秒あたりの最大発音数。音が飽和して耳障りになるのを防ぐ。</summary>
    public int MaxVoicesPerSecond
    {
        get => maxVoicesPerSecond;
        set => Set(ref maxVoicesPerSecond, Math.Clamp(value, 1, 240));
    }
    private int maxVoicesPerSecond = 20;

    /// <summary>同一フレーム内の同種の発音を 1 回にまとめる。</summary>
    public bool CoalesceSimultaneous
    {
        get => coalesceSimultaneous;
        set => Set(ref coalesceSimultaneous, value);
    }
    private bool coalesceSimultaneous = true;

    /// <summary>コアエンジンの設定へ変換する。</summary>
    public CoreSoundSettings ToSoundSettings(bool isEnabled) => new()
    {
        // ファイルが指定されていなければ無効扱い (存在しないパスでも記録自体は行う)
        IsEnabled = isEnabled && !string.IsNullOrWhiteSpace(FilePath),
        FilePath = string.IsNullOrWhiteSpace(FilePath) ? null : FilePath,
        Volume = Volume,
        PitchSemitones = PitchSemitones,
        PitchJitterSemitones = PitchJitterSemitones,
        MaxVoicesPerSecond = MaxVoicesPerSecond,
        CoalesceSimultaneous = CoalesceSimultaneous,
    };

    private void Set<T>(ref T storage, T value, [System.Runtime.CompilerServices.CallerMemberName] string name = "")
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return;
        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>
/// 弾幕プラグインの効果音設定。
/// <para>
/// 開発計画書の「ユーザーが YMM4 の設定画面から自由に効果音を指定できる」要件に対応する。
/// <see cref="SettingsBase{T}"/> により <c>%AppData%</c> 配下へ JSON として自動保存される。
/// </para>
/// <para>
/// 音声ファイルはプロジェクトではなく<b>アプリ設定</b>に保存する。
/// 弾幕アイテム側は「発射音を鳴らすか」の ON/OFF だけを持ち、
/// 実際の音源はユーザーの好みとして全プロジェクト共通になる。
/// </para>
/// </summary>
public class DanmakuSoundSettings : SettingsBase<DanmakuSoundSettings>
{
    public override SettingsCategory Category => SettingsCategory.Other;

    public override string Name => "弾幕効果音";

    /// <summary>専用画面は用意せず、属性ベースの自動生成 UI を使う。</summary>
    public override bool HasSettingView => false;

    public override object? SettingView => null;

    public override void Initialize() { }

    [Display(GroupName = "発射音", Name = "音声ファイル", Description = "弾を発射した瞬間に鳴る音。")]
    [FileSelector(YukkuriMovieMaker.Settings.FileGroupType.AudioItem)]
    public string FireFilePath
    {
        get => Fire.FilePath;
        set => Fire.FilePath = value;
    }

    [Display(GroupName = "発射音", Name = "音量")]
    [TextBoxSlider("F2", "", 0, 1)]
    [DefaultValue(0.6d)]
    [Range(0, 1)]
    public double FireVolume
    {
        get => Fire.Volume;
        set => Fire.Volume = value;
    }

    [Display(GroupName = "発射音", Name = "ピッチ変調", Description = "同時発射時に音が単調にならないよう、±この半音幅でランダムにずらします。")]
    [TextBoxSlider("F2", "半音", 0, 6)]
    [DefaultValue(1.5d)]
    [Range(0, 12)]
    public double FirePitchJitter
    {
        get => Fire.PitchJitterSemitones;
        set => Fire.PitchJitterSemitones = value;
    }

    [Display(GroupName = "発射音", Name = "毎秒の上限", Description = "1 秒間に鳴らす最大回数。音の飽和を防ぎます。")]
    [TextBoxSlider("F0", "回/秒", 1, 60)]
    [DefaultValue(20)]
    [Range(1, 240)]
    public int FireMaxVoices
    {
        get => Fire.MaxVoicesPerSecond;
        set => Fire.MaxVoicesPerSecond = value;
    }

    [Display(GroupName = "変化音", Name = "音声ファイル", Description = "弾が分裂・軌道変化したときに鳴る音。")]
    [FileSelector(YukkuriMovieMaker.Settings.FileGroupType.AudioItem)]
    public string ChangeFilePath
    {
        get => Change.FilePath;
        set => Change.FilePath = value;
    }

    [Display(GroupName = "変化音", Name = "音量")]
    [TextBoxSlider("F2", "", 0, 1)]
    [DefaultValue(0.5d)]
    [Range(0, 1)]
    public double ChangeVolume
    {
        get => Change.Volume;
        set => Change.Volume = value;
    }

    [Display(GroupName = "変化音", Name = "ピッチ変調")]
    [TextBoxSlider("F2", "半音", 0, 6)]
    [DefaultValue(2d)]
    [Range(0, 12)]
    public double ChangePitchJitter
    {
        get => Change.PitchJitterSemitones;
        set => Change.PitchJitterSemitones = value;
    }

    [Display(GroupName = "被弾音", Name = "音声ファイル", Description = "ターゲットに弾が当たったときに鳴る音。")]
    [FileSelector(YukkuriMovieMaker.Settings.FileGroupType.AudioItem)]
    public string HitFilePath
    {
        get => Hit.FilePath;
        set => Hit.FilePath = value;
    }

    [Display(GroupName = "被弾音", Name = "音量")]
    [TextBoxSlider("F2", "", 0, 1)]
    [DefaultValue(0.8d)]
    [Range(0, 1)]
    public double HitVolume
    {
        get => Hit.Volume;
        set => Hit.Volume = value;
    }

    [Display(GroupName = "被弾音", Name = "ピッチ変調")]
    [TextBoxSlider("F2", "半音", 0, 6)]
    [DefaultValue(1d)]
    [Range(0, 12)]
    public double HitPitchJitter
    {
        get => Hit.PitchJitterSemitones;
        set => Hit.PitchJitterSemitones = value;
    }

    [Display(GroupName = "消滅音", Name = "音声ファイル", Description = "弾が画面外や寿命で消えたときに鳴る音。")]
    [FileSelector(YukkuriMovieMaker.Settings.FileGroupType.AudioItem)]
    public string VanishFilePath
    {
        get => Vanish.FilePath;
        set => Vanish.FilePath = value;
    }

    [Display(GroupName = "消滅音", Name = "音量")]
    [TextBoxSlider("F2", "", 0, 1)]
    [DefaultValue(0.35d)]
    [Range(0, 1)]
    public double VanishVolume
    {
        get => Vanish.Volume;
        set => Vanish.Volume = value;
    }

    [Display(GroupName = "消滅音", Name = "ピッチ変調")]
    [TextBoxSlider("F2", "半音", 0, 6)]
    [DefaultValue(2.5d)]
    [Range(0, 12)]
    public double VanishPitchJitter
    {
        get => Vanish.PitchJitterSemitones;
        set => Vanish.PitchJitterSemitones = value;
    }

    [Display(GroupName = "自機ショット発射音", Name = "音声ファイル", Description = "自機がショットを発射した瞬間に鳴る音。")]
    [FileSelector(YukkuriMovieMaker.Settings.FileGroupType.AudioItem)]
    public string PlayerShotFilePath
    {
        get => PlayerShot.FilePath;
        set => PlayerShot.FilePath = value;
    }

    [Display(GroupName = "自機ショット発射音", Name = "音量")]
    [TextBoxSlider("F2", "", 0, 1)]
    [DefaultValue(0.5d)]
    [Range(0, 1)]
    public double PlayerShotVolume
    {
        get => PlayerShot.Volume;
        set => PlayerShot.Volume = value;
    }

    [Display(GroupName = "自機ショット発射音", Name = "ピッチ変調")]
    [TextBoxSlider("F2", "半音", 0, 6)]
    [DefaultValue(1.0d)]
    [Range(0, 12)]
    public double PlayerShotPitchJitter
    {
        get => PlayerShot.PitchJitterSemitones;
        set => PlayerShot.PitchJitterSemitones = value;
    }

    [Display(GroupName = "自機ショット発射音", Name = "毎秒の上限", Description = "1 秒間に鳴らす最大回数。")]
    [TextBoxSlider("F0", "回/秒", 1, 60)]
    [DefaultValue(30)]
    [Range(1, 240)]
    public int PlayerShotMaxVoices
    {
        get => PlayerShot.MaxVoicesPerSecond;
        set => PlayerShot.MaxVoicesPerSecond = value;
    }

    /// <summary>プリセット保存先フォルダ。空のときは既定 (プラグインフォルダ配下) を使う。</summary>
    [Display(GroupName = "プリセット", Name = "保存フォルダ", Description = "弾幕プリセット (.json) を読み書きするフォルダ。空欄なら既定のフォルダを使います。")]
    [DirectorySelector]
    public string PresetDirectory
    {
        get => presetDirectory;
        set => presetDirectory = value ?? string.Empty;
    }
    private string presetDirectory = string.Empty;

    // ---- 実体 (JSON へ保存される) ----

    public DanmakuSoundEntry Fire { get; set; } = new() { Volume = 0.6, PitchJitterSemitones = 1.5 };

    public DanmakuSoundEntry Change { get; set; } = new() { Volume = 0.5, PitchJitterSemitones = 2.0 };

    public DanmakuSoundEntry Hit { get; set; } = new() { Volume = 0.8, PitchJitterSemitones = 1.0 };

    public DanmakuSoundEntry Vanish { get; set; } = new() { Volume = 0.35, PitchJitterSemitones = 2.5 };

    public DanmakuSoundEntry PlayerShot { get; set; } = new() { Volume = 0.5, PitchJitterSemitones = 1.0, MaxVoicesPerSecond = 30 };

    /// <summary>種類を指定して設定を取得する。</summary>
    public DanmakuSoundEntry GetEntry(DanmakuSoundKind kind) => kind switch
    {
        DanmakuSoundKind.Fire => Fire,
        DanmakuSoundKind.Change => Change,
        DanmakuSoundKind.Hit => Hit,
        DanmakuSoundKind.PlayerShot => PlayerShot,
        _ => Vanish,
    };

    /// <summary>種類を指定してコアエンジンの設定へ変換する。</summary>
    public CoreSoundSettings ToSoundSettings(DanmakuSoundKind kind, bool isEnabled) =>
        GetEntry(kind).ToSoundSettings(isEnabled);

    /// <summary>プリセットの保存先フォルダを解決する。</summary>
    public string ResolvePresetDirectory()
    {
        if (!string.IsNullOrWhiteSpace(PresetDirectory)) return PresetDirectory;

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "YukkuriMovieMaker",
            "DanmakuPresets");
    }
}
