using Ymm4DanmakuPlugin.Core.Audio;
using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Mathematics;
using Ymm4DanmakuPlugin.Core.Model;

namespace Ymm4DanmakuPlugin.Core.Engine;

/// <summary>
/// タイムライン上の任意フレームへシークできるようにした弾幕シミュレーター。
/// <para>
/// 動画編集ソフトは「10 秒目だけ描画する」「巻き戻す」といった非連続なアクセスを行う。
/// 弾幕は履歴依存のシミュレーションなので、
/// ・前進シーク → 差分だけ進める
/// ・後退シーク → 先頭から再計算する
/// という方針で常に正しい状態を得る。
/// </para>
/// </summary>
public sealed class DanmakuSimulator
{
    private DanmakuSettings settings;
    private string settingsSignature;
    private double timelineTime;

    /// <summary>内部エンジン。</summary>
    public DanmakuEngine Engine { get; private set; }

    /// <summary>直近のシークで発生した警告 (外部データの読み込みエラーなど)。</summary>
    public IReadOnlyList<string> Warnings { get; private set; } = [];

    /// <summary>1 回のシークで計算する最大秒数。これを超えるシークは打ち切る。</summary>
    public double MaxSimulationSeconds { get; set; } = 600.0;

    /// <summary>最後に再計算 (巻き戻し) が発生した回数。パフォーマンス計測用。</summary>
    public int RewindCount { get; private set; }

    public DanmakuSimulator(DanmakuSettings settings)
    {
        this.settings = settings;
        var warnings = new List<string>();
        var behaviors = DanmakuBehaviorFactory.CreateAll(settings, warnings);
        Warnings = warnings;
        Engine = new DanmakuEngine(settings, behaviors);
        settingsSignature = CreateSignature(settings);
    }

    /// <summary>生存中の弾。</summary>
    public IReadOnlyList<Bullet> Bullets => Engine.Pool.ActiveBullets;

    /// <summary>効果音イベント。</summary>
    public SoundEventLog SoundLog => Engine.SoundLog;

    /// <summary>
    /// キーフレームで時間変化する値 (エミッター位置・ターゲット位置) の供給元。
    /// <para>
    /// ここへ関数を設定しても設定署名は変化しないため、
    /// キーフレームを動かしてもシミュレーションは作り直されない。
    /// </para>
    /// </summary>
    public LiveValueSource Live => Engine.Live;

    /// <summary>現在のシミュレーション時刻 (秒)。</summary>
    public double CurrentTime => Engine.CurrentTime;

    public DanmakuSettings Settings => settings;

    /// <summary>
    /// 設定を適用する。弾幕の構造に関わる変更があった場合のみ再構築する。
    /// (座標やターゲット位置など、途中変更しても破綻しない項目では再構築しない)
    /// </summary>
    public void Configure(DanmakuSettings newSettings)
    {
        var signature = CreateSignature(newSettings);
        settings = newSettings;

        if (string.Equals(signature, settingsSignature, StringComparison.Ordinal))
        {
            // 構造は同じなので、軽い項目だけ反映する
            Engine.Settings = newSettings;
            Engine.TargetPosition = new Vec2(newSettings.Collision.TargetX, newSettings.Collision.TargetY);
            return;
        }

        settingsSignature = signature;

        var warnings = new List<string>();
        var behaviors = DanmakuBehaviorFactory.CreateAll(newSettings, warnings);
        Warnings = warnings;
        Engine.Reconfigure(newSettings, behaviors);
    }

    /// <summary>指定時刻までシミュレーションを進める (必要なら先頭から再計算)。</summary>
    public void SeekTo(double timeSeconds)
    {
        if (timeSeconds < 0) timeSeconds = 0;
        if (timeSeconds > MaxSimulationSeconds) timeSeconds = MaxSimulationSeconds;

        const double epsilon = 1e-6;

        if (timeSeconds < timelineTime - epsilon)
        {
            Engine.Reset();
            timelineTime = 0;
            RewindCount++;
        }

        if (Live.TimeScale is null)
        {
            // 動的 TimeScale がない場合は一括で進める
            var delta = timeSeconds - timelineTime;
            if (delta > epsilon)
            {
                var scale = Math.Max(0.0, Settings.TimeScale);
                if (scale > 0)
                {
                    Engine.Advance(delta * scale);
                }
                timelineTime = timeSeconds;
            }
        }
        else
        {
            // 動的 TimeScale (キーフレーム) がある場合は格子刻みで積分する
            var dt = Engine.StepSize;
            while (timelineTime < timeSeconds - epsilon)
            {
                var chunk = Math.Min(dt, timeSeconds - timelineTime);
                var scale = Math.Max(0.0, Live.TimeScale(timelineTime) ?? Settings.TimeScale);
                if (scale > 0 && chunk > 0)
                {
                    Engine.Advance(chunk * scale);
                }
                timelineTime += chunk;
            }
            timelineTime = timeSeconds;
        }
    }

    /// <summary>指定フレームまでシミュレーションを進める。</summary>
    public void SeekToFrame(int frame, double fps)
    {
        if (fps <= 0) fps = 60;
        SeekTo(frame / fps);
    }

    /// <summary>先頭から作り直す。</summary>
    public void Reset()
    {
        Engine.Reset();
        timelineTime = 0;
        RewindCount = 0;
    }

    /// <summary>
    /// 弾幕の「構造」を表す署名。
    /// これが変わったときだけシミュレーションを作り直せばよい。
    /// </summary>
    private static string CreateSignature(DanmakuSettings settings)
    {
        var hash = new HashCode();
        hash.Add(settings.Seed);
        hash.Add(settings.MaxBullets);
        hash.Add(settings.FixedTimeStep);
        hash.Add(settings.TimeScale);
        hash.Add(settings.CanvasWidth);
        hash.Add(settings.CanvasHeight);
        hash.Add(settings.BoundsMargin);
        hash.Add((int)settings.OutOfBounds);
        hash.Add(settings.Emitters.Length);

        // ターゲット座標はプレビュー上のドラッグで頻繁に動くため署名に含めない。
        // (含めるとドラッグ中に毎回シミュレーションが作り直されて重くなる)
        hash.Add(settings.Collision with { TargetX = 0, TargetY = 0 });
        hash.Add(settings.PlayerShot);
        hash.Add(settings.FireSound);
        hash.Add(settings.ChangeSound);
        hash.Add(settings.HitSound);
        hash.Add(settings.VanishSound);

        foreach (var emitter in settings.Emitters)
            hash.Add(emitter);

        return hash.ToHashCode().ToString("X8");
    }
}
