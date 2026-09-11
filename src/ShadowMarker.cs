using Godot;

namespace PaleKnight;

/// <summary>
/// Telegraph decal for the Warden's LeapSlam: a dark-red ellipse on the
/// ground that grows in and pulses while the boss winds up. Dismiss() fades
/// it out and frees it.
/// </summary>
public partial class ShadowMarker : Node2D
{
    private float _radius = 70f;
    private float _grow; // 0 -> 1 grow-in
    private float _t;
    private bool _dismissing;
    private float _fade = 1f;

    /// <summary>Call after the node has been added to the tree.</summary>
    public void Setup(Vector2 pos, float radius)
    {
        GlobalPosition = pos;
        _radius = radius;
    }

    public void Dismiss()
    {
        _dismissing = true;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        _grow = Mathf.Min(1f, _grow + dt * 4f);

        if (_dismissing)
        {
            _fade -= dt * 5f;
            if (_fade <= 0f)
            {
                QueueFree();
                return;
            }
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        float grow = Mathf.Max(_grow, 0.01f);
        float pulse = 0.55f + 0.25f * Mathf.Sin(_t * 10f);

        // Flat dark-red ellipse, pulsing alpha.
        DrawSetTransform(Vector2.Zero, 0f, new Vector2(1.5f, 0.55f) * grow);
        DrawCircle(Vector2.Zero, _radius, new Color(Palette.Danger, pulse * _fade));
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);

        // Thin bright rim so the edge reads against dark ground.
        DrawSetTransform(Vector2.Zero, 0f, new Vector2(1.5f, 0.55f) * grow);
        DrawArc(Vector2.Zero, _radius, 0f, Mathf.Tau, 40, new Color(Palette.Danger, 0.9f * _fade), 3f);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
