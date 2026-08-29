using Ymm4DanmakuPlugin.Core.Audio;
using Ymm4DanmakuPlugin.Core.Configuration;
using Ymm4DanmakuPlugin.Core.Mathematics;
using Ymm4DanmakuPlugin.Core.Model;

namespace Ymm4DanmakuPlugin.Core.Engine;

/// <summary>
/// 弾幕の生成・挙動計算を一括管理するコアエンジン。
/// <para>
/// 決定論的に動作するため、同じ設定・同じシードであれば
/// 何度シークしても同一フレームでは必ず同一の結果になる。
/// </para>
/// </summary>
public sealed class DanmakuEngine
{
    private readonly List<IEmitterBehavior> behaviors = [];
    private readonly List<EmitterContext> contexts = [];
    private readonly List<Bullet> pendingSplits = [];
    private readonly BulletBulletMlHost scriptHost;

    public DanmakuSettings Settings { get; internal set; }

    /// <summary>全エミッターの実行コンテキスト一覧。</summary>
    public IReadOnlyList<EmitterContext> Contexts => contexts;

    public BulletPool Pool { get; private set; }

    public DeterministicRandom Random { get; private set; }

    public SoundEventLog SoundLog { get; } = new();

    /// <summary>完了した固定ステップ数 (時刻の整数部)。</summary>
    private long gridSteps;

    /// <summary>固定ステップ格子からのはみ出し秒数 (0 以上 <see cref="StepSize"/> 未満)。</summary>
    private double partialTime;

    /// <summary>
    /// シミュレーション開始からの経過秒数。
    /// <para>
    /// <b>加算ではなく「ステップ数 × ステップ幅」で算出する。</b>
    /// 単純に <c>+=</c> で積み上げると浮動小数点誤差の累積量がシークの経路
    /// (一気に進めたか、途中で巻き戻したか) によって変わってしまい、
    /// 時刻を参照する処理 (キーフレーム値・Whip パターンなど) の結果が
    /// 同一フレームでも一致しなくなる。
    /// </para>
    /// </summary>
    public double CurrentTime => gridSteps * StepSize + partialTime;

    /// <summary>固定ステップの幅 (秒)。</summary>
    public double StepSize => Math.Max(1.0 / 600.0, Settings.FixedTimeStep);

    /// <summary>実行したステップ数。</summary>
    public long StepCount { get; private set; }

    /// <summary>ターゲット (自機) の位置。ホーミングと衝突判定で使用する。</summary>
    public Vec2 TargetPosition { get; set; }

    /// <summary>
    /// キーフレームで時間変化する値の供給元。
    /// 設定されている場合、エミッター位置とターゲット位置は毎ステップこちらから取得される。
    /// </summary>
    public LiveValueSource Live { get; } = new();

    /// <summary>これまでに発生した衝突回数。</summary>
    public int HitCount { get; private set; }

    /// <summary>これまでに生成された弾の総数。</summary>
    public long TotalSpawned { get; private set; }

    public DanmakuEngine(DanmakuSettings settings, IEnumerable<IEmitterBehavior>? behaviors = null)
    {
        Settings = settings;
        Pool = new BulletPool(settings.MaxBullets);
        Random = new DeterministicRandom(settings.Seed);
        scriptHost = new BulletBulletMlHost(this);
        TargetPosition = new Vec2(settings.Collision.TargetX, settings.Collision.TargetY);

        SetBehaviors(behaviors ?? CreateDefaultBehaviors(settings));
    }

    /// <summary>設定から既定 (パターン生成) の挙動を作る。</summary>
    private static IEnumerable<IEmitterBehavior> CreateDefaultBehaviors(DanmakuSettings settings) =>
        settings.Emitters.Select(IEmitterBehavior (e) => new PatternEmitterBehavior(e));

    /// <summary>エミッターの挙動を差し替える。</summary>
    public void SetBehaviors(IEnumerable<IEmitterBehavior> newBehaviors)
    {
        behaviors.Clear();
        contexts.Clear();
        behaviors.AddRange(newBehaviors);

        for (var i = 0; i < behaviors.Count; i++)
            contexts.Add(new EmitterContext(this, Math.Min(i, Settings.Emitters.Length - 1)));

        Reset();
    }

    /// <summary>設定を差し替えて初期化する。</summary>
    public void Reconfigure(DanmakuSettings settings, IEnumerable<IEmitterBehavior> newBehaviors)
    {
        Settings = settings;
        if (Pool.Capacity != settings.MaxBullets)
            Pool = new BulletPool(settings.MaxBullets);
        Random = new DeterministicRandom(settings.Seed);
        TargetPosition = new Vec2(settings.Collision.TargetX, settings.Collision.TargetY);
        SetBehaviors(newBehaviors);
    }

    /// <summary>シミュレーションを初期状態に戻す。</summary>
    public void Reset()
    {
        Pool.Clear();
        SoundLog.Clear();
        Random.Reset();
        gridSteps = 0;
        partialTime = 0;
        StepCount = 0;
        HitCount = 0;
        TotalSpawned = 0;
        EnemyHitCount = 0;
        playerShotTimer = 0;
        TotalDamageDealt = 0;

        BossMaxHp = Settings.HpBar.MaxHp > 0 ? Settings.HpBar.MaxHp : 1000.0;
        var initPct = Live.BossHp?.Invoke(0) ?? Settings.HpBar.InitialHpPercentage;
        CurrentBossHp = Math.Clamp(initPct / 100.0, 0.0, 1.0) * BossMaxHp;
        DamageLagBossHp = CurrentBossHp;

        foreach (var ctx in contexts)
        {
            ctx.OrbitAngle = 0;
            ctx.MagicCircleAngle = 0;
            ctx.RainbowBaseHue = 0;
            var s = Settings.Emitters[ctx.EmitterIndex];
            var p = Live.EmitterPosition?.Invoke(ctx.EmitterIndex, 0) ?? new Vec2(s.X, s.Y);
            var orbitRadius = Live.EmitterOrbitRadius?.Invoke(ctx.EmitterIndex, 0) ?? s.OrbitRadius;
            var orbitPhase = Live.EmitterOrbitPhase?.Invoke(ctx.EmitterIndex, 0) ?? s.OrbitPhase;
            if (orbitRadius != 0)
            {
                p += Vec2.FromDegrees(orbitPhase, orbitRadius);
            }
            ctx.Position = p;
        }

        RefreshPositions();

        foreach (var behavior in behaviors) behavior.Reset();
    }

    /// <summary>
    /// 指定時間ぶんシミュレーションを進める。
    /// 内部では <see cref="DanmakuSettings.FixedTimeStep"/> の固定ステップに分割される。
    /// <para>
    /// <b>決定論の要:</b> 分割は「絶対時刻の固定ステップ格子」に対して行う。
    /// 端数 (格子に乗らない余り) は次回の呼び出しへ繰り越し、
    /// 中途半端な幅のステップを実行しない。これにより
    /// 「1 回で 1.0 秒進める」場合と「1/120 秒ずつ 120 回進める」場合とで
    /// 完全に同一のステップ列が実行される。
    /// </para>
    /// </summary>
    public void Advance(double deltaSeconds)
    {
        if (deltaSeconds <= 0) return;

        var step = StepSize;
        var target = CurrentTime + deltaSeconds;

        // 目標時刻までに完了しているべき格子ステップ数
        var targetSteps = (long)Math.Floor(target / step + 1e-9);
        var remainingSteps = targetSteps - gridSteps;

        for (var i = 0L; i < remainingSteps; i++)
            StepGrid(step);

        // 格子に乗らない端数は時刻としてのみ保持し、次回の Advance で消化する。
        //
        // ここで端数を丸めずに保持すると、格子ぴったりの時刻であっても
        // 浮動小数点演算の丸め (target と gridSteps*step の差) により
        // 1ulp 程度 (実測 5.5e-17 秒) の残差が入り込む。
        // CurrentTime が 1ulp ずれるだけで発射タイミングの比較結果が変わり、
        // 「一括で進めた場合」と「刻んで進めた場合」で弾幕が食い違う。
        // そのためステップ幅に対して無視できる大きさの残差は 0 に丸める。
        var rest = target - gridSteps * step;
        var epsilon = step * 1e-9;
        partialTime = rest > epsilon && rest < step ? rest : 0;
    }

    /// <summary>
    /// 固定ステップを 1 回進める。
    /// <para>
    /// 端数時刻を持っている場合は、まず格子上へ戻してからステップする。
    /// </para>
    /// </summary>
    public void Step(double deltaTime)
    {
        partialTime = 0;
        StepGrid(deltaTime);
    }

    /// <summary>エネミー (ボス) の現在位置。</summary>
    public Vec2 EnemyPosition { get; set; }

    /// <summary>エネミー (ボス) の被弾判定半径。</summary>
    public double EnemyRadius { get; set; } = 32.0;

    /// <summary>エネミーへの自機ショット命中回数。</summary>
    public int EnemyHitCount { get; private set; }

    /// <summary>ボスの最大 HP。</summary>
    public double BossMaxHp { get; set; } = 1000.0;

    /// <summary>ボスの現在 HP。</summary>
    public double CurrentBossHp { get; set; } = 1000.0;

    /// <summary>被弾追従ラグバーの HP (滑らかに減衰)。</summary>
    public double DamageLagBossHp { get; set; } = 1000.0;

    /// <summary>これまでに受けた累積ダメージ量。</summary>
    public double TotalDamageDealt { get; set; }

    /// <summary>ボスの現在 HP 割合 (0.0〜1.0)。</summary>
    public double BossHpRatio => BossMaxHp > 0 ? Math.Clamp(CurrentBossHp / BossMaxHp, 0.0, 1.0) : 1.0;

    /// <summary>被弾追従ラグバーの HP 割合 (0.0〜1.0)。</summary>
    public double BossDamageLagRatio => BossMaxHp > 0 ? Math.Clamp(DamageLagBossHp / BossMaxHp, 0.0, 1.0) : 1.0;

    private double playerShotTimer;

    /// <summary>格子 1 ステップぶんの更新処理。</summary>
    private void StepGrid(double deltaTime)
    {
        RefreshPositions();

        // タイムラインによるキーフレーム HP 制御 ＆ 累積被弾ダメージの合算
        var baseHp = BossMaxHp;
        if (Live.BossHp?.Invoke(CurrentTime) is { } liveHp)
        {
            baseHp = Math.Clamp(liveHp / 100.0, 0.0, 1.0) * BossMaxHp;
        }
        CurrentBossHp = Math.Max(0.0, baseHp - TotalDamageDealt);

        // 被弾追従ラグバーの滑らかなアニメーション補間
        if (DamageLagBossHp > CurrentBossHp)
        {
            var diff = DamageLagBossHp - CurrentBossHp;
            DamageLagBossHp = Math.Max(CurrentBossHp, DamageLagBossHp - diff * Math.Min(1.0, 5.0 * deltaTime) - (BossMaxHp * 0.15 * deltaTime));
        }
        else if (DamageLagBossHp < CurrentBossHp)
        {
            DamageLagBossHp = CurrentBossHp;
        }

        UpdateEmitters(deltaTime);
        if (Settings.PlayerShot.IsEnabled) UpdatePlayerShots(deltaTime);
        UpdateBullets(deltaTime);
        ProcessSplits();
        if (Settings.Collision.IsEnabled || Settings.PlayerShot.IsEnabled) ProcessCollisions();
        Pool.Compact();

        gridSteps++;
        StepCount++;
    }

    /// <summary>
    /// ターゲット位置とエネミー位置を現在時刻の値に更新する。
    /// キーフレーム供給関数があればそれを優先し、なければ設定値を使う。
    /// </summary>
    private void RefreshPositions()
    {
        var liveTarget = Live.TargetPosition?.Invoke(CurrentTime);
        TargetPosition = liveTarget ?? new Vec2(Settings.Collision.TargetX, Settings.Collision.TargetY);

        var liveEnemy = Live.EnemyPosition?.Invoke(CurrentTime);
        if (liveEnemy.HasValue)
        {
            EnemyPosition = liveEnemy.Value;
        }
        else if (contexts.Count > 0)
        {
            EnemyPosition = contexts[0].Position;
        }
        else
        {
            EnemyPosition = new Vec2(Settings.Collision.EnemyX, Settings.Collision.EnemyY);
        }

        var liveEnemyRadius = Live.EnemyRadius?.Invoke(CurrentTime);
        EnemyRadius = liveEnemyRadius ?? Settings.Collision.EnemyRadius;
    }

    private void UpdateEmitters(double deltaTime)
    {
        for (var i = 0; i < behaviors.Count; i++)
        {
            var context = contexts[i];
            var settings = Settings.Emitters[context.EmitterIndex];
            if (!settings.IsEnabled) continue;

            // キーフレームで動かす場合は供給関数の値を基準位置とする
            var position = Live.EmitterPosition?.Invoke(context.EmitterIndex, CurrentTime)
                           ?? new Vec2(settings.X, settings.Y);

            var orbitRadius = Live.EmitterOrbitRadius?.Invoke(context.EmitterIndex, CurrentTime) ?? settings.OrbitRadius;
            var orbitSpeed = Live.EmitterOrbitSpeed?.Invoke(context.EmitterIndex, CurrentTime) ?? settings.OrbitSpeed;
            var orbitPhase = Live.EmitterOrbitPhase?.Invoke(context.EmitterIndex, CurrentTime) ?? settings.OrbitPhase;

            context.OrbitAngle += orbitSpeed * deltaTime;
            if (orbitRadius != 0)
            {
                var angle = orbitPhase + context.OrbitAngle;
                position += Vec2.FromDegrees(angle, orbitRadius);
            }

            var mcRotSpeed = Live.EmitterMagicCircleRotationSpeed?.Invoke(context.EmitterIndex, CurrentTime) ?? 0.0;
            context.MagicCircleAngle += mcRotSpeed * deltaTime;

            var hueVelocity = Live.EmitterHueVelocity?.Invoke(context.EmitterIndex, CurrentTime) ?? settings.Appearance.HueVelocity;
            context.RainbowBaseHue += hueVelocity * deltaTime;

            context.Position = position;
            behaviors[i].Update(context, deltaTime);
        }
    }

    private void UpdatePlayerShots(double deltaTime)
    {
        var shot = Settings.PlayerShot;
        if (!shot.IsEnabled) return;

        var interval = Live.PlayerShotInterval?.Invoke(CurrentTime) ?? shot.FireInterval;
        if (interval <= 0) return;

        playerShotTimer += deltaTime;

        while (playerShotTimer >= interval)
        {
            playerShotTimer -= interval;
            FirePlayerShotBurst(shot);
        }
    }

    private void FirePlayerShotBurst(PlayerShotSettings shot)
    {
        var way = Live.PlayerShotWay?.Invoke(CurrentTime) ?? shot.Way;
        if (way <= 0) return;

        var speed = Live.PlayerShotSpeed?.Invoke(CurrentTime) ?? shot.Speed;
        var spread = Live.PlayerShotSpread?.Invoke(CurrentTime) ?? shot.SpreadAngle;
        var scale = Live.PlayerShotScale?.Invoke(CurrentTime) ?? shot.Scale;
        var hitRadius = Live.PlayerShotHitRadius?.Invoke(CurrentTime) ?? shot.HitRadius;

        var baseAngle = -90.0; // 上向き (STG標準)
        if (shot.AutoAim && (EnemyPosition - TargetPosition).LengthSquared > 1.0)
        {
            baseAngle = (EnemyPosition - TargetPosition).Degrees;
        }

        var isHoming = shot.ShotType == PlayerShotType.HomingAmulet;
        var step = way > 1 ? spread / (way - 1) : 0;
        var spriteIndex = GetPlayerShotSpriteIndex(shot.ShotType);
        var soundEmitted = false;

        for (var i = 0; i < way; i++)
        {
            var bullet = Pool.Rent();
            if (bullet is null) return;

            TotalSpawned++;

            var angle = way > 1 ? baseAngle - spread / 2.0 + step * i : baseAngle;
            var offset = Vec2.Zero;

            // 拡散0度 (前方集中) では横に並べる
            if (way > 1 && Math.Abs(spread) <= 0.001)
            {
                var spacing = 16.0 * Math.Abs(scale);
                var xOffset = (i - (way - 1) / 2.0) * spacing;
                offset = new Vec2(xOffset, 0);
            }

            bullet.EmitterIndex = 0;
            bullet.Generation = 0;
            bullet.IsPlayerShot = true;
            bullet.CancelEnemyBullets = shot.CancelEnemyBullets;
            bullet.Position = TargetPosition + offset;
            bullet.PreviousPosition = bullet.Position;
            bullet.Direction = angle;
            bullet.Speed = speed;
            bullet.HitRadius = hitRadius;
            bullet.DestroyOnHit = shot.DestroyOnHit;
            bullet.Scale = scale;
            bullet.Color = shot.Color;
            bullet.Additive = shot.Additive;
            bullet.AlignToDirection = shot.AlignToDirection;
            bullet.SpriteIndex = spriteIndex;
            bullet.Lifetime = 3.0;

            if (isHoming)
            {
                bullet.HomingEnabled = true;
                bullet.HomingTurnRate = 540.0;
                bullet.HomingRemaining = 2.5;
                bullet.HomingDelay = 0.04;
                bullet.HomingTarget = EnemyPosition;
            }

            if (!soundEmitted)
            {
                EmitSound(DanmakuSoundKind.PlayerShot, 0);
                soundEmitted = true;
            }
        }
    }

    private static int GetPlayerShotSpriteIndex(PlayerShotType type) => type switch
    {
        PlayerShotType.FocusStraight => 14,
        PlayerShotType.WideSpread => 14,
        PlayerShotType.HomingAmulet => 14,
        PlayerShotType.FastNeedle => 15,
        PlayerShotType.CustomImage => 16,
        _ => 14,
    };

    private void UpdateBullets(double deltaTime)
    {
        var bullets = Pool.ActiveBullets;
        var halfWidth = Settings.CanvasWidth / 2.0 + Settings.BoundsMargin;
        var halfHeight = Settings.CanvasHeight / 2.0 + Settings.BoundsMargin;

        for (var i = 0; i < bullets.Count; i++)
        {
            var bullet = bullets[i];
            if (!bullet.IsAlive) continue;

            // --- BulletML スクリプト ---
            if (bullet.Script is not null)
            {
                scriptHost.Bullet = bullet;
                scriptHost.SpeedScale = Settings.Emitters[bullet.EmitterIndex].ScriptSpeedScale;
                scriptHost.Rank = Settings.Emitters[bullet.EmitterIndex].ScriptRank;
                bullet.Script.Update(scriptHost, deltaTime);
                if (!bullet.IsAlive) continue;
            }

            // --- ホーミング ---
            if (bullet.HomingEnabled)
            {
                if (bullet.HomingDelay > 0)
                {
                    bullet.HomingDelay -= deltaTime;
                }
                else if (bullet.HomingRemaining > 0)
                {
                    bullet.HomingTarget = bullet.IsPlayerShot ? EnemyPosition : TargetPosition;
                    var desired = (bullet.HomingTarget - bullet.Position).Degrees;
                    if (bullet.HomingTurnRate < 0)
                    {
                        desired = DanmakuMath.NormalizeAngle(desired + 180.0);
                        bullet.Direction = DanmakuMath.MoveTowardsAngle(
                            bullet.Direction, desired, -bullet.HomingTurnRate * deltaTime);
                    }
                    else
                    {
                        bullet.Direction = DanmakuMath.MoveTowardsAngle(
                            bullet.Direction, desired, bullet.HomingTurnRate * deltaTime);
                    }
                    bullet.HomingRemaining -= deltaTime;
                    if (bullet.HomingRemaining <= 0) bullet.HomingEnabled = false;
                }
                else
                {
                    bullet.HomingEnabled = false;
                }
            }

            // --- 物理積分 ---
            bullet.Direction = DanmakuMath.NormalizeAngle(bullet.Direction + bullet.AngularVelocity * deltaTime);
            bullet.Speed += bullet.Acceleration * deltaTime;

            if (bullet.Damping < 1.0)
                bullet.Speed *= Math.Pow(bullet.Damping, deltaTime);

            var velocity = Vec2.FromDegrees(bullet.Direction, bullet.Speed);

            if (bullet.ExternalAcceleration.LengthSquared > 0)
            {
                velocity += bullet.ExternalAcceleration * deltaTime;
                bullet.Direction = velocity.Degrees;
                bullet.Speed = velocity.Length;
            }

            if (bullet.Speed < bullet.MinSpeed) bullet.Speed = bullet.MinSpeed;
            if (bullet.Speed > bullet.MaxSpeed) bullet.Speed = bullet.MaxSpeed;

            bullet.PreviousPosition = bullet.Position;
            bullet.Position += Vec2.FromDegrees(bullet.Direction, bullet.Speed) * deltaTime;

            // --- 見た目の更新 ---
            bullet.Age += deltaTime;
            bullet.Scale += bullet.ScaleVelocity * deltaTime;
            bullet.Rotation += bullet.RotationVelocity * deltaTime;

            if (bullet.HueVelocity != 0)
            {
                bullet.Hue = DanmakuMath.NormalizeAngle360(bullet.Hue + bullet.HueVelocity * deltaTime);
                var alpha = bullet.Color.A;
                bullet.Color = BulletColor.FromHsv(bullet.Hue, bullet.Saturation, bullet.Value, alpha);
            }

            bullet.UpdateTrail(deltaTime);

            // --- 分裂 ---
            if (bullet.Split is not null && bullet.SplitTimer >= 0)
            {
                bullet.SplitTimer -= deltaTime;
                if (bullet.SplitTimer <= 0) pendingSplits.Add(bullet);
            }

            // --- 寿命 ---
            if (bullet.Age >= bullet.Lifetime)
            {
                Kill(bullet, BulletDeathReason.Lifetime);
                continue;
            }

            // --- 画面外処理 ---
            HandleBounds(bullet, halfWidth, halfHeight);
        }
    }

    private void HandleBounds(Bullet bullet, double halfWidth, double halfHeight)
    {
        var x = bullet.Position.X;
        var y = bullet.Position.Y;
        var outside = x < -halfWidth || x > halfWidth || y < -halfHeight || y > halfHeight;
        if (!outside) return;

        switch (Settings.OutOfBounds)
        {
            case OutOfBoundsBehavior.Destroy:
                Kill(bullet, BulletDeathReason.OutOfBounds, playSound: false);
                break;

            case OutOfBoundsBehavior.Bounce:
            {
                var velocity = Vec2.FromDegrees(bullet.Direction, bullet.Speed);
                var vx = velocity.X;
                var vy = velocity.Y;
                if (x < -halfWidth || x > halfWidth) vx = -vx;
                if (y < -halfHeight || y > halfHeight) vy = -vy;
                bullet.Direction = new Vec2(vx, vy).Degrees;
                bullet.Position = new Vec2(
                    DanmakuMath.Clamp(x, -halfWidth, halfWidth),
                    DanmakuMath.Clamp(y, -halfHeight, halfHeight));
                EmitSound(DanmakuSoundKind.Change, bullet.EmitterIndex);
                break;
            }

            case OutOfBoundsBehavior.Wrap:
            {
                var nx = x;
                var ny = y;
                if (x < -halfWidth) nx = halfWidth;
                else if (x > halfWidth) nx = -halfWidth;
                if (y < -halfHeight) ny = halfHeight;
                else if (y > halfHeight) ny = -halfHeight;
                bullet.Position = new Vec2(nx, ny);
                break;
            }
        }
    }

    private void ProcessSplits()
    {
        if (pendingSplits.Count == 0) return;

        foreach (var parent in pendingSplits)
        {
            if (!parent.IsAlive) continue;
            var split = parent.Split;
            if (split is null) continue;

            if (parent.Generation < split.MaxGeneration)
                SpawnSplitChildren(parent, split);

            EmitSound(DanmakuSoundKind.Change, parent.EmitterIndex);

            if (split.DestroyParent)
                Kill(parent, BulletDeathReason.Split, playSound: false);
            else
                parent.Split = null;
        }

        pendingSplits.Clear();
    }

    private void SpawnSplitChildren(Bullet parent, SplitSpec split)
    {
        var count = Math.Max(1, split.Count);
        var isFullCircle = Math.Abs(Math.Abs(split.SpreadDegrees) - 360.0) < 1e-6;
        var step = count > 1
            ? (isFullCircle ? split.SpreadDegrees / count : split.SpreadDegrees / (count - 1))
            : 0;
        var start = isFullCircle || count <= 1
            ? parent.Direction + split.AngleOffset
            : parent.Direction + split.AngleOffset - split.SpreadDegrees / 2;

        var settings = Settings.Emitters[parent.EmitterIndex];

        for (var i = 0; i < count; i++)
        {
            var child = Pool.Rent();
            if (child is null) return;

            TotalSpawned++;

            child.EmitterIndex = parent.EmitterIndex;
            child.Generation = parent.Generation + 1;
            child.Position = parent.Position;
            child.PreviousPosition = parent.Position;
            child.Direction = DanmakuMath.NormalizeAngle(start + step * i);
            child.Speed = split.SpeedIsRelative ? parent.Speed + split.Speed : split.Speed;

            child.Acceleration = parent.Acceleration;
            child.AngularVelocity = parent.AngularVelocity;
            child.ExternalAcceleration = parent.ExternalAcceleration;
            child.Damping = parent.Damping;
            child.MinSpeed = parent.MinSpeed;
            child.MaxSpeed = parent.MaxSpeed;

            child.SpriteIndex = split.SpriteIndex >= 0 ? split.SpriteIndex : parent.SpriteIndex;
            child.Scale = parent.Scale * split.ScaleFactor;
            child.ScaleVelocity = parent.ScaleVelocity;
            child.Rotation = parent.Rotation;
            child.RotationVelocity = parent.RotationVelocity;
            child.AlignToDirection = parent.AlignToDirection;
            child.Color = split.Color ?? parent.Color;
            child.Hue = parent.Hue;
            child.HueVelocity = parent.HueVelocity;
            child.Saturation = parent.Saturation;
            child.Value = parent.Value;
            child.Additive = parent.Additive;
            child.FadeInDuration = parent.FadeInDuration;
            child.FadeOutDuration = parent.FadeOutDuration;
            child.AnimationFps = parent.AnimationFps;

            child.Lifetime = split.Lifetime > 0
                ? split.Lifetime
                : (double.IsFinite(parent.Lifetime) ? Math.Max(0.1, parent.Lifetime - parent.Age) : double.PositiveInfinity);

            child.HitRadius = parent.HitRadius;
            child.DestroyOnHit = parent.DestroyOnHit;

            child.TrailLength = parent.TrailLength;
            child.TrailInterval = parent.TrailInterval;

            child.HomingEnabled = settings.Physics.HomingEnabled;
            child.HomingTurnRate = settings.Physics.HomingTurnRate;
            child.HomingRemaining = settings.Physics.HomingDuration > 0
                ? settings.Physics.HomingDuration
                : double.PositiveInfinity;

            if (split.Next is not null && child.Generation < split.MaxGeneration)
            {
                child.Split = split.Next;
                child.SplitTimer = Math.Max(0.01, split.NextDelay);
            }
        }
    }

    private void ProcessCollisions()
    {
        var collision = Settings.Collision;
        var bullets = Pool.ActiveBullets;

        for (var i = 0; i < bullets.Count; i++)
        {
            var bullet = bullets[i];
            if (!bullet.IsAlive || bullet.HasHit || bullet.HitRadius <= 0) continue;

            if (bullet.IsPlayerShot)
            {
                // --- 自機弾 (相殺有効時) vs 敵弾 (ボスの手前の弾を先に相殺消去) ---
                if (bullet.CancelEnemyBullets && bullet.IsAlive)
                {
                    for (var j = 0; j < bullets.Count; j++)
                    {
                        var enemyBullet = bullets[j];
                        if (!enemyBullet.IsAlive || enemyBullet.IsPlayerShot || enemyBullet.HitRadius <= 0) continue;

                        var clashRadius = bullet.HitRadius * Math.Abs(bullet.Scale) + enemyBullet.HitRadius * Math.Abs(enemyBullet.Scale);
                        if (bullet.Position.DistanceSquaredTo(enemyBullet.Position) <= clashRadius * clashRadius)
                        {
                            enemyBullet.HasHit = true;
                            Kill(enemyBullet, BulletDeathReason.Hit, playSound: false);
                            EmitSound(DanmakuSoundKind.Vanish, enemyBullet.EmitterIndex);

                            if (collision.SpawnHitEffect)
                                SpawnHitEffectAt(enemyBullet.Position, enemyBullet, collision);

                            if (bullet.DestroyOnHit)
                            {
                                bullet.HasHit = true;
                                Kill(bullet, BulletDeathReason.Hit, playSound: false);
                                break;
                            }
                        }
                    }
                }

                // --- 自機弾 vs エネミー (ボス) ---
                if (bullet.IsAlive && collision.EnemyHitEnabled && EnemyRadius > 0)
                {
                    var radius = bullet.HitRadius * Math.Abs(bullet.Scale) + EnemyRadius;
                    if (bullet.Position.DistanceSquaredTo(EnemyPosition) <= radius * radius)
                    {
                        bullet.HasHit = true;
                        EnemyHitCount++;
                        HitCount++;
                        var dmg = Settings.HpBar.DamagePerHit > 0 ? Settings.HpBar.DamagePerHit : 15.0;
                        TotalDamageDealt += dmg;
                        var baseHp = BossMaxHp;
                        if (Live.BossHp?.Invoke(CurrentTime) is { } liveHp)
                        {
                            baseHp = Math.Clamp(liveHp / 100.0, 0.0, 1.0) * BossMaxHp;
                        }
                        CurrentBossHp = Math.Max(0.0, baseHp - TotalDamageDealt);
                        EmitSound(DanmakuSoundKind.Hit, bullet.EmitterIndex);

                        if (collision.SpawnHitEffect)
                            SpawnHitEffectAt(bullet.Position, bullet, collision);

                        if (bullet.DestroyOnHit)
                            Kill(bullet, BulletDeathReason.Hit, playSound: false);
                    }
                }
            }
            else
            {
                // --- 敵弾 vs 自機 (ターゲット) ---
                if (collision.IsEnabled && collision.TargetRadius > 0)
                {
                    var radius = bullet.HitRadius * Math.Abs(bullet.Scale) + collision.TargetRadius;
                    if (bullet.Position.DistanceSquaredTo(TargetPosition) <= radius * radius)
                    {
                        bullet.HasHit = true;
                        HitCount++;
                        EmitSound(DanmakuSoundKind.Hit, bullet.EmitterIndex);

                        if (collision.SpawnHitEffect)
                            SpawnHitEffectAt(bullet.Position, bullet, collision);

                        if (bullet.DestroyOnHit)
                            Kill(bullet, BulletDeathReason.Hit, playSound: false);
                    }
                }
            }
        }
    }

    private void SpawnHitEffectAt(Vec2 position, Bullet source, CollisionSettings collision)
    {
        var count = Math.Max(1, collision.HitEffectCount);
        for (var i = 0; i < count; i++)
        {
            var particle = Pool.Rent();
            if (particle is null) return;

            TotalSpawned++;

            particle.EmitterIndex = source.EmitterIndex;
            particle.Generation = source.Generation + 1;
            particle.Position = position;
            particle.PreviousPosition = position;
            particle.Direction = 360.0 / count * i + Random.NextSymmetric(15);
            particle.Speed = collision.HitEffectSpeed * Random.NextDouble(0.6, 1.4);
            particle.Damping = 0.02;
            particle.SpriteIndex = collision.HitEffectSpriteIndex;
            particle.Scale = Math.Abs(source.Scale) * 0.6;
            particle.ScaleVelocity = -Math.Abs(source.Scale) * 0.6 / Math.Max(0.05, collision.HitEffectLifetime);
            particle.Color = source.Color;
            particle.Additive = true;
            particle.Lifetime = collision.HitEffectLifetime;
            particle.FadeOutDuration = collision.HitEffectLifetime * 0.6;
            particle.HitRadius = 0;
        }
    }

    /// <summary>弾を生成する。</summary>
    public Bullet? Spawn(in BulletSpawnRequest request)
    {
        var bullet = Pool.Rent();
        if (bullet is null) return null;

        TotalSpawned++;

        var appearance = request.Appearance;
        var physics = request.Physics;

        bullet.EmitterIndex = request.EmitterIndex;
        bullet.Generation = request.Generation;
        bullet.Position = request.Position;
        bullet.PreviousPosition = request.Position;
        bullet.Direction = DanmakuMath.NormalizeAngle(request.Direction);

        physics.Apply(bullet, Random, request.IndexInBurst);

        if (!double.IsNaN(request.SpeedOverride))
        {
            var speed = request.SpeedOverride;
            if (physics.SpeedJitter > 0) speed += Random.NextSymmetric(physics.SpeedJitter);
            bullet.Speed = speed;
        }

        if (request.AngularVelocityOverride is { } angVel)
            bullet.AngularVelocity = angVel + (physics.AngularVelocityJitter > 0 ? Random.NextSymmetric(physics.AngularVelocityJitter) : 0);

        if (request.GravityOverride is { } grav || request.WindOverride is { } wind)
        {
            var g = request.GravityOverride ?? physics.Gravity;
            var w = request.WindOverride ?? physics.Wind;
            bullet.ExternalAcceleration = new Vec2(w, g);
        }

        if (request.AccelerationOverride is { } accel)
            bullet.Acceleration = accel;

        if (request.DampingOverride is { } damping)
            bullet.Damping = damping;

        if (request.MinSpeedOverride is { } minSpeed)
            bullet.MinSpeed = minSpeed;

        if (request.MaxSpeedOverride is { } maxSpeed)
            bullet.MaxSpeed = maxSpeed;

        if (request.HomingTurnRateOverride is { } homingTurn)
            bullet.HomingTurnRate = homingTurn;

        if (request.HomingDurationOverride is { } homingDur)
            bullet.HomingRemaining = homingDur;

        if (request.HomingDelayOverride is { } homingDelay)
            bullet.HomingDelay = homingDelay;

        if (request.HitRadiusOverride is { } hitRadius)
            bullet.HitRadius = hitRadius;

        if (request.LifetimeOverride > 0)
            bullet.Lifetime = request.LifetimeOverride;

        // --- 見た目 ---
        bullet.SpriteIndex = request.SpriteIndexOverride >= 0
            ? request.SpriteIndexOverride
            : ResolveSpriteIndex(appearance, request.IndexInBurst);

        var baseScale = request.ScaleOverride ?? appearance.Scale;
        var scale = baseScale * request.ScaleFactor;
        if (appearance.ScaleJitter > 0) scale += Random.NextSymmetric(appearance.ScaleJitter);
        bullet.Scale = Math.Max(0.01, scale);
        bullet.ScaleVelocity = request.ScaleVelocityOverride ?? appearance.ScaleVelocity;

        bullet.Rotation = appearance.Rotation;
        bullet.RotationVelocity = request.RotationVelocityOverride ?? appearance.RotationVelocity;
        bullet.AlignToDirection = appearance.AlignToDirection;
        bullet.Additive = appearance.Additive;
        bullet.FadeInDuration = request.FadeInDurationOverride ?? appearance.FadeInDuration;
        bullet.FadeOutDuration = request.FadeOutDurationOverride ?? appearance.FadeOutDuration;
        bullet.AnimationFps = appearance.AnimationFps;

        var trailLen = request.TrailLengthOverride ?? appearance.TrailLength;
        bullet.TrailLength = Math.Min(Math.Max(0, trailLen), Bullet.MaxTrailLength);
        bullet.TrailInterval = request.TrailIntervalOverride ?? appearance.TrailInterval;

        ApplyColor(bullet, appearance, request);

        if (request.OpacityOverride is { } opacity)
        {
            bullet.Color = bullet.Color with { A = (byte)Math.Clamp((int)Math.Round(bullet.Color.A * opacity), 0, 255) };
        }

        // --- 分裂 ---
        if (request.Split is not null && request.Generation < request.Split.MaxGeneration)
        {
            bullet.Split = request.Split;
            bullet.SplitTimer = Math.Max(0.01, request.SplitDelay);
        }

        bullet.Script = request.Script;

        if (request.PlayFireSound)
            EmitSound(DanmakuSoundKind.Fire, request.EmitterIndex);

        return bullet;
    }

    private static int ResolveSpriteIndex(BulletAppearance appearance, int indexInBurst)
    {
        var cycle = Math.Max(1, appearance.SpriteCycleCount);
        return cycle <= 1 ? appearance.SpriteIndex : appearance.SpriteIndex + indexInBurst % cycle;
    }

    private void ApplyColor(Bullet bullet, BulletAppearance appearance, in BulletSpawnRequest request)
    {
        var opacity = (float)DanmakuMath.Clamp(appearance.Opacity, 0, 1);

        if (request.ColorOverride is { } overrideColor)
        {
            bullet.Color = overrideColor.MultiplyAlpha(opacity);
            return;
        }

        switch (appearance.ColorMode)
        {
            case ColorMode.Original:
                bullet.Color = new BulletColor(1f, 1f, 1f, opacity);
                break;

            case ColorMode.Single:
                bullet.Color = appearance.PrimaryColor.MultiplyAlpha(opacity);
                break;

            case ColorMode.Gradient:
            {
                var steps = Math.Max(2, appearance.ColorGradientSteps);
                var t = (float)(request.IndexInBurst % steps) / (steps - 1);
                bullet.Color = BulletColor.Lerp(appearance.PrimaryColor, appearance.SecondaryColor, t)
                    .MultiplyAlpha(opacity);
                break;
            }

            case ColorMode.Rainbow:
            {
                var baseHue = request.EmitterIndex >= 0 && request.EmitterIndex < contexts.Count
                    ? contexts[request.EmitterIndex].RainbowBaseHue
                    : 0.0;
                bullet.Hue = DanmakuMath.NormalizeAngle360(appearance.HueStep * request.IndexInBurst + baseHue);
                bullet.HueVelocity = appearance.HueVelocity;
                bullet.Saturation = 0.9;
                bullet.Value = 1.0;
                bullet.Color = BulletColor.FromHsv(bullet.Hue, bullet.Saturation, bullet.Value, opacity);
                break;
            }

            case ColorMode.Palette:
                bullet.Color = BulletColor
                    .FromPaletteIndex(request.IndexInBurst + (int)(appearance.HueStep / 45.0))
                    .MultiplyAlpha(opacity);
                break;

            case ColorMode.Random:
            {
                bullet.Hue = Random.NextDouble(0, 360);
                bullet.Color = BulletColor.FromHsv(bullet.Hue, 0.85, 1.0, opacity);
                break;
            }
        }
    }

    /// <summary>弾を消滅させる。</summary>
    public void Kill(Bullet bullet, BulletDeathReason reason, bool playSound = true)
    {
        if (!bullet.IsAlive) return;
        bullet.DeathReason = reason;
        Pool.Return(bullet);

        if (playSound && reason is BulletDeathReason.Vanished or BulletDeathReason.Lifetime)
            EmitSound(DanmakuSoundKind.Vanish, bullet.EmitterIndex);
    }

    /// <summary>効果音イベントを記録する。ピッチは設定に従い微変調される。</summary>
    public void EmitSound(DanmakuSoundKind kind, int emitterIndex)
    {
        var settings = Settings.GetSound(kind);
        if (!settings.IsEnabled) return;

        var semitones = settings.PitchSemitones;
        if (settings.PitchJitterSemitones > 0)
            semitones += Random.NextSymmetric(settings.PitchJitterSemitones);

        SoundLog.Emit(kind, settings, CurrentTime, DanmakuMath.SemitoneToRatio(semitones), emitterIndex);
    }
}
