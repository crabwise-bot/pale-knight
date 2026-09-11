using Godot;

namespace PaleKnight;

/// <summary>
/// Base class for all damageable enemies. Handles HP, white hit-flash,
/// knockback, soul gain on hit, contact damage, and the death sequence.
/// Subclasses build their collision shape and AI in <see cref="SetupBody"/>
/// and draw themselves by overriding _Draw (Visual is this node itself).
/// </summary>
public abstract partial class Enemy : CharacterBody2D, IDamageable
{
    [Export] public int MaxHp = 3;
    [Export] public int ContactDamage = 1;
    [Export] public int GeoValue = 5;
    [Export] public int SoulOnHit = 11;

    public int Hp { get; protected set; }
    public bool IsDead => Hp <= 0;

    protected Node2D Visual = null!;
    protected bool _dying;
    protected float _flashT;   // 1 -> 0 white hit-flash, read by subclass _Draw
    protected float _flinchT;  // brief post-hit flinch timer subclasses may use
    protected Area2D? _hurtbox;

    /// <summary>Body footprint used to size the contact-damage hurtbox.</summary>
    protected abstract Vector2 BodySize { get; }

    /// <summary>Build the CollisionShape2D and any AI helper nodes (rays, timers).</summary>
    protected abstract void SetupBody();

    public override void _Ready()
    {
        Hp = MaxHp;
        Visual = this;
        CollisionLayer = 2; // enemies
        CollisionMask = 1;  // world + player

        SetupBody();

        // Contact-damage hurtbox: detects the player, never collides physically.
        _hurtbox = new Area2D
        {
            CollisionLayer = 0,
            CollisionMask = 1,
            Monitoring = true,
            Monitorable = false,
        };
        var shape = new CollisionShape2D();
        shape.Shape = new RectangleShape2D { Size = BodySize };
        _hurtbox.AddChild(shape);
        _hurtbox.BodyEntered += OnHurtboxBodyEntered;
        AddChild(_hurtbox);
    }

    private void OnHurtboxBodyEntered(Node2D body)
    {
        if (_dying || IsDead)
            return;
        if (body is Player p && !p.IsDead)
            p.TakeDamage(ContactDamage, GlobalPosition);
    }

    public virtual void TakeHit(int damage, Vector2 fromPosition, Vector2 hitDirection)
    {
        if (IsDead || _dying)
            return;

        Hp -= damage;
        Game.Instance?.Player?.AddSoul(SoulOnHit);
        AudioManager.Instance?.Play("hit", 1f, 0f);
        Game.Instance?.Hitstop(0.06f, 0.1f);
        _flashT = 1f;
        ApplyKnockback(hitDirection);

        if (Hp <= 0)
            Die();
        else
            _flinchT = 0.22f;
    }

    /// <summary>Knockback applied on hit. The boss overrides this to a no-op.</summary>
    protected virtual void ApplyKnockback(Vector2 dir)
    {
        Velocity += dir * 200f + new Vector2(0f, -60f);
    }

    /// <summary>Squash, fade, drop geo, then free. Subclasses (boss) override for custom deaths.</summary>
    protected virtual async void Die()
    {
        _dying = true;
        SetDeferred("collision_layer", 0);
        SetDeferred("collision_mask", 0);
        if (_hurtbox != null)
            _hurtbox.SetDeferred("monitoring", false);

        var tw = CreateTween();
        tw.TweenProperty(this, "scale", new Vector2(1.35f, 0.55f), 0.12f);
        tw.TweenProperty(this, "modulate:a", 0f, 0.25f);

        Node? parent = GetParent();
        if (parent != null)
            GeoPickup.Spawn(parent, GlobalPosition, GeoValue);
        AudioManager.Instance?.Play("enemy_die", 1f, 0f);

        await ToSignal(GetTree().CreateTimer(0.4, true, false, true), SceneTreeTimer.SignalName.Timeout);
        if (!IsInstanceValid(this))
            return;
        QueueFree();
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (_flashT > 0f)
        {
            _flashT = Mathf.Max(0f, _flashT - dt * 7f);
            QueueRedraw();
        }
        if (_flinchT > 0f)
            _flinchT -= dt;
    }

    /// <summary>Shared helper: points of an ellipse polygon centered at <paramref name="center"/>.</summary>
    protected static Vector2[] EllipsePoints(Vector2 center, float rx, float ry, int count = 18)
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
