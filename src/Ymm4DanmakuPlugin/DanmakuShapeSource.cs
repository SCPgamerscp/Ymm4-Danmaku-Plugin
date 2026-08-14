using System.Numerics;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Engine;
using Ymm4DanmakuPlugin.Core.Mathematics;
using Ymm4DanmakuPlugin.Core.Rendering;
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

        var settings = parameter.ToSettings(canvasWidth, canvasHeight);

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

        // 同じフレーム・同じキャンバスなら描き直さない
        var needsRedraw =
            output is null ||
            frame != lastFrame ||
            fps != lastFps ||
            canvasWidth != lastCanvasWidth ||
            canvasHeight != lastCanvasHeight;

        simulator.SeekToFrame(frame, fps);
        LastBulletCount = simulator.Bullets.Count;

        if (!needsRedraw) return;

        lastFrame = frame;
        lastFps = fps;
        lastCanvasWidth = canvasWidth;
        lastCanvasHeight = canvasHeight;

        batchBuilder.Build(simulator.Bullets, GetAppearance, parameter.GlobalOpacity);
        output = renderer.Render(batchBuilder, parameter.GetGlowIntensity);
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
        var emitters = parameter.Emitters;

        sim.Live.EmitterPosition = (index, timeSeconds) =>
        {
            if (index < 0 || index >= emitters.Count) return null;

            var emitter = emitters[index];
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return new Vec2(
                emitter.X.GetValue(frame, totalFrame, fps),
                emitter.Y.GetValue(frame, totalFrame, fps));
        };

        sim.Live.TargetPosition = timeSeconds =>
        {
            var frame = TimeToFrame(timeSeconds, fps, totalFrame);
            return new Vec2(
                parameter.TargetX.GetValue(frame, totalFrame, fps),
                parameter.TargetY.GetValue(frame, totalFrame, fps));
        };
    }

    /// <summary>
    /// 秒をフレーム番号へ変換する。
    /// <para>
    /// シミュレーションは 1/120 秒などフレームより細かい刻みで進むため、
    /// キーフレーム値もフレーム境界で階段状になる。これは YMM4 本体の挙動と一致しており、
    /// 「同じフレームなら同じ値」という決定論の条件を満たす。
    /// </para>
    /// </summary>
    private static long TimeToFrame(double timeSeconds, int fps, int totalFrame)
    {
        var frame = (long)(timeSeconds * fps);
        if (frame < 0) frame = 0;
        if (frame > totalFrame) frame = totalFrame;
        return frame;
    }

    /// <summary>エミッター番号から見た目設定を引く (トレイル描画のため描画側で必要)。</summary>
    private BulletAppearance GetAppearance(int emitterIndex)
    {
        var settings = simulator?.Settings;
        if (settings is null) return new BulletAppearance();

        if (emitterIndex < 0 || emitterIndex >= settings.Emitters.Length)
            return settings.Emitters.Length > 0 ? settings.Emitters[0].Appearance : new BulletAppearance();

        return settings.Emitters[emitterIndex].Appearance;
    }

    /// <summary>各エミッターのユーザー指定画像をスプライトスロットへ読み込む。</summary>
    private void LoadCustomImages()
    {
        var emitters = parameter.Emitters;
        for (var i = 0; i < emitters.Count && i < DanmakuShapeParameter.MaxEmitters; i++)
        {
            var emitter = emitters[i];
            renderer.Sprites.SetCustomImage(
                SpriteSlots.CustomSlotOf(i),
                emitter.HasCustomImage ? emitter.ImagePath : null);
        }
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
        var emitters = parameter.Emitters;
        var fps = lastFps > 0 ? lastFps : 60;
        var frame = Math.Max(0, lastFrame);

        foreach (var emitter in emitters)
        {
            var captured = emitter;
            var position = new Vector3(
                (float)captured.X.GetValue(frame, frame + 1, fps),
                (float)captured.Y.GetValue(frame, frame + 1, fps),
                0);

            var point = new ControllerPoint(position, arg =>
            {
                captured.X.AddToEachValues(arg.Delta.X);
                captured.Y.AddToEachValues(arg.Delta.Y);
            })
            {
                Shape = ControllerPointShape.Circle,
            };

            yield return new VideoController([point])
            {
                Connection = VideoControllerPointConnection.None,
            };
        }

        if (!parameter.CollisionEnabled) yield break;

        var targetPosition = new Vector3(
            (float)parameter.TargetX.GetValue(frame, frame + 1, fps),
            (float)parameter.TargetY.GetValue(frame, frame + 1, fps),
            0);

        var targetPoint = new ControllerPoint(targetPosition, arg =>
        {
            parameter.TargetX.AddToEachValues(arg.Delta.X);
            parameter.TargetY.AddToEachValues(arg.Delta.Y);
        })
        {
            Shape = ControllerPointShape.Square,
        };

        yield return new VideoController([targetPoint])
        {
            Connection = VideoControllerPointConnection.None,
        };
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
        simulator = null;
        output = null;
        emptyOutput = null;
        disposer.DisposeAndClear();
    }
}
