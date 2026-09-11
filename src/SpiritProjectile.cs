using Godot;
using System.Collections.Generic;

namespace PaleKnight;

/// <summary>
/// Vengeful Spirit projectile: a white ghost-head that flies horizontally,
/// passes through enemies (hitting each once), and dissipates on walls/range.
/// </summary>
public partial class SpiritProjectile : Area2D
{
    private const float MaxRange = 1000f;

    private Vector2 _dir = Vector2.Right;
    private float _travelled;
    private float _age;
    private readonly HashSet<IDamageable> _struck = new();

    public void Fire(Vector2 dir)
    {
        _dir = dir.Normalized();
        CollisionLayer = 0;
        CollisionMask = 2; // enemies + boss
        BodyEntered += OnBodyEntered;
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        float d = (float)delta;
        _age += d;
        float step = Player.SpellSpeed * d;
        GlobalPosition += _dir * step;
        _travelled += step;

        // Dissipate on walls via a short forward raycast.
        var space = GetWorld2D().DirectSpaceState;
        var query = new PhysicsRayQueryParameters2D
        {
            From = GlobalPosition,
            To = GlobalPosition + _dir * 26f,
            CollisionMask = 1,
        };
        if (space.IntersectRay(query).Count > 0 || _travelled >= MaxRange)
        {
            QueueFree();
            return;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float t = Mathf.Clamp(1f - _travelled / MaxRange, 0.2f, 1f);
        var glow = new Color(1f, 1f, 1f, 0.25f * t);
        var core = new Color(1f, 1f, 1f, 0.95f * t);
        var teal = new Color(Palette.Soul, 0.8f * t);

        // Trailing wisps.
        for (int i = 1; i <= 3; i++)
        {
            var wp = -_dir * i * 22f + new Vector2(0f, Mathf.Sin(_age * 12f + i * 2f) * 6f);
            DrawCircle(wp, 12f - i * 3f, new Color(1f, 1f, 1f, 0.12f * t));
        }
        // Ghost head: elongated glow + bright core.
        DrawEllipse(new Vector2(14f, 0f), 20f, 13f, glow);
        DrawEllipse(new Vector2(16f, 0f), 13f, 8.5f, core);
        // Eyes.
        float ex = _dir.X >= 0f ? 20f : 12f;
        DrawCircle(new Vector2(ex, -3.5f), 2.6f, teal);
        DrawCircle(new Vector2(ex, 3.5f), 2.6f, teal);
    }

    private void DrawEllipse(Vector2 center, float rx, float ry, Color col)
    {
        const int segs = 20;
        var pts = new List<Vector2>(segs);
        for (int i = 0; i < segs; i++)
        {
            float a = i / (float)segs * Mathf.Tau;
            pts.Add(center + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry));
        }
        DrawColoredPolygon(pts.ToArray(), col, System.Array.Empty<Vector2>(), null);
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not IDamageable dmg || dmg.IsDead || _struck.Contains(dmg))
            return;
        _struck.Add(dmg);
        dmg.TakeHit(Player.SpellDamage, GlobalPosition, _dir);

        var player = Game.Instance?.Player;
        if (player != null && IsInstanceValid(player) && !player.IsDead)
            player.AddSoul(Player.SoulPerHit);

        if (Game.Instance != null)
            Game.Instance.Hitstop(0.06f);
        if (AudioManager.Instance != null)
            AudioManager.Instance.Play("hit", 0.8f);
    }
}
