using Godot;

namespace PaleKnight;

/// <summary>
/// Rest bench. Sitting (E / "interact") heals to full, sets the respawn point,
/// saves, and respawns all non-boss enemies in the current room (HK-authentic).
/// The Warden is spawned by the arena trigger, never via EnemySpawns, so it is untouched.
/// </summary>
public partial class Bench : Area2D
{
    private string _benchId = "";
    private string _roomId = "";
    private string _doorTag = "";
    private PointLight2D _light;
    private float _t;
    private float _restCooldown;
    private bool _eWasDown;

    public void Setup(string benchId, string roomId, string doorTag)
    {
        _benchId = benchId;
        _roomId = roomId;
        _doorTag = doorTag;
        CollisionLayer = 0;
        CollisionMask = 1;
        Monitoring = true;
        var shape = new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(110, 70) },
            Position = new Vector2(0, -10)
        };
        AddChild(shape);
        _light = new PointLight2D
        {
            Color = Palette.BenchGlow,
            Energy = 0.9f,
            TextureScale = 3.0f,
            Position = new Vector2(0, -40)
        };
        AddChild(_light);
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        _restCooldown = Mathf.Max(0f, _restCooldown - (float)delta);
        QueueRedraw();

        var p = Game.Instance?.Player;
        if (p == null || p.IsDead || _restCooldown > 0f) return;
        if (p.GlobalPosition.DistanceTo(GlobalPosition) > 70f) return;

        bool eDown = Input.IsKeyPressed(Key.E);
        bool pressed = (InputMap.HasAction("interact") && Input.IsActionJustPressed("interact"))
            || (eDown && !_eWasDown);
        _eWasDown = eDown;
        if (pressed)
            OnRest();
    }

    /// <summary>Heal to full, set respawn, save, respawn non-boss enemies.</summary>
    public void OnRest()
    {
        var game = Game.Instance;
        var p = game?.Player;
        if (p == null || p.IsDead) return;
        _restCooldown = 0.8f;

        p.AddHealth(Player.MaxHealth - p.Health);

        if (game!.Data != null)
        {
            game.Data.BenchRoom = _roomId;
            game.Data.BenchDoor = _doorTag;
        }
        game.SaveGame();

        // Respawn non-boss enemies: free current ones, rebuild from recorded placements.
        var build = GetParent()?.GetNodeOrNull<RoomBuildHolder>("RoomBuild")?.Build
            ?? RoomBuilder.Current;
        var tree = GetTree();
        if (build != null && tree != null)
        {
            foreach (var n in tree.GetNodesInGroup("enemies"))
            {
                if (n != null && IsInstanceValid(n))
                    n.QueueFree();
            }
            RoomBuilder.SpawnEnemies(build, build.Root ?? GetParent());
        }

        AudioManager.Instance?.Play("bench");
        game.Hud?.Toast("Restored. Progress saved.");

        if (_light != null)
        {
            var tw = CreateTween();
            tw.TweenProperty(_light, "energy", 1.6f, 0.25f);
            tw.TweenProperty(_light, "energy", 0.9f, 0.6f);
        }
    }

    public override void _Draw()
    {
        // Warm glow halo.
        float pulse = 0.5f + 0.5f * Mathf.Sin(_t * 2.0f);
        DrawCircle(new Vector2(0, -30), 60f + 8f * pulse, new Color(Palette.BenchGlow, 0.06f));
        // Legs.
        DrawRect(new Rect2(-40, -18, 10, 18), Palette.RockEdge);
        DrawRect(new Rect2(30, -18, 10, 18), Palette.RockEdge);
        // Stone seat with pale top edge.
        DrawRect(new Rect2(-48, -30, 96, 14), Palette.PaleDim);
        DrawRect(new Rect2(-48, -30, 96, 4), Palette.Pale);

        // "REST [E]" hint while the player is close.
        var p = Game.Instance?.Player;
        if (p != null && !p.IsDead && p.GlobalPosition.DistanceTo(GlobalPosition) <= 70f)
        {
            DrawString(ThemeDB.FallbackFont, new Vector2(-100, -72), "REST [E]",
                HorizontalAlignment.Center, 200f, 18, Palette.Pale);
        }
    }
}
