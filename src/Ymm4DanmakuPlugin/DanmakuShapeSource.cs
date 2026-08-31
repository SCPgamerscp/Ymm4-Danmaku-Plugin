using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Engine;
using Ymm4DanmakuPlugin.Core.Mathematics;
using Ymm4DanmakuPlugin.Core.Rendering;
using Ymm4DanmakuPlugin.Interop;
using Ymm4DanmakuPlugin.Parameters;
using Ymm4DanmakuPlugin.Rendering;

namespace Ymm4DanmakuPlugin;

/// <summary>
/// 弾幕図形の描画ソース。
/// <para>
/// YMM4 は表示するフレームが変わるたびに <see cref="Update"/> を呼ぶ。
/// ここで
/// </para>
/// <list type="number">
/// <item>編集項目 → コアエンジン設定へ変換し、必要なら再構築する</item>
/// <item>キーフレーム値の供給関数 (<see cref="LiveValueSource"/>) を張り替える</item>
/// <item>当該フレームまでシミュレーションをシークする</item>
/// <item>描画データを組み立てて Direct2D へ描く</item>
/// </list>
/// <para>
/// <b>プレビュー上のドラッグ</b>は <see cref="IShapeSource2.Controllers"/> で実現している。
/// エミッター位置とターゲット位置に制御点を出し、ドラッグ量を
/// <c>Animation.AddToEachValues</c> でキーフレーム値へ加算する。
/// </para>
/// </summary>
public sealed class DanmakuShapeSource : IShapeSource2
{
    private readonly IGraphicsDevicesAndContext devices;
    private readonly DanmakuShapeParameter parameter;
    private readonly DanmakuRenderer renderer;
    private readonly RenderBatchBuilder batchBuilder = new() { MaxSpriteSlots = SpriteSlots.Capacity };
    private readonly DisposeCollector disposer = new();

    private DanmakuSimulator? simulator;
    private ID2D1CommandList? output;
    private ID2D1CommandList? emptyOutput;

    /// <summary>直近の描画で使った情報 (再描画の省略判定に使う)。</summary>
    private int lastFrame = -1;
    private int lastFps;
    private int lastCanvasWidth;
    private int lastCanvasHeight;
    private int lastTotalFrame;

    public DanmakuShapeSource(IGraphicsDevicesAndContext devices, DanmakuShapeParameter parameter)
    {
        this.devices = devices;
        this.parameter = parameter;

        renderer = new DanmakuRenderer(devices);
        disposer.Collect(renderer);

        // 音声エフェクト側から設定を引けるように登録する (弱参照なので解放を妨げない)
        Audio.DanmakuChannelBus.Register(parameter);
    }

    /// <summary>YMM4 が合成に使う描画結果。</summary>
    public ID2D1Image Output => output ?? GetEmptyOutput();

    /// <summary>プレビューエリアに表示する制御点。</summary>
    public IEnumerable<VideoController> Controllers => BuildControllers();

    /// <summary>直近のシミュレーションで生存していた弾数 (デバッグ用)。</summary>
    public int LastBulletCount { get; private set; }

    public void Update(TimelineItemSourceDescription description)
    {
        var canvasWidth = Math.Max(1, description.ScreenSize.Width);
        var canvasHeight = Math.Max(1, description.ScreenSize.Height);
        var fps = description.FPS > 0 ? description.FPS : 60;
        var frame = description.ItemPosition.Frame;
        var totalFrame = Math.Max(1, description.ItemDuration.Frame);

        // 音声側が同じ条件でシミュレーションを再現できるよう記録しておく
        parameter.LastCanvasWidth = canvasWidth;
        parameter.LastCanvasHeight = canvasHeight;

        var settings = parameter.ToSettings(canvasWidth, canvasHeight) with { TimeScale = 1.0 };

        if (simulator is null)
        {
            simulator = new DanmakuSimulator(settings);
        }
        else
        {
            // 構造が変わっていなければ再構築されない (Configure 内で署名比較している)
            simulator.Configure(settings);
        }

        // --- キーフレーム値の供給関数を張り替える ---
        // 毎フレーム作り直しても設定署名は変化しないため、シミュレーションは維持される。
        WireLiveValues(simulator, fps, totalFrame);

        // --- ユーザー指定画像をスロットへ読み込む ---
        LoadCustomImages();

        // 外部レイヤー間の相互参照（自機・ボス・ダメージ）のためにシミュレーション前にレイヤーを登録
        Collision.DanmakuCollisionBus.RegisterLayer(this, parameter, fps, totalFrame);

        // キーフレームやスライダーの編集が一時停止中に行われても確実に最新の弾幕状態を反映するため、
        // 常に先頭から現在フレームまで確定的にシミュレーションを再現して描画する
        simulator.Reset();
        var simTime = ComputeSimulatedTime(frame, fps, totalFrame);
        simulator.SeekTo(simTime);
        LastBulletCount = simulator.Bullets.Count;

        // シミュレーション結果のスナップショットを不変配列として公開 (他スレッドから安全に参照可能)
        var damageSnapshot = simulator.Engine.DamageHistory.ToArray();
        Collision.BulletCancelArea[]? cancelersSnapshot = null;
        if (parameter.PlayerShotCancelEnemyBullets.GetValue(frame, totalFrame, fps) >= 0.5)
        {
            var cancelList = new List<Collision.BulletCancelArea>();
            foreach (var bullet in simulator.Bullets)
            {
                if (bullet.IsPlayerShot && bullet.IsAlive && bullet.CancelEnemyBullets)
                {
                    cancelList.Add(new Collision.BulletCancelArea(bullet.Position, bullet.HitRadius * Math.Abs(bullet.Scale)));
                }
            }
            if (cancelList.Count > 0) cancelersSnapshot = cancelList.ToArray();
        }
        var enemyHitboxesSnapshot = simulator.Engine.EnemyHitboxes.ToArray();
        var targetHitboxesSnapshot = simulator.Engine.TargetHitboxes.ToArray();
        Collision.DanmakuCollisionBus.PublishSnapshots(this, damageSnapshot, cancelersSnapshot, enemyHitboxesSnapshot, targetHitboxesSnapshot);

        lastFrame = frame;
        lastFps = fps;
        lastCanvasWidth = canvasWidth;
        lastCanvasHeight = canvasHeight;
        lastTotalFrame = totalFrame;

        var targetX = (float)parameter.TargetX.GetValue(frame, totalFrame, fps);
        var targetY = (float)parameter.TargetY.GetValue(frame, totalFrame, fps);
        var targetScale = (float)parameter.TargetScale.GetValue(frame, totalFrame, fps);
        var targetRotation = (float)parameter.TargetRotation.GetValue(frame, totalFrame, fps);
        var targetOpacity = (float)parameter.TargetOpacity.GetValue(frame, totalFrame, fps);
        var targetRadius = (float)parameter.TargetRadius.GetValue(frame, totalFrame, fps);

        var collisionEnabled = parameter.CollisionEnabled.GetValue(frame, totalFrame, fps) >= 0.5;
        var showTargetMarker = parameter.ShowTargetMarker.GetValue(frame, totalFrame, fps) >= 0.5;
        var hasTarget = collisionEnabled && (showTargetMarker || parameter.HasCustomTargetImage);
        var targetInfo = new TargetRenderInfo(
            Enabled: collisionEnabled && (parameter.HasCustomTargetImage || showTargetMarker),
            X: targetX,
            Y: targetY,
            Scale: targetScale,
            Rotation: targetRotation,
            Opacity: targetOpacity,
            Radius: targetRadius,
            ShowMarker: showTargetMarker,
            HasCustomImage: parameter.HasCustomTargetImage
        );

        var enemies = new List<EnemyRenderInfo>(parameter.Emitters.Count);
        for (var i = 0; i < parameter.Emitters.Count && i < DanmakuShapeParameter.MaxEmitters; i++)
        {
            var emitter = parameter.Emitters[i];
            var emitterEnabled = emitter.IsEnabled.GetValue(frame, totalFrame, fps) >= 0.5;
            if (!emitterEnabled) continue;

            // エミッター位置 (X, Y および公転) と魔法陣回転角 (シミュレータの積分値を正確に反映)
            var ctx = simulator?.Engine.Contexts is { Count: > 0 } ctxs && i < ctxs.Count ? ctxs[i] : null;
            var posX = ctx is not null ? (float)ctx.Position.X : (float)emitter.X.GetValue(frame, totalFrame, fps);
            var posY = ctx is not null ? (float)ctx.Position.Y : (float)emitter.Y.GetValue(frame, totalFrame, fps);

            var enemyScale = (float)emitter.EnemyScale.GetValue(frame, totalFrame, fps);
            var enemyRotation = (float)emitter.EnemyRotation.GetValue(frame, totalFrame, fps);
            var enemyOpacity = (float)emitter.EnemyOpacity.GetValue(frame, totalFrame, fps);

            var mcScale = (float)emitter.MagicCircleScale.GetValue(frame, totalFrame, fps);
            var mcAngle = ctx is not null ? (float)ctx.MagicCircleAngle : (float)emitter.MagicCircleRotationSpeed.GetValue(frame, totalFrame, fps) * (float)simTime;
            var mcOpacity = (float)emitter.MagicCircleOpacity.GetValue(frame, totalFrame, fps);
            var mcColor4 = ColorExtensions.ToColor4(emitter.MagicCircleColor);

            var auraIntensity = (float)emitter.AuraIntensity.GetValue(frame, totalFrame, fps);
            var auraColor4 = ColorExtensions.ToColor4(emitter.AuraColor);

            var enemyBehindBullets = emitter.EnemyBehindBullets.GetValue(frame, totalFrame, fps) >= 0.5;
            var magicCircleEnabled = emitter.MagicCircleEnabled.GetValue(frame, totalFrame, fps) >= 0.5;
            var magicCircleAdditive = emitter.MagicCircleAdditive.GetValue(frame, totalFrame, fps) >= 0.5;
            var auraEnabled = emitter.AuraEnabled.GetValue(frame, totalFrame, fps) >= 0.5;

            enemies.Add(new EnemyRenderInfo(
                X: posX,
                Y: posY,
                EnemyEnabled: emitter.HasEnemyImage,
                EnemySlot: SpriteSlots.EnemyCustomSlotOf(i),
                EnemyScale: enemyScale,
                EnemyRotation: enemyRotation,
                EnemyOpacity: enemyOpacity,
                EnemyBehindBullets: enemyBehindBullets,
                MagicCircleEnabled: magicCircleEnabled,
                MagicCircleSlot: SpriteSlots.MagicCircleCustomSlotOf(i),
                MagicCircleScale: mcScale,
                MagicCircleAngle: mcAngle,
                MagicCircleColor: mcColor4,
                MagicCircleOpacity: mcOpacity,
                MagicCircleAdditive: magicCircleAdditive,
                IsBuiltInMagicCircle: !emitter.HasCustomMagicCircleImage,
                AuraEnabled: auraEnabled,
                AuraIntensity: auraIntensity,
                AuraColor: auraColor4
            ));
        }

        var globalOpacity = Math.Clamp(parameter.GlobalOpacity.GetValue(frame, totalFrame, fps) / 100.0, 0.0, 1.0);
        batchBuilder.Build(simulator?.Bullets ?? [], GetAppearance, globalOpacity);

        var hpBarEnabled = parameter.HpBarEnabled.GetValue(frame, totalFrame, fps) >= 0.5;
        var hpBarGlow = parameter.HpBarGlow.GetValue(frame, totalFrame, fps) >= 0.5;
        var hpRatio = (float)(simulator?.Engine.BossHpRatio ?? 1.0);
        var lagRatio = (float)(simulator?.Engine.BossDamageLagRatio ?? 1.0);
        var hpBarRadius = (float)parameter.HpBarRadius.GetValue(frame, totalFrame, fps);
        var hpBarWidth = (float)parameter.HpBarWidth.GetValue(frame, totalFrame, fps);
        var hpBarHeight = (float)parameter.HpBarHeight.GetValue(frame, totalFrame, fps);
        var hpBarX = (float)parameter.HpBarX.GetValue(frame, totalFrame, fps);
        var hpBarY = (float)parameter.HpBarY.GetValue(frame, totalFrame, fps);
        var hpBarOpacity = Math.Clamp((float)parameter.HpBarOpacity.GetValue(frame, totalFrame, fps) / 100f, 0f, 1f);

        var bossX = 0f;
        var bossY = -200f;
        if (enemies is { Count: > 0 })
        {
            bossX = enemies[0].X;
            bossY = enemies[0].Y;
        }

        var hpBarInfo = new BossHpBarRenderInfo(
            Enabled: hpBarEnabled,
            Style: parameter.HpBarStyle,
            HpRatio: hpRatio,
            DamageLagRatio: lagRatio,
            BossX: bossX,
            BossY: bossY,
            Radius: hpBarRadius,
            Width: hpBarWidth,
            Height: hpBarHeight,
            X: hpBarX,
            Y: hpBarY,
            Thickness: (float)parameter.HpBarThickness,
            BarColor: ColorExtensions.ToColor4(parameter.HpBarColor),
            DangerColor: ColorExtensions.ToColor4(parameter.HpBarDangerColor),
            DamageLagColor: ColorExtensions.ToColor4(parameter.HpBarDamageLagColor),
            BackgroundColor: ColorExtensions.ToColor4(parameter.HpBarBackgroundColor),
            PhaseCount: parameter.HpBarPhaseCount,
            Glow: hpBarGlow,
            Opacity: hpBarOpacity
        );

        output = renderer.Render(batchBuilder, parameter.GetGlowIntensity, in targetInfo, enemies, in hpBarInfo);
    }

    /// <summary>
    /// キーフレームで動く値をエンジンへ供給する。
    /// <para>
    /// 渡す関数は「時刻のみに依存する純粋関数」でなければならない。
    /// <c>Animation.GetValue</c> はまさにその条件を満たすため、
    /// どのフレームへシークしても弾幕は同一に再現される。
    /// </para>
    /// </summary>
    private void WireLiveValues(DanmakuSimulator sim, int fps, int totalFrame)
    {
        DanmakuLiveWiring.WireLiveValues(parameter, sim, fps, totalFrame, this);
    }

    /// <summary>
    /// タイムライン上のフレーム番号から、TimeScale を考慮したシミュレーション上の経過秒数を求める。
    /// 正の値なら通常再生、負の値ならアイテム終端からの逆再生、キーフレームなら巻き戻しを正しく計算する。
    /// </summary>
    private double ComputeSimulatedTime(int currentFrame, int fps, int totalFrame)
    {
        if (fps <= 0) fps = 60;
        if (totalFrame <= 0) totalFrame = 1;

        var dt = 1.0 / fps;
        var integralAtCurrent = 0.0;
        var minIntegral = 0.0;
        var runningIntegral = 0.0;

        for (var f = 0; f < totalFrame; f++)
        {
            var scale = parameter.TimeScale.GetValue(f, totalFrame, fps);
            runningIntegral += scale * dt;
            if (runningIntegral < minIntegral)
            {
                minIntegral = runningIntegral;
            }
            if (f == currentFrame - 1)
            {
                integralAtCurrent = runningIntegral;
            }
        }

        var baseOffset = Math.Max(0.0, -minIntegral);
        return currentFrame <= 0 ? baseOffset : Math.Max(0.0, baseOffset + integralAtCurrent);
    }

    /// <summary>
    /// シミュレーション秒をタイムラインフレーム番号へ逆変換する。
    /// </summary>
    private int TimeToFrame(double timeSeconds, int fps, int totalFrame)
    {
        if (fps <= 0) fps = 60;
        if (totalFrame <= 0) totalFrame = 1;

        var scale0 = parameter.TimeScale.GetValue(0, totalFrame, fps);
        if (scale0 < 0)
        {
            var duration = (double)totalFrame / fps;
            var timelineTime = duration - timeSeconds / Math.Abs(scale0);
            return Math.Clamp((int)Math.Round(timelineTime * fps), 0, Math.Max(0, totalFrame - 1));
        }
        if (scale0 > 0)
        {
            var timelineTime = timeSeconds / scale0;
            return Math.Clamp((int)Math.Round(timelineTime * fps), 0, Math.Max(0, totalFrame - 1));
        }

        return Math.Clamp((int)Math.Round(timeSeconds * fps), 0, Math.Max(0, totalFrame - 1));
    }

    /// <summary>弾の描画用プロパティを引く (バッチ生成用)。</summary>
    private BulletAppearance GetAppearance(int emitterIndex)
    {
        if (simulator is null) return new BulletAppearance();

        var settings = simulator.Settings;
        if (emitterIndex < 0 || emitterIndex >= settings.Emitters.Length)
            return settings.Emitters.Length > 0 ? settings.Emitters[0].Appearance : new BulletAppearance();

        return settings.Emitters[emitterIndex].Appearance;
    }

    /// <summary>各エミッターの弾画像・エネミー画像・魔法陣画像および自機画像をスプライトスロットへ読み込む。</summary>
    private void LoadCustomImages()
    {
        var emitters = parameter.Emitters;
        for (var i = 0; i < emitters.Count && i < DanmakuShapeParameter.MaxEmitters; i++)
        {
            var emitter = emitters[i];

            // 弾画像
            renderer.Sprites.SetCustomImage(
                SpriteSlots.CustomSlotOf(i),
                emitter.HasCustomImage ? emitter.ImagePath : null);

            // エネミー画像
            renderer.Sprites.SetCustomImage(
                SpriteSlots.EnemyCustomSlotOf(i),
                emitter.HasEnemyImage ? emitter.EnemyImagePath : null);

            // カスタム魔法陣画像
            renderer.Sprites.SetCustomImage(
                SpriteSlots.MagicCircleCustomSlotOf(i),
                emitter.HasCustomMagicCircleImage ? emitter.MagicCircleImagePath : null);
        }

        // 自機 (ターゲット) の画像
        renderer.Sprites.SetCustomImage(
            SpriteSlots.TargetCustomSlot,
            parameter.HasCustomTargetImage ? parameter.TargetImagePath : null);

        // 自機ショットのカスタム画像
        renderer.Sprites.SetCustomImage(
            SpriteSlots.PlayerCustomShotSlot,
            parameter.HasCustomPlayerShotImage ? parameter.PlayerShotImagePath : null);
    }

    /// <summary>
    /// プレビューエリア上の制御点を組み立てる。
    /// <para>
    /// エミッターごとに 1 点、当たり判定が有効ならターゲットにも 1 点を出す。
    /// ドラッグ量はスクリーン座標の差分なので、そのままキーフレーム値へ加算すればよい。
    /// </para>
    /// </summary>
    private IEnumerable<VideoController> BuildControllers()
    {
        if (!parameter.ShowControllers) yield break;

        var emitters = parameter.Emitters;
        var fps = lastFps > 0 ? lastFps : 60;
        var frame = Math.Max(0, lastFrame);
        var totalFrame = Math.Max(1, lastTotalFrame > 0 ? lastTotalFrame : frame + 1);

        foreach (var emitter in emitters)
        {
            var captured = emitter;
            var position = new Vector3(
                (float)captured.X.GetValue(frame, totalFrame, fps),
                (float)captured.Y.GetValue(frame, totalFrame, fps),
                0);

            var point = new ControllerPoint(position, arg =>
            {
                ApplyControllerDrag(captured.X, arg.Delta.X, frame, totalFrame);
                ApplyControllerDrag(captured.Y, arg.Delta.Y, frame, totalFrame);
            })
            {
                Shape = VideoControllerPointShape.Circle,
            };

            yield return new VideoController([point])
            {
                Connection = VideoControllerPointConnection.None,
            };
        }

        if (parameter.CollisionEnabled.GetValue(frame, totalFrame, fps) < 0.5) yield break;

        var targetPosition = new Vector3(
            (float)parameter.TargetX.GetValue(frame, totalFrame, fps),
            (float)parameter.TargetY.GetValue(frame, totalFrame, fps),
            0);

        var targetPoint = new ControllerPoint(targetPosition, arg =>
        {
            ApplyControllerDrag(parameter.TargetX, arg.Delta.X, frame, totalFrame);
            ApplyControllerDrag(parameter.TargetY, arg.Delta.Y, frame, totalFrame);
        })
        {
            Shape = VideoControllerPointShape.Square,
        };

        yield return new VideoController([targetPoint])
        {
            Connection = VideoControllerPointConnection.None,
        };
    }

    /// <summary>
    /// プレビュー上の制御点ドラッグ時、現在フレームに応じた適切なキーフレーム値を更新する。
    /// </summary>
    private static void ApplyControllerDrag(Animation anim, double delta, int frame, int totalFrame)
    {
        if (anim.Values.Count == 0) return;

        if (anim.AnimationType == AnimationType.なし)
        {
            // 固定値の場合は全体を加算
            anim.AddToEachValues(delta);
        }
        else if (anim.Values.Count == 2)
        {
            // 直線移動・加減速など (From -> To)
            if (frame <= 0)
            {
                // 先頭フレームでは開始位置 (From) のみを移動
                anim.Values[0].Value += delta;
            }
            else if (frame >= totalFrame - 1)
            {
                // 末尾フレームでは終了位置 (To) のみを移動
                anim.Values[1].Value += delta;
            }
            else
            {
                // 中間フレームでは現在比率に応じて移動
                var ratio = (double)frame / Math.Max(1, totalFrame - 1);
                anim.Values[0].Value += delta * (1.0 - ratio);
                anim.Values[1].Value += delta * ratio;
            }
        }
        else
        {
            anim.AddToEachValues(delta);
        }
    }

    /// <summary>
    /// 弾が 1 つも無いときに返す空の描画結果。
    /// <para>
    /// <see cref="Output"/> は null を返せないため、空の CommandList を 1 つだけ用意しておく。
    /// </para>
    /// </summary>
    private ID2D1CommandList GetEmptyOutput()
    {
        if (emptyOutput is not null) return emptyOutput;

        var dc = devices.DeviceContext;
        emptyOutput = dc.CreateCommandList();
        disposer.Collect(emptyOutput);

        var previousTarget = dc.Target;
        dc.Target = emptyOutput;
        dc.BeginDraw();
        dc.Clear(null);
        dc.EndDraw();
        dc.Target = previousTarget;
        emptyOutput.Close();

        return emptyOutput;
    }

    public void Dispose()
    {
        Audio.DanmakuChannelBus.Unregister(parameter);
        Collision.DanmakuCollisionBus.UnregisterLayer(this);
        simulator = null;
        output = null;
        emptyOutput = null;
        disposer.DisposeAndClear();
    }
}
