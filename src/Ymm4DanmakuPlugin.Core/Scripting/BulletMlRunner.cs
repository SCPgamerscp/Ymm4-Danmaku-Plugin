using Ymm4DanmakuPlugin.Core.Mathematics;

namespace Ymm4DanmakuPlugin.Core.Scripting;

/// <summary>
/// BulletML アクションを実行する小さな仮想マシン。
/// <para>
/// BulletML は 60fps を前提とした「フレーム」単位の命令列であるため、
/// 実時間 (秒) を <see cref="FrameRate"/> でフレームへ換算して駆動する。
/// エミッター本体にも個々の弾にも同じランナーを取り付けられる。
/// </para>
/// </summary>
public sealed class BulletMlRunner
{
    /// <summary>1 ステップで実行する命令数の上限 (無限ループ対策)。</summary>
    private const int MaxCommandsPerFrame = 512;

    /// <summary>入れ子アクションの深さ上限。</summary>
    private const int MaxStackDepth = 64;

    private readonly BulletMlProgram program;
    private readonly List<Frame> stack = [];

    private double frameAccumulator;
    private double waitFrames;

    // 直前に発射した弾の方向・速度 (type="sequence" 用)
    private double lastFireDirection;
    private double lastFireSpeed;
    private bool hasFired;

    // 漸次変化 (changeDirection / changeSpeed / accel)
    private double directionFramesLeft;
    private double directionDeltaPerFrame;
    private double speedFramesLeft;
    private double speedDeltaPerFrame;
    private double accelFramesLeft;
    private double accelVx;
    private double accelVy;

    /// <summary>BulletML の 1 フレームに対応する実時間の逆数 (既定 60fps)。</summary>
    public double FrameRate { get; init; } = 60.0;

    /// <summary>アクションをすべて実行し終えたか。</summary>
    public bool IsFinished => stack.Count == 0 && directionFramesLeft <= 0 && speedFramesLeft <= 0 && accelFramesLeft <= 0;

    /// <summary>アクション終了後に先頭から繰り返すか (エミッターの top アクション用)。</summary>
    public bool Loop { get; init; }

    private readonly BulletMlAction? rootAction;
    private readonly double[] rootParameters;

    public BulletMlRunner(BulletMlProgram program, BulletMlAction action, double[]? parameters = null)
    {
        this.program = program;
        rootAction = action;
        rootParameters = parameters ?? [];
        stack.Add(new Frame(action, rootParameters));
    }

    private BulletMlRunner(BulletMlProgram program, IEnumerable<Frame> frames, double frameRate)
    {
        this.program = program;
        rootAction = null;
        rootParameters = [];
        FrameRate = frameRate;
        stack.AddRange(frames);
    }

    /// <summary>初期状態へ戻す。</summary>
    public void Reset()
    {
        stack.Clear();
        if (rootAction is not null) stack.Add(new Frame(rootAction, rootParameters));
        frameAccumulator = 0;
        waitFrames = 0;
        lastFireDirection = 0;
        lastFireSpeed = 0;
        hasFired = false;
        directionFramesLeft = 0;
        speedFramesLeft = 0;
        accelFramesLeft = 0;
    }

    /// <summary>実時間 deltaTime 秒ぶんだけ実行する。</summary>
    public void Update(IBulletMlHost host, double deltaTime)
    {
        frameAccumulator += deltaTime * FrameRate;

        var guard = 0;
        while (frameAccumulator >= 1.0 && guard++ < 600)
        {
            frameAccumulator -= 1.0;
            StepFrame(host);
        }
    }

    /// <summary>BulletML の 1 フレームぶん実行する。</summary>
    public void StepFrame(IBulletMlHost host)
    {
        ApplyGradualChanges(host);

        if (waitFrames > 0)
        {
            waitFrames -= 1.0;
            return;
        }

        var executed = 0;
        while (stack.Count > 0 && executed++ < MaxCommandsPerFrame)
        {
            var frame = stack[^1];

            if (frame.Pc >= frame.Action.Commands.Count)
            {
                frame.RepeatIndex++;
                if (frame.RepeatIndex < frame.RepeatTimes)
                {
                    frame.Pc = 0;
                    continue;
                }

                stack.RemoveAt(stack.Count - 1);

                if (stack.Count == 0 && Loop && rootAction is not null)
                    stack.Add(new Frame(rootAction, rootParameters));

                continue;
            }

            var command = frame.Action.Commands[frame.Pc++];
            if (Execute(host, frame, command))
                return; // wait が発生したのでこのフレームは終了
        }
    }

    /// <summary>命令を 1 つ実行する。true を返した場合はこのフレームの処理を打ち切る。</summary>
    private bool Execute(IBulletMlHost host, Frame frame, IBulletMlCommand command)
    {
        var variables = CreateVariables(host, frame);

        switch (command)
        {
            case BulletMlWait wait:
            {
                var frames = wait.Frames.Evaluate(in variables);
                if (frames > 0)
                {
                    waitFrames = frames - 1;
                    return true;
                }

                return false;
            }

            case BulletMlVanish:
                host.Vanish();
                stack.Clear();
                return true;

            case BulletMlFireRef fireRef:
                ExecuteFire(host, frame, fireRef);
                return false;

            case BulletMlActionRef actionRef:
            {
                var action = program.ResolveAction(actionRef);
                if (action is null || stack.Count >= MaxStackDepth) return false;
                var parameters = EvaluateParameters(actionRef.Parameters, in variables);
                stack.Add(new Frame(action, parameters));
                return false;
            }

            case BulletMlRepeat repeat:
            {
                var action = program.ResolveAction(repeat.Action);
                if (action is null || stack.Count >= MaxStackDepth) return false;
                var times = (int)Math.Round(repeat.Times.Evaluate(in variables));
                if (times <= 0) return false;
                var parameters = EvaluateParameters(repeat.Action.Parameters, in variables);
                stack.Add(new Frame(action, parameters) { RepeatTimes = times });
                return false;
            }

            case BulletMlChangeDirection change:
            {
                var term = Math.Max(1.0, change.Term.Evaluate(in variables));
                var value = change.Direction.Evaluate(in variables);
                if (change.Type == BulletMlDirectionType.Sequence)
                {
                    directionDeltaPerFrame = value;
                }
                else
                {
                    var target = ResolveDirection(host, value, change.Type);
                    directionDeltaPerFrame = DanmakuMath.DeltaAngle(host.SelfDirection, target) / term;
                }

                directionFramesLeft = term;
                host.NotifyChange();
                return false;
            }

            case BulletMlChangeSpeed change:
            {
                var term = Math.Max(1.0, change.Term.Evaluate(in variables));
                var value = change.Speed.Evaluate(in variables);
                speedDeltaPerFrame = change.Type switch
                {
                    BulletMlSpeedType.Sequence => value,
                    BulletMlSpeedType.Relative => value / term,
                    _ => (value - host.SelfSpeed) / term,
                };
                speedFramesLeft = term;
                host.NotifyChange();
                return false;
            }

            case BulletMlAccel accel:
            {
                var term = Math.Max(1.0, accel.Term.Evaluate(in variables));
                var velocity = Vec2.FromDegrees(host.SelfDirection, host.SelfSpeed);

                accelVx = ResolveAccelComponent(accel.Horizontal, accel.HorizontalType, velocity.X, term, in variables);
                accelVy = ResolveAccelComponent(accel.Vertical, accel.VerticalType, velocity.Y, term, in variables);
                accelFramesLeft = term;
                host.NotifyChange();
                return false;
            }

            default:
                return false;
        }
    }

    private static double ResolveAccelComponent(
        BulletMlExpression? expression,
        BulletMlSpeedType type,
        double current,
        double term,
        in BulletMlVariables variables)
    {
        if (expression is null) return 0;
        var value = expression.Evaluate(in variables);
        return type switch
        {
            BulletMlSpeedType.Sequence => value,
            BulletMlSpeedType.Relative => value / term,
            _ => (value - current) / term,
        };
    }

    private void ExecuteFire(IBulletMlHost host, Frame frame, BulletMlFireRef fireRef)
    {
        var fire = program.ResolveFire(fireRef);
        if (fire is null) return;

        // fireRef のパラメータは fire 内の式から参照される
        var fireParameters = fireRef.Parameters.Count > 0
            ? EvaluateParameters(fireRef.Parameters, CreateVariables(host, frame))
            : frame.Parameters;

        var fireVariables = new BulletMlVariables(fireParameters, host.Rank, frame.RepeatIndex, host.Random);

        var bulletDefinition = program.ResolveBullet(fire.Bullet);

        var bulletParameters = fire.Bullet.Parameters.Count > 0
            ? EvaluateParameters(fire.Bullet.Parameters, in fireVariables)
            : fireParameters;

        var bulletVariables = new BulletMlVariables(bulletParameters, host.Rank, frame.RepeatIndex, host.Random);

        // 方向: fire の指定が優先、なければ bullet の指定、どちらも無ければ自機狙い
        double direction;
        if (fire.Direction is not null)
            direction = ResolveDirection(host, fire.Direction.Evaluate(in fireVariables), fire.DirectionType);
        else if (bulletDefinition?.Direction is not null)
            direction = ResolveDirection(host, bulletDefinition.Direction.Evaluate(in bulletVariables), bulletDefinition.DirectionType);
        else
            direction = AngleToTarget(host);

        // 速度
        double speed;
        if (fire.Speed is not null)
            speed = ResolveSpeed(host, fire.Speed.Evaluate(in fireVariables), fire.SpeedType);
        else if (bulletDefinition?.Speed is not null)
            speed = ResolveSpeed(host, bulletDefinition.Speed.Evaluate(in bulletVariables), bulletDefinition.SpeedType);
        else
            speed = 1.0;

        lastFireDirection = direction;
        lastFireSpeed = speed;
        hasFired = true;

        BulletMlRunner? childRunner = null;
        if (bulletDefinition is { Actions.Count: > 0 })
        {
            var frames = new List<Frame>();
            // 複数アクションは逆順に積んで先頭から実行されるようにする
            for (var i = bulletDefinition.Actions.Count - 1; i >= 0; i--)
            {
                var reference = bulletDefinition.Actions[i];
                var action = program.ResolveAction(reference);
                if (action is null) continue;
                var parameters = reference.Parameters.Count > 0
                    ? EvaluateParameters(reference.Parameters, in bulletVariables)
                    : bulletParameters;
                frames.Add(new Frame(action, parameters));
            }

            if (frames.Count > 0)
                childRunner = new BulletMlRunner(program, frames, FrameRate);
        }

        host.Fire(direction, speed, bulletDefinition, childRunner);
    }

    private void ApplyGradualChanges(IBulletMlHost host)
    {
        if (directionFramesLeft > 0)
        {
            host.SelfDirection = DanmakuMath.NormalizeAngle(host.SelfDirection + directionDeltaPerFrame);
            directionFramesLeft -= 1.0;
        }

        if (speedFramesLeft > 0)
        {
            host.SelfSpeed += speedDeltaPerFrame;
            speedFramesLeft -= 1.0;
        }

        if (accelFramesLeft > 0)
        {
            host.ApplyVelocityDelta(accelVx, accelVy);
            accelFramesLeft -= 1.0;
        }
    }

    private BulletMlVariables CreateVariables(IBulletMlHost host, Frame frame) =>
        new(frame.Parameters, host.Rank, frame.RepeatIndex, host.Random);

    private static double[] EvaluateParameters(IReadOnlyList<BulletMlExpression> expressions, in BulletMlVariables variables)
    {
        if (expressions.Count == 0) return [];
        var values = new double[expressions.Count];
        for (var i = 0; i < expressions.Count; i++)
            values[i] = expressions[i].Evaluate(in variables);
        return values;
    }

    private double ResolveDirection(IBulletMlHost host, double value, BulletMlDirectionType type) => type switch
    {
        // BulletML の絶対角は「上方向が 0、時計回り」。エンジン角は「右方向が 0、時計回り」。
        BulletMlDirectionType.Absolute => DanmakuMath.NormalizeAngle(value - 90),
        BulletMlDirectionType.Relative => DanmakuMath.NormalizeAngle(host.SelfDirection + value),
        BulletMlDirectionType.Sequence => DanmakuMath.NormalizeAngle((hasFired ? lastFireDirection : host.SelfDirection) + value),
        _ => DanmakuMath.NormalizeAngle(AngleToTarget(host) + value),
    };

    private double ResolveSpeed(IBulletMlHost host, double value, BulletMlSpeedType type) => type switch
    {
        BulletMlSpeedType.Relative => host.SelfSpeed + value,
        BulletMlSpeedType.Sequence => (hasFired ? lastFireSpeed : host.SelfSpeed) + value,
        _ => value,
    };

    private static double AngleToTarget(IBulletMlHost host) => (host.TargetPosition - host.SelfPosition).Degrees;

    private sealed class Frame(BulletMlAction action, double[] parameters)
    {
        public BulletMlAction Action { get; } = action;
        public double[] Parameters { get; } = parameters;
        public int Pc;
        public int RepeatTimes { get; init; } = 1;
        public int RepeatIndex;
    }
}
