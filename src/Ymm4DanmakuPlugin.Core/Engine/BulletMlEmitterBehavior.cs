using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Scripting;

namespace Ymm4DanmakuPlugin.Core.Engine;

/// <summary>
/// 第 3 階層「外部データ読み込み」のうち BulletML を実行するエミッター。
/// top アクションを <see cref="BulletMlRunner"/> で駆動する。
/// </summary>
public sealed class BulletMlEmitterBehavior : IEmitterBehavior
{
    private readonly EmitterSettings settings;
    private readonly BulletMlProgram program;
    private readonly List<BulletMlRunner> runners = [];
    private EmitterBulletMlHost? host;

    public BulletMlEmitterBehavior(EmitterSettings settings, BulletMlProgram program)
    {
        this.settings = settings;
        this.program = program;
        CreateRunners();
    }

    private void CreateRunners()
    {
        runners.Clear();
        foreach (var action in program.TopActions)
            runners.Add(new BulletMlRunner(program, action) { Loop = settings.ScriptLoop });
    }

    public void Reset()
    {
        foreach (var runner in runners) runner.Reset();
    }

    public void Update(EmitterContext context, double deltaTime)
    {
        var start = settings.Pattern.StartTime;
        var end = settings.Pattern.EndTime > 0 ? settings.Pattern.EndTime : double.PositiveInfinity;
        if (context.Time < start || context.Time > end) return;

        host ??= new EmitterBulletMlHost(context.Engine, context.EmitterIndex);
        host.Context = context;

        foreach (var runner in runners)
        {
            if (runner.IsFinished && !settings.ScriptLoop) continue;
            runner.Update(host, deltaTime);
        }
    }
}
