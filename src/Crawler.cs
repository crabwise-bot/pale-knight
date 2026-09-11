using Godot;

namespace PaleKnight;

/// <summary>
/// Ground patrol enemy. Walks in a straight line, turning around at walls
/// and ledges. Dark oval body, white mask with angry slit eyes, animated legs.
/// </summary>
public partial class Crawler : Enemy
{
    private const float Speed = 70f;
    private const float Gravity = 1500f;

    private int _dir = 1;
    private float _walkPhase;
    private RayCast2D? _ray;

    public Crawler()
    {
        GeoValue = 5;
    }

    protected override Vector2 BodySize => new Vector2(28f, 20f);

    protected override void SetupBody()
    {
        var shape = new CollisionShape2D();
        shape.Shape = new RectangleShape2D { Size = BodySize };
        AddChild(shape);

        // Ledge-detection ray: points ahead of the direction of travel, down past the feet.
        _ray = new RayCast2D
        {
            Position = new Vector2(14f * _dir, 4f),
            TargetPosition = new Vector2(14f * _dir, 34f),
            CollisionMask = 1,
            Enabled = true,
        };
        _ray.CollideWithAreas = false;
        _ray.CollideWithBodies = true;
        AddChild(_ray);

        if (GD.Randf() < 0.5f)
            _dir = -1;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        float dt = (float)delta;
        if (_dying)
            return;

        Velocity = new Vector2(_dir * Speed, Velocity.Y + Gravity * dt);
        MoveAndSlide();

        if (_ray != null)
        {
            _ray.Position = new Vector2(14f * _dir, 4f);
            _ray.TargetPosition = new Vector2(14f * _dir, 34f);
        }

        if (IsOnWall())
        {
            _dir *= -1;
        }
        else if (IsOnFloor() && _ray != null && !_ray.IsColliding())
        {
            // No ground ahead: turn around.
            _dir *= -1;
        }

        if (IsOnFloor())
            _walkPhase += dt * 12f;
        QueueRedraw();
    }

    public override void _Draw()
    {
        // Legs: 3 per side, animated.
        for (int i = 0; i < 3; i++)
        {
            float bx = -9f + i * 9f;
            float step = Mathf.Sin(_walkPhase + i * 2.1f) * 4f;
            DrawLine(new Vector2(bx, 4f), new Vector2(bx + step, 13f), Palette.RockEdge, 2f);
        }

        // Dark oval body.
        DrawSetTransform(Vector2.Zero, 0f, new Vector2(1f, 0.72f));
        DrawCircle(Vector2.Zero, 14f, Palette.RockDark);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        DrawLine(new Vector2(-10f, 4f), new Vector2(10f, 4f), Palette.RockEdge, 2f);

        // White mask on the front, with angry slit eyes.
        Vector2 maskCenter = new Vector2(9f * _dir, -5f);
        DrawPolygon(EllipsePoints(maskCenter, 8f, 10f), new[] { Palette.Pale });
        Vector2 eyeA = maskCenter + new Vector2(-4f * _dir, -2f);
        Vector2 eyeB = maskCenter + new Vector2(4f * _dir, 0f);
        DrawLine(eyeA + new Vector2(-3f, 0f), eyeA + new Vector2(3f, 0f), Palette.ShadeBlack, 2.5f);
        DrawLine(eyeB + new Vector2(-3f, 0f), eyeB + new Vector2(3f, 0f), Palette.ShadeBlack, 2.5f);

        // White hit-flash overlay.
        if (_flashT > 0f)
            DrawPolygon(EllipsePoints(Vector2.Zero, 15f, 11f), new[] { new Color(1f, 1f, 1f, _flashT * 0.85f) });
    }
}
