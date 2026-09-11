using Godot;

namespace PaleKnight;

/// <summary>
/// Flying enemy. Hovers around its spawn anchor on a lazy sine path; when the
/// player comes close it telegraphs (white strobe) then dives at the player's
/// locked position, recovers, and drifts back to hovering.
/// </summary>
public partial class Flyer : Enemy
{
    private enum Mode { Hover, Telegraph, Dive, Recover }

    private const float TriggerRange = 340f;
    private const float DiveSpeed = 400f;
    private const float DiveTime = 0.75f;
    private const float TelegraphTime = 0.5f;
    private const float RecoverTime = 0.6f;
    private const float CooldownTime = 2.2f;

    private Mode _mode = Mode.Hover;
    private float _t;          // visual/hover clock, always advances
    private float _stateT;     // time left in the current mode
    private float _cooldownT;
    private Vector2 _anchor;
    private Vector2 _diveTarget;

    public Flyer()
    {
        MaxHp = 2;
        GeoValue = 5;
    }

    protected override Vector2 BodySize => new Vector2(28f, 28f);

    protected override void SetupBody()
    {
        var shape = new CollisionShape2D();
        shape.Shape = new CircleShape2D { Radius = 13f };
        AddChild(shape);
    }

    public override void _Ready()
    {
        base._Ready();
        _anchor = GlobalPosition;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        float dt = (float)delta;
        if (_dying)
            return;

        _t += dt;
        Player? pl = Game.Instance?.Player;
        bool playerAlive = pl != null && !pl.IsDead;

        switch (_mode)
        {
            case Mode.Hover:
            {
                Vector2 target = _anchor + new Vector2(Mathf.Sin(_t * 1.1f) * 70f, Mathf.Sin(_t * 1.7f) * 36f);
                Velocity = (target - GlobalPosition) * 5f;
                _cooldownT -= dt;
                if (playerAlive && _cooldownT <= 0f && pl!.GlobalPosition.DistanceTo(GlobalPosition) < TriggerRange)
                {
                    _mode = Mode.Telegraph;
                    _stateT = TelegraphTime;
                    Velocity = Vector2.Zero;
                }
                break;
            }
            case Mode.Telegraph:
                Velocity = Velocity.MoveToward(Vector2.Zero, 800f * dt);
                _stateT -= dt;
                // 4 Hz white strobe while telegraphing.
                _flashT = Mathf.Sin(_t * Mathf.Tau * 4f) > 0f ? 1f : 0.15f;
                if (_stateT <= 0f)
                {
                    // Lock the player's position at the moment the dive starts.
                    _diveTarget = playerAlive ? pl!.GlobalPosition : GlobalPosition + new Vector2(100f, 0f);
                    Vector2 d = _diveTarget - GlobalPosition;
                    Velocity = d.Length() > 1f ? d.Normalized() * DiveSpeed : Vector2.Zero;
                    _mode = Mode.Dive;
                    _stateT = DiveTime;
                    _flashT = 0f;
                }
                break;
            case Mode.Dive:
                _stateT -= dt;
                if (_stateT <= 0f)
                {
                    _mode = Mode.Recover;
                    _stateT = RecoverTime;
                }
                break;
            case Mode.Recover:
                Velocity = Velocity.MoveToward(Vector2.Zero, 600f * dt);
                _stateT -= dt;
                if (_stateT <= 0f)
                {
                    _mode = Mode.Hover;
                    _cooldownT = CooldownTime;
                }
                break;
        }

        MoveAndSlide();
        QueueRedraw();
    }

    public override void _Draw()
    {
        float flap = Mathf.Sin(_t * 14f);

        // Little wings (triangles), flapping.
        DrawPolygon(new Vector2[]
        {
            new Vector2(-8f, -2f), new Vector2(-24f, -8f - flap * 8f), new Vector2(-14f, 8f)
        }, Palette.Rock);
        DrawPolygon(new Vector2[]
        {
            new Vector2(8f, -2f), new Vector2(24f, -8f - flap * 8f), new Vector2(14f, 8f)
        }, Palette.Rock);

        // Bell/ghost body.
        DrawPolygon(EllipsePoints(new Vector2(0f, 2f), 12f, 14f), Palette.RockDark);
        DrawArc(new Vector2(0f, 2f), 13f, 0.3f, Mathf.Pi - 0.3f, 16, Palette.RockEdge, 2f);

        // White mask with round eyes.
        DrawCircle(new Vector2(0f, -4f), 7.5f, Palette.Pale);
        DrawCircle(new Vector2(-3f, -5f), 2.2f, Palette.ShadeBlack);
        DrawCircle(new Vector2(3f, -5f), 2.2f, Palette.ShadeBlack);
        DrawCircle(new Vector2(-3.7f, -5.7f), 0.8f, Palette.Pale);

        // White hit-flash / telegraph strobe overlay.
        if (_flashT > 0f)
            DrawPolygon(EllipsePoints(new Vector2(0f, 2f), 13f, 15f), new Color(1f, 1f, 1f, _flashT * 0.85f));
    }
}
