using System.Collections.Generic;
using Godot;

namespace PaleKnight;

/// <summary>
/// Arcing wisp fired by the Warden. Flies on a manual ballistic path (no
/// physics body); hits the player on proximity, expires after a lifetime.
/// Draws a teal-white core with a fading trail, and a small expanding burst
/// on impact or timeout.
/// </summary>
public partial class BossProjectile : Area2D
{
    private const float FallAccel = 950f;
    private const float Lifetime = 4f;
    private const float HitRadius = 26f;
    private const int TrailLength = 12;
    private const float BurstTime = 0.3f;

    private Vector2 _vel;
    private float _life = Lifetime;
    private readonly List<Vector2> _trail = new();
    private bool _bursting;
    private float _burstT;

    /// <summary>Call after the node has been added to the tree.</summary>
    public void Setup(Vector2 pos, Vector2 vel)
    {
        GlobalPosition = pos;
        _vel = vel;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        if (_bursting)
        {
            _burstT -= dt;
            QueueRedraw();
            if (_burstT <= 0f)
                QueueFree();
            return;
        }

        _vel = new Vector2(_vel.X, _vel.Y + FallAccel * dt);
        GlobalPosition += _vel * dt;

        _trail.Add(GlobalPosition);
        while (_trail.Count > TrailLength)
            _trail.RemoveAt(0);

        Player? pl = Game.Instance?.Player;
        if (pl != null && !pl.IsDead && pl.GlobalPosition.DistanceTo(GlobalPosition) < HitRadius)
        {
            pl.TakeDamage(1, GlobalPosition);
            Burst();
            return;
        }

        _life -= dt;
        if (_life <= 0f)
            Burst();

        QueueRedraw();
    }

    private void Burst()
    {
        _bursting = true;
        _burstT = BurstTime;
    }

    public override void _Draw()
    {
        if (_bursting)
        {
            // 3 expanding, fading circles.
            float k = 1f - _burstT / BurstTime;
            for (int i = 0; i < 3; i++)
            {
                float r = k * (18f + i * 12f);
                float a = (_burstT / BurstTime) * 0.8f;
                DrawArc(Vector2.Zero, r, 0f, Mathf.Tau, 24, new Color(Palette.Pale, a), 3f);
            }
            return;
        }

        // Fading trail.
        for (int i = 0; i < _trail.Count; i++)
        {
            float k = (i + 1) / (float)_trail.Count;
            DrawCircle(ToLocal(_trail[i]), 2f + 5f * k, new Color(Palette.Soul, 0.4f * k));
        }

        // Teal-white wisp core.
        DrawCircle(Vector2.Zero, 9f, Palette.Pale);
        DrawCircle(Vector2.Zero, 5f, Palette.Soul);
    }
}
