using Godot;

namespace PaleKnight;

/// <summary>
/// The player's Shade, released on death (HK-authentic). Drifts toward the
/// player at ~140 px/s with a sine hover wobble, no gravity. 3 HP, contact
/// damage 1. Killing it restores the player's geo and uncaps soul — the Game
/// handles all of that via OnShadeKilled; the Shade itself drops no geo.
/// </summary>
public partial class Shade : Enemy
{
    private const float DriftSpeed = 140f;
    private const float WobbleAmp = 26f;
    private const float WobbleFreq = 3.2f;

    private float _t;
    private float _wobblePhase;

    /// <summary>Geo held for the player; set by Game after AddChild.</summary>
    public int HeldGeo { get; set; } = 0;

    public Shade()
    {
        MaxHp = 3;
        ContactDamage = 1;
        GeoValue = 0; // no geo drop — Game.OnShadeKilled handles rewards
        SoulOnHit = 11;
    }

    protected override Vector2 BodySize => new Vector2(24f, 30f);

    protected override void SetupBody()
    {
        var shape = new CollisionShape2D();
        shape.Shape = new CircleShape2D { Radius = 13f };
        AddChild(shape);

        _wobblePhase = GD.Randf() * Mathf.Tau;
    }

    public override void _Ready()
    {
        base._Ready();
        AddToGroup("shade");
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        float dt = (float)delta;
        if (_dying)
            return;

        _t += dt;

        // Drift toward the player; perpendicular sine wobble for the hover feel.
        Player? pl = Game.Instance?.Player;
        Vector2 drift = Vector2.Zero;
        if (pl != null && !pl.IsDead)
        {
            Vector2 toPlayer = pl.GlobalPosition - GlobalPosition;
            if (toPlayer.Length() > 4f)
                drift = toPlayer.Normalized() * DriftSpeed;
        }
        Vector2 perp = new Vector2(-drift.Y, drift.X).Normalized();
        if (perp.Length() < 0.5f)
            perp = Vector2.Right;
        Vector2 wobble = perp * Mathf.Sin(_t * WobbleFreq + _wobblePhase) * WobbleAmp;

        Velocity = drift + wobble;
        MoveAndSlide();

        // Slight transparency flicker.
        float alpha = 0.78f + 0.22f * Mathf.Sin(_t * 7f + _wobblePhase);
        Modulate = new Color(1f, 1f, 1f, alpha);

        QueueRedraw();
    }

    protected override async void Die()
    {
        _dying = true;
        SetDeferred("collision_layer", 0);
        SetDeferred("collision_mask", 0);
        if (_hurtbox != null)
            _hurtbox.SetDeferred("monitoring", false);

        // Game restores geo and uncaps soul. No geo drop here.
        Game.Instance?.OnShadeKilled(GlobalPosition);
        AudioManager.Instance?.Play("enemy_die", 0.8f, -4f);

        var tw = CreateTween();
        tw.TweenProperty(this, "modulate:a", 0f, 0.3f);

        await ToSignal(GetTree().CreateTimer(0.3, true, false, true), SceneTreeTimer.SignalName.Timeout);
        if (!IsInstanceValid(this))
            return;
        QueueFree();
    }

    public override void _Draw()
    {
        // Black wisp silhouette: teardrop body with a wispy tail.
        DrawPolygon(EllipsePoints(new Vector2(0f, -2f), 11f, 14f), new[] { Palette.ShadeBlack });
        DrawPolygon(new Vector2[]
        {
            new Vector2(-8f, 8f), new Vector2(8f, 8f),
            new Vector2(3f, 22f + Mathf.Sin(_t * 5f) * 3f),
            new Vector2(-3f, 22f - Mathf.Sin(_t * 5f) * 3f),
        }, new[] { Palette.ShadeBlack });
        // Head tendrils.
        DrawLine(new Vector2(-6f, -14f), new Vector2(-12f, -24f + Mathf.Sin(_t * 6f) * 2f), Palette.ShadeBlack, 3f);
        DrawLine(new Vector2(6f, -14f), new Vector2(12f, -24f - Mathf.Sin(_t * 6f) * 2f), Palette.ShadeBlack, 3f);

        // White eyes.
        DrawCircle(new Vector2(-4.5f, -4f), 2.6f, Palette.Pale);
        DrawCircle(new Vector2(4.5f, -4f), 2.6f, Palette.Pale);

        // White hit-flash overlay.
        if (_flashT > 0f)
            DrawPolygon(EllipsePoints(new Vector2(0f, -2f), 12f, 15f), new[] { new Color(1f, 1f, 1f, _flashT * 0.85f) });
    }
}
