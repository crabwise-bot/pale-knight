using System.Collections.Generic;
using Godot;

namespace PaleKnight;

/// <summary>
/// The Warden: arena boss. Authentic Hollow Knight rules — no HP bar, no name
/// banner, no damage numbers. Damage is communicated via white hit-flash, sfx,
/// and behavior changes (phase 2 roar, stagger).
///
/// The room builder sets ArenaBounds and calls Activate() when the player
/// enters. Attacks: LeapSlam (shadow-marked), DashAcross, Ranged (arcing
/// wisps). Stagger after 8 hits within 3s (max 3 per fight); hitting during a
/// stagger wakes the boss early.
/// </summary>
public partial class Warden : Enemy
{
    private enum State
    {
        Dormant,        // waiting for Activate()
        Intro,          // drop-in, driven by a tween
        Idle,           // choosing the next attack — the heal window
        TelegraphLeap,
        Leaping,
        TelegraphDash,
        Dashing,
        TelegraphRanged,
        Ranged,
        Recover,        // post-attack breather — the heal window
        Staggered,
        RoarTell,       // phase-2 transition
        Dying,
    }

    private enum Attack { LeapSlam, DashAcross, Ranged }

    private const float Gravity = 1500f;
    private const float LeapDuration = 0.5f;
    private const float LeapHeight = 170f;
    private const float SlamRadius = 110f;
    private const float DashSpeed = 760f;

    [Signal] public delegate void DefeatedEventHandler();
    [Signal] public delegate void PhaseChangedEventHandler(int phase);

    /// <summary>Arena rectangle, set by the room builder before Activate().</summary>
    public Rect2 ArenaBounds;

    public bool FightActive { get; private set; }

    // Pose fields read by WardenVisual.Draw.
    public float CrouchAmount;
    public float Collapsed;
    public float RearUp;
    public bool Airborne;
    public bool Trembling;
    public int FaceDir = 1;
    public float RingT;
    public float HitFlash => _flashT;

    private State _state = State.Dormant;
    private float _stateT;
    private int _phase = 1;
    private Attack? _lastAttack;
    private int _hits;          // consecutive hits within the stagger window
    private float _lastHitT;
    private int _staggers;

    private Vector2 _spawn;
    private float _groundY;
    private ShadowMarker? _marker;

    private int _leapCount;
    private Vector2 _leapFrom;
    private Vector2 _leapTo;
    private float _leapT;

    private int _dashDir = 1;
    private bool _dashHitDone;

    private int _shotsLeft;
    private float _shotT;

    public override void _Ready()
    {
        MaxHp = 40;
        ContactDamage = 1;
        GeoValue = 40;
        SoulOnHit = 11;
        base._Ready();

        _spawn = GlobalPosition;
        _groundY = GlobalPosition.Y;
        if (ArenaBounds.Size == Vector2.Zero)
            ArenaBounds = new Rect2(GlobalPosition - new Vector2(420f, 200f), new Vector2(840f, 400f));
    }

    protected override Vector2 BodySize => new Vector2(64f, 84f);

    protected override void SetupBody()
    {
        var shape = new CollisionShape2D();
        shape.Shape = new RectangleShape2D { Size = BodySize };
        AddChild(shape);
    }

    // ------------------------------------------------------------------ setup

    /// <summary>Called by the room when the player enters the arena.</summary>
    public void Activate()
    {
        if (FightActive || _dying || IsDead)
            return;
        FightActive = true;

        foreach (Node n in GetTree().GetNodesInGroup("boss_gates"))
            n.Call("Close");

        AudioManager.Instance?.Play("roar", 1f, 0f);
        AudioManager.Instance?.PlayMusic("boss_music");

        _state = State.Intro;
        Airborne = true;
        GlobalPosition = _spawn + new Vector2(0f, -500f);
        var tw = CreateTween();
        tw.TweenProperty(this, "global_position", _spawn, 0.7)
            .SetTrans(Tween.TransitionType.Bounce)
            .SetEase(Tween.EaseType.Out);
        tw.TweenCallback(Callable.From(OnIntroLanded));
    }

    private void OnIntroLanded()
    {
        if (_dying || !IsInstanceValid(this))
            return;
        Airborne = false;
        AudioManager.Instance?.Play("slam", 1f, 0f);
        Game.Instance?.ShakeCamera(14f, 0.4f);
        RingT = 1f;
        _state = State.Idle;
        _stateT = 0.9f;
    }

    // ------------------------------------------------------------------ damage

    public override void TakeHit(int damage, Vector2 fromPosition, Vector2 hitDirection)
    {
        if (IsDead || _dying || _state == State.Dying || _state == State.Dormant || _state == State.Intro)
            return;

        bool wasStaggered = _state == State.Staggered;
        base.TakeHit(damage, fromPosition, hitDirection); // no knockback (overridden below)
        if (IsDead || _dying)
            return; // Die() handled it.

        // Hitting the boss during a stagger wakes it immediately (HK rule).
        if (wasStaggered)
        {
            EndStaggerToIdle();
            return;
        }

        // Phase 2 transition at half HP.
        if (_phase == 1 && Hp <= 20)
        {
            _phase = 2;
            _hits = 0;
            StartRoarTell();
            EmitSignal(SignalName.PhaseChanged, 2);
            return;
        }

        // Stagger tracking: 8 hits within a rolling 3s window, max 3 per fight.
        float now = Time.GetTicksMsec() / 1000f;
        _hits = (now - _lastHitT <= 3.0f) ? _hits + 1 : 1;
        _lastHitT = now;
        if (_hits >= 8 && _staggers < 3 && _state != State.Staggered)
            StartStagger();
    }

    protected override void ApplyKnockback(Vector2 dir)
    {
        // The boss does not flinch from knockback.
    }

    private void StartStagger()
    {
        _staggers++;
        DismissMarker();
        SetHurtboxEnabled(true); // a stagger can interrupt the dash, which disables the hurtbox
        _state = State.Staggered;
        _stateT = 2.5f;
        Velocity = Vector2.Zero;
        Collapsed = 1f;
        CrouchAmount = 0f;
        RearUp = 0f;
        Trembling = false;
        Airborne = false; // a stagger can interrupt the leap mid-air; the boss drops
        AudioManager.Instance?.Play("stagger", 1f, 0f);
        Game.Instance?.ShakeCamera(6f, 0.4f);
    }

    private void EndStaggerToIdle()
    {
        Collapsed = 0f;
        _state = State.Idle;
        _stateT = 1.2f; // generous breather after a stagger
    }

    private void StartRoarTell()
    {
        DismissMarker();
        SetHurtboxEnabled(true); // phase change can interrupt the dash, which disables the hurtbox
        _state = State.RoarTell;
        _stateT = 1.0f;
        Velocity = Vector2.Zero;
        AudioManager.Instance?.Play("roar", 1f, 0f);
        Game.Instance?.ShakeCamera(8f, 1.0f);
    }

    protected override async void Die()
    {
        _dying = true;
        _state = State.Dying;
        DismissMarker();
        SetDeferred("collision_layer", 0);
        SetDeferred("collision_mask", 0);
        if (_hurtbox != null)
            _hurtbox.SetDeferred("monitoring", false);

        Velocity = Vector2.Zero;
        Collapsed = 1f;
        CrouchAmount = 0f;
        RearUp = 0f;
        Trembling = false;
        RingT = 1.5f; // big white burst ring

        Engine.TimeScale = 0.25f;
        AudioManager.Instance?.Play("boss_die", 1f, 0f);
        Game.Instance?.ShakeCamera(10f, 1f);

        await ToSignal(GetTree().CreateTimer(1.0, true, false, true), SceneTreeTimer.SignalName.Timeout);
        Engine.TimeScale = 1f; // restore before the validity check so a rebuild can't trap slow-mo
        if (!IsInstanceValid(this))
            return;

        Node? parent = GetParent();
        if (parent != null)
            GeoPickup.Spawn(parent, GlobalPosition, 40);
        Game.Instance?.Player?.AddSoul(99);

        foreach (Node n in GetTree().GetNodesInGroup("boss_gates"))
            n.Call("Open");

        AudioManager.Instance?.PlayMusic("ambient_drone");
        EmitSignal(SignalName.Defeated);
        Game.Instance?.OnBossDefeated();

        await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
        if (!IsInstanceValid(this))
            return;
        QueueFree();
    }

    // ------------------------------------------------------------------ brain

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        float dt = (float)delta;
        if (_dying)
        {
            QueueRedraw();
            return;
        }
        if (RingT > 0f)
            RingT = Mathf.Max(0f, RingT - dt * 1.4f);

        Player? pl = Game.Instance?.Player;

        switch (_state)
        {
            case State.Dormant:
            case State.Intro:
            case State.Dying:
                break; // intro is tween-driven; dying is handled in Die()

            case State.Idle:
                ApplyGroundPhysics(dt);
                FacePlayer(pl);
                CrouchAmount = Mathf.MoveToward(CrouchAmount, 0f, dt * 4f);
                RearUp = Mathf.MoveToward(RearUp, 0f, dt * 4f);
                _stateT -= dt;
                if (_stateT <= 0f)
                    PickAttack();
                break;

            case State.TelegraphLeap:
                ApplyGroundPhysics(dt);
                FacePlayer(pl);
                CrouchAmount = Mathf.MoveToward(CrouchAmount, 1f, dt * 5f);
                _stateT -= dt;
                if (_stateT <= 0f)
                    BeginLeap();
                break;

            case State.Leaping:
                UpdateLeap(dt);
                break;

            case State.TelegraphDash:
                ApplyGroundPhysics(dt);
                Trembling = true;
                CrouchAmount = Mathf.MoveToward(CrouchAmount, 0.6f, dt * 6f);
                _stateT -= dt;
                if (_stateT <= 0f)
                    BeginDash(pl);
                break;

            case State.Dashing:
                UpdateDash(dt, pl);
                break;

            case State.TelegraphRanged:
                ApplyGroundPhysics(dt);
                FacePlayer(pl);
                RearUp = Mathf.MoveToward(RearUp, 1f, dt * 5f);
                _stateT -= dt;
                if (_stateT <= 0f)
                    BeginRanged();
                break;

            case State.Ranged:
                ApplyGroundPhysics(dt);
                UpdateRanged(dt, pl);
                break;

            case State.Recover:
                ApplyGroundPhysics(dt);
                CrouchAmount = Mathf.MoveToward(CrouchAmount, 0f, dt * 3f);
                RearUp = Mathf.MoveToward(RearUp, 0f, dt * 3f);
                _stateT -= dt;
                if (_stateT <= 0f)
                {
                    _state = State.Idle;
                    _stateT = _phase == 2 ? 0.5f : 0.9f;
                }
                break;

            case State.Staggered:
                ApplyGroundPhysics(dt);
                _stateT -= dt;
                if (_stateT <= 0f)
                    EndStaggerToIdle();
                break;

            case State.RoarTell:
                ApplyGroundPhysics(dt);
                RearUp = Mathf.MoveToward(RearUp, 1f, dt * 6f);
                _stateT -= dt;
                if (_stateT <= 0f)
                {
                    RearUp = 0f;
                    _state = State.Idle;
                    _stateT = 0.9f;
                }
                break;
        }

        QueueRedraw();
    }

    private void ApplyGroundPhysics(double dt)
    {
        float d = (float)dt;
        Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0f, 3000f * d), Velocity.Y + Gravity * d);
        MoveAndSlide();
    }

    private void FacePlayer(Player? pl)
    {
        if (pl != null && !pl.IsDead)
            FaceDir = pl.GlobalPosition.X >= GlobalPosition.X ? 1 : -1;
    }

    private void PickAttack()
    {
        var options = new List<Attack> { Attack.LeapSlam, Attack.DashAcross, Attack.Ranged };
        if (_lastAttack.HasValue)
            options.Remove(_lastAttack.Value); // never repeat the same attack twice
        Attack atk = options[GD.RandRange(0, options.Count - 1)];
        _lastAttack = atk;

        switch (atk)
        {
            case Attack.LeapSlam:
                _leapCount = 0;
                StartLeapTelegraph(_phase == 2 ? 0.6f : 0.75f);
                break;
            case Attack.DashAcross:
                _state = State.TelegraphDash;
                _stateT = 0.55f;
                _dashHitDone = false;
                break;
            case Attack.Ranged:
                _state = State.TelegraphRanged;
                _stateT = 0.6f;
                break;
        }
    }

    // --------------------------------------------------------------- leap slam

    private void StartLeapTelegraph(float duration)
    {
        _state = State.TelegraphLeap;
        _stateT = duration;

        Player? pl = Game.Instance?.Player;
        float px = (pl != null && !pl.IsDead) ? pl.GlobalPosition.X : GlobalPosition.X;
        float x = Mathf.Clamp(px, ArenaBounds.Position.X + 60f, ArenaBounds.End.X - 60f);

        DismissMarker();
        _marker = new ShadowMarker();
        Node? parent = GetParent();
        if (parent != null)
            parent.AddChild(_marker);
        _marker.Setup(new Vector2(x, _groundY + 44f), 70f);
    }

    private void DismissMarker()
    {
        if (_marker != null)
        {
            _marker.Dismiss();
            _marker = null;
        }
    }

    private void SetHurtboxEnabled(bool enabled)
    {
        if (_hurtbox != null && IsInstanceValid(_hurtbox))
            _hurtbox.SetDeferred("monitoring", enabled);
    }

    private void BeginLeap()
    {
        _leapFrom = GlobalPosition;
        float x = _marker != null ? _marker.GlobalPosition.X : GlobalPosition.X;
        _leapTo = new Vector2(x, _groundY);
        _leapT = 0f;
        _state = State.Leaping;
        Airborne = true;
        CrouchAmount = 0f;
    }

    private void UpdateLeap(float dt)
    {
        _leapT += dt / LeapDuration;
        float k = Mathf.Clamp(_leapT, 0f, 1f);
        GlobalPosition = _leapFrom.Lerp(_leapTo, k) + new Vector2(0f, -Mathf.Sin(k * Mathf.Pi) * LeapHeight);
        if (_leapT >= 1f)
            LandLeap();
    }

    private void LandLeap()
    {
        GlobalPosition = _leapTo;
        Airborne = false;
        CrouchAmount = 0.5f;
        DismissMarker();

        AudioManager.Instance?.Play("slam", 1f, 0f);
        Game.Instance?.ShakeCamera(14f, 0.4f);
        RingT = 1f;

        Player? pl = Game.Instance?.Player;
        if (pl != null && !pl.IsDead && pl.GlobalPosition.DistanceTo(GlobalPosition) < SlamRadius)
            pl.TakeDamage(2, GlobalPosition);

        if (_phase == 2 && _leapCount == 0)
        {
            // Phase 2: chain a second, faster leap at the player's current position.
            _leapCount = 1;
            StartLeapTelegraph(0.4f);
        }
        else
        {
            _state = State.Recover;
            _stateT = 0.5f;
        }
    }

    // ------------------------------------------------------------- dash across

    private void BeginDash(Player? pl)
    {
        _dashDir = (pl != null && !pl.IsDead && pl.GlobalPosition.X < GlobalPosition.X) ? -1 : 1;
        FaceDir = _dashDir;
        _state = State.Dashing;
        _stateT = 0.65f;
        Trembling = false;
        // Disable contact damage during the dash; the dash hitbox below handles it.
        SetHurtboxEnabled(false);
        AudioManager.Instance?.Play("dash", 1f, 0f);
    }

    private void UpdateDash(float dt, Player? pl)
    {
        Velocity = new Vector2(_dashDir * DashSpeed, Velocity.Y + Gravity * dt);
        MoveAndSlide();

        float minX = ArenaBounds.Position.X + 40f;
        float maxX = ArenaBounds.End.X - 40f;
        if (IsOnWall() || GlobalPosition.X <= minX || GlobalPosition.X >= maxX)
        {
            GlobalPosition = new Vector2(Mathf.Clamp(GlobalPosition.X, minX, maxX), GlobalPosition.Y);
            Velocity = Vector2.Zero;
            Game.Instance?.ShakeCamera(8f, 0.3f);
            AudioManager.Instance?.Play("slam", 0.8f, -4f);
            RingT = 0.7f;
            EndDash();
            return;
        }

        if (pl != null && !pl.IsDead && !_dashHitDone
            && pl.GlobalPosition.DistanceTo(GlobalPosition) < 46f)
        {
            _dashHitDone = true; // one hit per dash
            pl.TakeDamage(1, GlobalPosition);
        }

        _stateT -= dt;
        if (_stateT <= 0f)
            EndDash();
    }

    private void EndDash()
    {
        _state = State.Recover;
        _stateT = 0.5f;
        Velocity = new Vector2(0f, Velocity.Y);
        if (!_dying)
            SetHurtboxEnabled(true);
    }

    // ------------------------------------------------------------------ ranged

    private void BeginRanged()
    {
        _state = State.Ranged;
        _shotsLeft = _phase == 2 ? 3 : 2;
        _shotT = 0f;
    }

    private void UpdateRanged(float dt, Player? pl)
    {
        _shotT -= dt;
        if (_shotT <= 0f && _shotsLeft > 0)
        {
            FireProjectile(pl);
            _shotsLeft--;
            _shotT = 0.18f;
        }
        RearUp = Mathf.MoveToward(RearUp, 0f, dt * 3f);
        if (_shotsLeft <= 0)
        {
            _state = State.Recover;
            _stateT = 0.5f;
        }
    }

    private void FireProjectile(Player? pl)
    {
        Vector2 mouth = GlobalPosition + new Vector2(FaceDir * 34f, -56f);
        Vector2 aim = (pl != null && !pl.IsDead)
            ? pl.GlobalPosition + new Vector2(0f, -20f) - mouth
            : new Vector2(FaceDir * 100f, -40f);
        Vector2 dir = aim.Length() > 1f ? aim.Normalized() : Vector2.Right;
        Vector2 vel = dir * 260f + new Vector2(0f, -260f); // toward the player, arcing up

        var proj = new BossProjectile();
        Node? parent = GetParent();
        if (parent != null)
            parent.AddChild(proj);
        proj.Setup(mouth, vel);
    }

    public override void _Draw()
    {
        WardenVisual.Draw(this);
    }
}

/// <summary>
/// Draws the Warden: armored beetle-knight with layered dark plates, a big
/// horned white mask, and glowing soul-teal eyes. Poses are driven by the
/// public fields on <see cref="Warden"/> (crouch, rear-up, collapse, ring).
/// </summary>
public static class WardenVisual
{
    public static void Draw(Warden w)
    {
        float ox = w.Trembling ? (GD.Randf() - 0.5f) * 7f : 0f;
        float sy = 1f - 0.35f * w.CrouchAmount - 0.30f * w.Collapsed;
        float sx = 1f + 0.30f * w.CrouchAmount + 0.20f * w.Collapsed;
        sy = Mathf.Max(sy, 0.45f);

        // Squash toward the feet (local y = +42).
        w.DrawSetTransform(new Vector2(ox, 42f - 42f * sy), 0f, new Vector2(sx, sy));

        // Layered dark plates.
        w.DrawPolygon(new Vector2[]
        {
            new Vector2(-30f, 42f), new Vector2(30f, 42f),
            new Vector2(22f, 10f), new Vector2(-22f, 10f)
        }, new[] { Palette.RockDark });
        w.DrawPolygon(new Vector2[]
        {
            new Vector2(-26f, 12f), new Vector2(26f, 12f),
            new Vector2(18f, -18f), new Vector2(-18f, -18f)
        }, new[] { Palette.Rock });
        w.DrawLine(new Vector2(-22f, 10f), new Vector2(22f, 10f), Palette.RockEdge, 2f);
        w.DrawLine(new Vector2(-18f, -18f), new Vector2(18f, -18f), Palette.RockEdge, 2f);
        // Shoulder spikes.
        w.DrawPolygon(new Vector2[]
        {
            new Vector2(-26f, 6f), new Vector2(-40f, -6f), new Vector2(-24f, -8f)
        }, new[] { Palette.RockEdge });
        w.DrawPolygon(new Vector2[]
        {
            new Vector2(26f, 6f), new Vector2(40f, -6f), new Vector2(24f, -8f)
        }, new[] { Palette.RockEdge });

        float mx = w.FaceDir * 10f;
        float maskY = -34f + 10f * w.Collapsed - 8f * w.RearUp;

        // Horns.
        w.DrawPolygon(new Vector2[]
        {
            new Vector2(mx - 12f, maskY - 20f), new Vector2(mx - 24f, maskY - 46f), new Vector2(mx - 2f, maskY - 26f)
        }, new[] { Palette.Pale });
        w.DrawPolygon(new Vector2[]
        {
            new Vector2(mx + 12f, maskY - 20f), new Vector2(mx + 24f, maskY - 46f), new Vector2(mx + 2f, maskY - 26f)
        }, new[] { Palette.Pale });
        // Big white mask.
        w.DrawPolygon(Ellipse(new Vector2(mx, maskY), 19f, 25f), new[] { Palette.Pale });

        if (w.Collapsed >= 0.5f)
        {
            // Knocked out: dark closed slits.
            w.DrawLine(new Vector2(mx - 10f, maskY - 2f), new Vector2(mx - 2f, maskY - 2f), Palette.ShadeBlack, 3f);
            w.DrawLine(new Vector2(mx + 2f, maskY - 2f), new Vector2(mx + 10f, maskY - 2f), Palette.ShadeBlack, 3f);
        }
        else
        {
            // Glowing soul-teal eyes.
            float ey = maskY - 4f;
            var glow = new Color(Palette.Soul, 0.35f);
            w.DrawCircle(new Vector2(mx - 8f, ey), 7f, glow);
            w.DrawCircle(new Vector2(mx + 8f, ey), 7f, glow);
            w.DrawCircle(new Vector2(mx - 8f, ey), 3.5f, Palette.Soul);
            w.DrawCircle(new Vector2(mx + 8f, ey), 3.5f, Palette.Soul);
        }

        w.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);

        // Expanding ground ring (slam / death burst).
        if (w.RingT > 0f)
        {
            float k = Mathf.Min(w.RingT, 1.5f);
            float radius = 20f + (1.5f - k) / 1.5f * 230f;
            w.DrawArc(new Vector2(0f, 42f), radius, 0f, Mathf.Tau, 48,
                new Color(1f, 1f, 1f, k * 0.55f), 6f);
        }

        // White hit-flash overlay.
        if (w.HitFlash > 0f)
            w.DrawPolygon(Ellipse(Vector2.Zero, 36f, 48f), new[] { new Color(1f, 1f, 1f, w.HitFlash * 0.7f) });
    }

    private static Vector2[] Ellipse(Vector2 center, float rx, float ry, int count = 20)
    {
        var pts = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            float a = i / (float)count * Mathf.Tau;
            pts[i] = center + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
        }
        return pts;
    }
}
