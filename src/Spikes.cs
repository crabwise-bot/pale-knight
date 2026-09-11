using Godot;

namespace PaleKnight;

/// <summary>
/// Pale spike strip. Hurts for 1 mask and sends the player back to their last
/// safe ground (HK-style). Player i-frames guard against multi-hit; the teleport
/// away guarantees the overlap ends.
/// Node origin: left edge of the strip, vertically centered in the tile row.
/// </summary>
public partial class Spikes : Area2D
{
    public void Setup(int widthCells)
    {
        float w = widthCells * 32f;
        CollisionLayer = 0;
        CollisionMask = 1;
        Monitoring = true;
        var shape = new CollisionShape2D
        {
            Name = "CollisionShape2D",
            Shape = new RectangleShape2D { Size = new Vector2(w, 20) }
        };
        AddChild(shape);
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not Player p) return;
        p.TakeDamage(1, GlobalPosition);
        if (!p.IsDead)
            p.ReturnToSafeGround();
    }

    public override void _Draw()
    {
        var shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        float w = 32f;
        if (shape?.Shape is RectangleShape2D rect)
            w = rect.Size.X;

        int cells = Mathf.Max(1, Mathf.RoundToInt(w / 32f));
        for (int i = 0; i < cells; i++)
        {
            for (int k = 0; k < 2; k++)
            {
                float x0 = i * 32f + k * 16f;
                var tri = new Vector2[]
                {
                    new Vector2(x0, 16f),
                    new Vector2(x0 + 16f, 16f),
                    new Vector2(x0 + 8f, -8f)
                };
                DrawColoredPolygon(tri, Palette.Pale);
                DrawLine(tri[0], tri[2], Palette.RockDark, 2f);
                DrawLine(tri[1], tri[2], Palette.RockDark, 2f);
            }
        }
    }
}
