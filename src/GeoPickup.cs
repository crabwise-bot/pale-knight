using Godot;

namespace PaleKnight;

/// <summary>
/// Single geo coin. Drifts with gravity (no collision), magnetizes toward the
/// player within range, and is collected on touch. Pure manual motion.
/// </summary>
public partial class GeoPickup : Area2D
{
    private const float MagnetRange = 130f;
    private const float CollectRange = 22f;
    private const float Life = 25f;
    private const float FadeTime = 1f;

    private Vector2 _vel;
    private float _age;
    private float _spin;

    public static void Spawn(Node parent, Vector2 pos, int count)
    {
        if (parent == null || count <= 0)
            return;
        for (int i = 0; i < count; i++)
        {
            var geo = new GeoPickup();
            parent.AddChild(geo);
            geo.GlobalPosition = pos + new Vector2(GD.Randf() * 20f - 10f, GD.Randf() * 10f - 8f);
        }
    }

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 0;
        Monitoring = false;
        Monitorable = false;
        _vel = new Vector2(GD.Randf() * 240f - 120f, -GD.Randf() * 170f - 50f);
        _spin = GD.Randf() * Mathf.Tau;
    }

    public override void _Process(double delta)
    {
        float d = (float)delta;
        _age += d;
        if (_age >= Life)
        {
            QueueFree();
            return;
        }
        if (Life - _age < FadeTime)
        {
            var m = Modulate;
            m.A = Mathf.Clamp((Life - _age) / FadeTime, 0f, 1f);
            Modulate = m;
        }

        var player = Game.Instance != null ? Game.Instance.Player : null;
        bool magnetized = false;
        if (player != null && IsInstanceValid(player) && !player.IsDead)
        {
            Vector2 toPlayer = player.GlobalPosition - GlobalPosition;
            float dist = toPlayer.Length();
            if (dist < CollectRange)
            {
                if (Game.Instance != null)
                    Game.Instance.AddGeo(1);
                if (AudioManager.Instance != null)
                    AudioManager.Instance.Play("geo", (float)GD.RandRange(0.95, 1.1));
                QueueFree();
                return;
            }
            if (dist < MagnetRange && dist > 0.001f)
            {
                magnetized = true;
                _vel = _vel.MoveToward(toPlayer / dist * 430f, 1500f * d);
            }
        }

        if (!magnetized)
        {
            _vel.Y = Mathf.Min(_vel.Y + 600f * d, 260f);
            _vel.X = Mathf.MoveToward(_vel.X, 0f, 260f * d);
        }

        GlobalPosition += _vel * d;
        _spin += d * 5f;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float s = 6f;
        var diamond = new Vector2[]
        {
            new Vector2(0f, -s).Rotated(_spin),
            new Vector2(s * 0.7f, 0f).Rotated(_spin),
            new Vector2(0f, s).Rotated(_spin),
            new Vector2(-s * 0.7f, 0f).Rotated(_spin),
        };
        DrawColoredPolygon(diamond, Palette.Geo, System.Array.Empty<Vector2>(), null);
        DrawLine(new Vector2(-2f, -3f).Rotated(_spin), new Vector2(1f, -1f).Rotated(_spin),
            new Color(1f, 1f, 1f, 0.8f), 1.5f, true);
    }
}
