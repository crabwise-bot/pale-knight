using Godot;

namespace PaleKnight;

/// <summary>
/// Pale archway the player walks through to change rooms.
/// Detects the player (Area2D, layer 0 / mask 1) and asks Game to swap rooms.
/// </summary>
public partial class Door : Area2D
{
    private DoorDef _def;
    private bool _used;
    private float _t;

    public void Setup(DoorDef def)
    {
        _def = def;
        CollisionLayer = 0;
        CollisionMask = 1;
        Monitoring = true;
        var shape = new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(48, 96) }
        };
        AddChild(shape);
        BodyEntered += OnBodyEntered;
    }

    private async void OnBodyEntered(Node2D body)
    {
        if (_used || _def == null) return;
        if (body is not Player) return;
        _used = true;
        AudioManager.Instance?.Play("gate");
        Game.Instance?.RequestRoomChange(_def.TargetRoom, _def.TargetDoor);
        // Re-arm after a short delay so the spawn-in overlap can't double-fire.
        var tree = GetTree();
        if (tree != null)
        {
            await ToSignal(tree.CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
            if (IsInstanceValid(this))
                _used = false;
        }
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        // Doorway darkness.
        DrawRect(new Rect2(-14, -48, 28, 96), Palette.ShadeBlack);
        // Soft soul-glow pulse behind the arch.
        float glow = 0.5f + 0.5f * Mathf.Sin(_t * 2.5f);
        DrawCircle(Vector2.Zero, 34f, new Color(Palette.Soul, 0.05f + 0.05f * glow));
        // Two pillars.
        DrawRect(new Rect2(-24, -48, 10, 96), Palette.RockEdge);
        DrawRect(new Rect2(14, -48, 10, 96), Palette.RockEdge);
        DrawLine(new Vector2(-24, -48), new Vector2(-24, 48), Palette.PaleDim, 2f);
        DrawLine(new Vector2(24, -48), new Vector2(24, 48), Palette.PaleDim, 2f);
        // Arch across the pillar tops.
        DrawArc(new Vector2(0, -48), 14f, Mathf.Pi, Mathf.Tau, 16, Palette.Pale, 4f);
        // Threshold.
        DrawLine(new Vector2(-24, 48), new Vector2(24, 48), Palette.PaleDim, 2f);
    }
}
