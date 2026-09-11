using Godot;

namespace PaleKnight;

/// <summary>
/// The Pale Knight: movement, nail combat, soul/focus healing, damage & death.
/// Collision, visuals, and light are all built in code; no scene file needed.
/// </summary>
public partial class Player : CharacterBody2D
{
    // ---- Tuning (HK research: snappy accel, 0.35s dash, 1.2s i-frames) ----
    public const float MoveSpeed = 230f;
    public const float Accel = 4000f;
    public const float Friction = 5000f;
    public const float JumpVel = -780f;
    public const float GravityUp = 1600f;
    public const float GravityDown = 2200f;
    public const float MaxFall = 1100f;
    public const float CoyoteTime = 0.08f;
    public const float JumpBuffer = 0.08f;
    public const float DashSpeed = 520f;
    public const float DashTime = 0.35f;
    public const float DashCooldown = 0.6f;
    public const float WallSlideMaxFall = 110f;
    public const float WallJumpX = 420f;
    public const float WallRegrabLockout = 0.15f;
    public const float AttackCooldown = 0.35f;
    public const float HealChannel = 1.1f;
    public const float HealChainTime = 0.9f;
    public const float IframeTime = 1.2f;
    public const float CastTapTime = 0.25f;
    public const float PogoVel = -500f;

    public const int MaxHealth = 5;
    public const int MaxSoul = 99;
    public const int HealCost = 33;
    public const int SoulPerHit = 11;
    public const int SpellCost = 33;
    public const int SpellDamage = 3;
    public const float SpellSpeed = 700f;

    public int Health { get; private set; } = MaxHealth;
    public int Soul { get; private set; } = 0;
    public int Facing { get; private set; } = 1;
    public bool IsDead { get; private set; } = false;
    public bool IsHealing => _healing;
    public bool IsDashing => _dashTimer > 0f;

    /// <summary>Soul meter cap: 99, or 66 while the Shade is loose (HK death penalty).</summary>
    public int SoulCap => Game.Instance != null && Game.Instance.ShadeActive ? 66 : 99;

    /// <summary>Last safe grounded position (spikes send the knight back here).</summary>
    public Vector2 LastSafeGround { get; set; } = Vector2.Zero;

    private KnightVisual _visual = null!;
    private PointLight2D _light = null!;

    private float _iframes;
    private float _coyote;
    private float _jumpBuffer;
    private float _attackCooldown;
    private float _dashTimer;
    private float _dashCooldown;
    private float _afterimageTimer;
    private bool _airDashUsed;
    private bool _healing;
    private float _healTimer;
    private int _healCount;
    private bool _castHeld;
    private float _castTimer;
    private float _safeGroundTimer;

    public override void _Ready()
    {
        CollisionLayer = 1;
        CollisionMask = 1;
        FloorSnapLength = 4f;

        var shapeNode = new CollisionShape2D();
        var capsule = new CapsuleShape2D { Radius = 9f, Height = 30f };
        shapeNode.Shape = capsule;
        shapeNode.Position = new Vector2(0f, -2f);
        AddChild(shapeNode);

        _visual = new KnightVisual();
        AddChild(_visual);

        _light = new PointLight2D();
        _light.Position = new Vector2(0f, -10f);
        _light.Energy = 1.0f;
        _light.TextureScale = 5f;
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(1f, 0.96f, 0.88f, 0.6f));
        gradient.SetColor(1, new Color(1f, 0.96f, 0.88f, 0f));
        var glowTex = new GradientTexture2D
        {
            Gradient = gradient,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
            Width = 128,
            Height = 128,
        };
        _light.Texture = glowTex;
        AddChild(_light);

        if (Game.Instance != null)
        {
            if (Game.Instance.Player == null)
                Game.Instance.Player = this;
            Game.Instance.EmitSignal("HealthChanged", Health, MaxHealth);
            Game.Instance.EmitSignal("SoulChanged", Soul);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float d = (float)delta;

        if (IsDead)
        {
            Velocity = new Vector2(Velocity.X, Mathf.Min(Velocity.Y + GravityDown * d, MaxFall));
            MoveAndSlide();
            return;
        }

        // ---- Timers ----
        _iframes = Mathf.Max(0f, _iframes - d);
        _coyote = Mathf.Max(0f, _coyote - d);
        _jumpBuffer = Mathf.Max(0f, _jumpBuffer - d);
        _attackCooldown = Mathf.Max(0f, _attackCooldown - d);
        _dashCooldown = Mathf.Max(0f, _dashCooldown - d);

        bool wasOnFloor = IsOnFloor();
        float inputX = Input.GetAxis("move_left", "move_right");

        if (_dashTimer <= 0f && !_healing && Mathf.Abs(inputX) > 0.01f)
            Facing = inputX > 0f ? 1 : -1;

        // ---- Dash ----
        if (Input.IsActionJustPressed("dash") && _dashCooldown <= 0f && _dashTimer <= 0f && !_healing)
        {
            bool canAirDash = IsOnFloor() || !_airDashUsed;
            if (canAirDash)
            {
                int dashDir = Mathf.Abs(inputX) > 0.01f ? (inputX > 0f ? 1 : -1) : Facing;
                Facing = dashDir;
                CancelHeal();
                _dashTimer = DashTime;
                _dashCooldown = DashCooldown;
                _afterimageTimer = 0f;
                if (!IsOnFloor())
                    _airDashUsed = true;
                Velocity = new Vector2(dashDir * DashSpeed, 0f);
                _iframes = Mathf.Max(_iframes, DashTime + 0.05f);
                _castHeld = false;
                if (AudioManager.Instance != null)
                    AudioManager.Instance.Play("dash");
            }
        }

        if (_dashTimer > 0f)
        {
            _dashTimer -= d;
            _afterimageTimer -= d;
            if (_afterimageTimer <= 0f)
            {
                SpawnAfterimage();
                _afterimageTimer = 0.04f;
            }
            int dashDir = Velocity.X >= 0f ? 1 : -1;
            Velocity = new Vector2(dashDir * DashSpeed, 0f);
            if (_dashTimer <= 0f)
                Velocity *= 0.35f;
        }

        // ---- Attack ----
        if (Input.IsActionJustPressed("attack") && _attackCooldown <= 0f && _dashTimer <= 0f && !_healing)
            DoAttack();

        // ---- Cast: tap = Vengeful Spirit, hold = Focus heal (same button, HK-style) ----
        if (Input.IsActionJustPressed("cast") && !_healing && _dashTimer <= 0f)
        {
            _castHeld = true;
            _castTimer = 0f;
        }
        if (_castHeld)
        {
            _castTimer += d;
            if (_castTimer >= CastTapTime && !_healing && _dashTimer <= 0f
                && Soul >= HealCost && Health < MaxHealth)
            {
                BeginFocus();
            }
            if (Input.IsActionJustReleased("cast"))
            {
                if (!_healing && _castTimer < CastTapTime)
                    TryCastSpell();
                _castHeld = false;
            }
        }
        if (_healing)
        {
            if (!Input.IsActionPressed("cast"))
            {
                // Released early (or interrupted): soul already deducted is wasted.
                CancelHeal();
            }
            else
            {
                _healTimer += d;
                float need = _healCount == 0 ? HealChannel : HealChainTime;
                if (_healTimer >= need)
                    CompleteHealChannel();
            }
        }

        // ---- Jump input ----
        if (Input.IsActionJustPressed("jump"))
            _jumpBuffer = JumpBuffer;

        // ---- Gravity (suspended while dashing) ----
        if (_dashTimer <= 0f)
        {
            float grav = (Velocity.Y < 0f && Input.IsActionPressed("jump")) ? GravityUp : GravityDown;
            Velocity = new Vector2(Velocity.X, Mathf.Min(Velocity.Y + grav * d, MaxFall));
        }

        // ---- Horizontal movement ----
        if (_dashTimer <= 0f)
        {
            float target = inputX * MoveSpeed * (_healing ? 0.15f : 1f);
            float rate = Mathf.Abs(target) > 0.01f ? Accel : Friction;
            if (!IsOnFloor())
                rate *= 0.65f;
            Velocity = new Vector2(Mathf.MoveToward(Velocity.X, target, rate * d), Velocity.Y);
        }

        // ---- Wall slide ----
        bool onFloor = IsOnFloor();
        if (!onFloor && IsOnWall() && Velocity.Y > 0f && _dashTimer <= 0f)
            Velocity = new Vector2(Velocity.X, Mathf.Min(Velocity.Y, WallSlideMaxFall));

        // ---- Jump execution ----
        if (_jumpBuffer > 0f && (onFloor || _coyote > 0f))
        {
            Velocity = new Vector2(Velocity.X, JumpVel);
            _jumpBuffer = 0f;
            _coyote = 0f;
            _visual.OnJump();
            if (AudioManager.Instance != null)
                AudioManager.Instance.Play("jump");
        }
        else if (_jumpBuffer > 0f && !onFloor && _coyote <= 0f && IsOnWall())
        {
            float nx = GetWallNormal().X;
            Facing = nx >= 0f ? 1 : -1;
            Velocity = new Vector2(nx * WallJumpX, JumpVel);
            _jumpBuffer = 0f;
            _visual.OnJump();
            if (AudioManager.Instance != null)
                AudioManager.Instance.Play("jump");
        }

        // ---- Variable jump height ----
        if (Input.IsActionJustReleased("jump") && Velocity.Y < 0f)
            Velocity = new Vector2(Velocity.X, Velocity.Y * 0.45f);

        MoveAndSlide();

        // ---- Landing ----
        onFloor = IsOnFloor();
        if (!wasOnFloor && onFloor)
        {
            _visual.OnLand();
            _airDashUsed = false;
        }
        if (onFloor)
        {
            _coyote = CoyoteTime;
            _safeGroundTimer -= d;
            if (_safeGroundTimer <= 0f)
            {
                LastSafeGround = GlobalPosition;
                _safeGroundTimer = 0.25f;
            }
        }

        // ---- Damage flicker ----
        if (_iframes > 0f)
        {
            bool dim = Mathf.FloorToInt(Time.GetTicksMsec() / 90f) % 2 == 0;
            _visual.Modulate = new Color(1f, 1f, 1f, dim ? 0.35f : 1f);
        }
        else if (_visual.Modulate.A < 1f)
        {
            _visual.Modulate = Colors.White;
        }

        // ---- Visual pose ----
        int wallDir = 0;
        if (!onFloor && IsOnWall())
            wallDir = GetWallNormal().X < 0f ? 1 : -1;
        _visual.RunSpeed = onFloor ? Mathf.Abs(Velocity.X) : 0f;
        _visual.SetPose(new KnightVisual.KnightPose
        {
            Facing = Facing,
            Grounded = onFloor,
            Dashing = _dashTimer > 0f,
            WallDir = wallDir,
            Crouch = _healing ? 0.6f : 0f,
        });
    }

    private void DoAttack()
    {
        Vector2 dir;
        bool downSlam = false;
        if (Input.IsActionPressed("move_up"))
        {
            dir = Vector2.Up;
        }
        else if (Input.IsActionPressed("move_down") && !IsOnFloor())
        {
            dir = Vector2.Down;
            downSlam = true;
        }
        else
        {
            dir = new Vector2(Facing, 0f);
        }

        var parent = GetParent();
        if (parent == null)
            return;
        var slash = new Slash();
        parent.AddChild(slash);
        slash.GlobalPosition = GlobalPosition;
        slash.Setup(this, dir, downSlam);
        _attackCooldown = AttackCooldown;
        if (AudioManager.Instance != null)
            AudioManager.Instance.Play("slash");
    }

    private void SpawnAfterimage()
    {
        var parent = GetParent();
        if (parent == null)
            return;
        var ghost = new Afterimage();
        parent.AddChild(ghost);
        ghost.GlobalPosition = GlobalPosition;
        ghost.Setup(new KnightVisual.KnightPose
        {
            Facing = Facing,
            RunPhase = 0f,
            Grounded = IsOnFloor(),
            Dashing = true,
        }, new Color(0.45f, 1f, 0.9f, 0.5f));
    }

    /// <summary>Bounce off a down-slash hit (pogo). Called by Slash.</summary>
    public void Pogo()
    {
        if (IsDead)
            return;
        Velocity = new Vector2(Velocity.X * 0.5f, PogoVel);
        _airDashUsed = false;
        _coyote = 0f;
        _jumpBuffer = 0f;
        _visual.OnJump();
        if (AudioManager.Instance != null)
            AudioManager.Instance.Play("jump", 1.2f);
    }

    /// <summary>Teleport back to the last safe grounded spot (spike hits).</summary>
    public void ReturnToSafeGround()
    {
        GlobalPosition = LastSafeGround + new Vector2(0f, -4f);
        Velocity = Vector2.Zero;
    }

    /// <summary>Vengeful Spirit: quick tap of cast fires a 3-damage spirit.</summary>
    private void TryCastSpell()
    {
        if (Soul < SpellCost || IsDead)
        {
            // Not enough soul: dull feedback so the button doesn't feel dead.
            if (Soul < SpellCost && !IsDead)
            {
                Game.Instance?.Hud?.PulseSoulDenied();
                if (AudioManager.Instance != null)
                    AudioManager.Instance.Play("soul_denied");
            }
            return;
        }
        AddSoul(-SpellCost);
        var parent = GetParent();
        if (parent == null)
            return;
        var spirit = new SpiritProjectile();
        parent.AddChild(spirit);
        spirit.GlobalPosition = GlobalPosition + new Vector2(Facing * 24f, -10f);
        spirit.Fire(new Vector2(Facing, 0f));
        if (AudioManager.Instance != null)
            AudioManager.Instance.Play("spell_cast");
    }

    /// <summary>Begin a Focus channel: soul is deducted up front, wasted if interrupted.</summary>
    private void BeginFocus()
    {
        AddSoul(-HealCost);
        _healing = true;
        _healTimer = 0f;
        _healCount = 0;
        _visual.SetHealing(true);
        if (AudioManager.Instance != null)
            AudioManager.Instance.Play("heal", 0.6f);
    }

    private void CompleteHealChannel()
    {
        AddHealth(1);
        if (AudioManager.Instance != null)
            AudioManager.Instance.Play("heal");
        _healCount++;
        _healTimer = 0f;
        // Keep channeling while held: each extra mask costs another 33 soul.
        if (Health < MaxHealth && Soul >= HealCost && Input.IsActionPressed("cast"))
        {
            AddSoul(-HealCost);
        }
        else
        {
            _healing = false;
            _visual.SetHealing(false);
        }
    }

    public void TakeDamage(int masks, Vector2 fromPosition)
    {
        if (IsDead || _iframes > 0f)
            return;
        AddHealth(-masks);
        CancelHeal();
        _castHeld = false;
        _dashTimer = 0f;

        Vector2 away = GlobalPosition - fromPosition;
        if (away.LengthSquared() < 0.001f)
            away = new Vector2(-Facing, 0f);
        away = away.Normalized();
        Velocity = new Vector2(away.X * 280f, -200f);

        _iframes = IframeTime;
        if (AudioManager.Instance != null)
            AudioManager.Instance.Play("player_hurt");
        if (Game.Instance != null)
            Game.Instance.ShakeCamera(6f, 0.25f);

        if (Health <= 0)
            Die();
    }

    public void AddSoul(int n)
    {
        Soul = Mathf.Clamp(Soul + n, 0, SoulCap);
        if (Game.Instance != null)
            Game.Instance.EmitSignal("SoulChanged", Soul);
    }

    public void AddHealth(int n)
    {
        Health = Mathf.Clamp(Health + n, 0, MaxHealth);
        if (Game.Instance != null)
            Game.Instance.EmitSignal("HealthChanged", Health, MaxHealth);
    }

    private void CancelHeal()
    {
        if (!_healing)
            return;
        _healing = false;
        _healTimer = 0f;
        if (_visual != null)
            _visual.SetHealing(false);
    }

    private void Die()
    {
        IsDead = true;
        CancelHeal();
        _iframes = 0f;
        if (_visual != null)
            _visual.Modulate = Colors.White;
        Velocity = new Vector2(0f, -260f);
        if (AudioManager.Instance != null)
            AudioManager.Instance.Play("player_hurt", 0.6f);
        if (Game.Instance != null)
            Game.Instance.OnPlayerDied();
    }
}
