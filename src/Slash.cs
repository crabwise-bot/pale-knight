using Godot;
using System.Collections.Generic;

namespace PaleKnight;

/// <summary>
/// Short-lived nail swoosh. Spawned as a child of the player's parent so it
/// never inherits the visual flip. Hits enemies (layer 2) once each.
/// </summary>
public partial class Slash : Area2D
{
    private const float Life = 0.12f;

    private Player _player = null!;
    private Vector2 _dir = Vector2.Right;
    private bool _downSlam;
    private float _age;
    private readonly HashSet<IDamageable> _struck = new();

    public void Setup(Player player, Vector2 dir, bool downSlam)
    {
        _player = player;
        if (dir.LengthSquared() > 0.001f)
            _dir = dir.Normalized();
        else if (player != null)
            _dir = new Vector2(player.Facing, 0f);
        _downSlam = downSlam;

        CollisionLayer = 0;
        CollisionMask = 2;

        var shapeNode = new CollisionShape2D();
        var rect = new RectangleShape2D();
        if (Mathf.Abs(_dir.X) > 0.5f)
        {
            rect.Size = new Vector2(64f, 36f);
            shapeNode.Position = _dir * 34f;
        }
        else
        {
            rect.Size = new Vector2(40f, 62f);
            shapeNode.Position = new Vector2(0f, Mathf.Sign(_dir.Y) * 34f);
        }
        shapeNode.Shape = rect;
        AddChild(shapeNode);

        BodyEntered += OnBodyEntered;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= Life)
        {
            QueueFree();
            return;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float t = 1f - _age / Life;
        float baseAngle = _dir.Angle();
        DrawArc(Vector2.Zero, 30f, baseAngle - 1.0f, baseAngle + 1.0f, 18,
            new Color(Palette.Pale, 0.85f * t), 6f, true);
        DrawArc(Vector2.Zero, 21f, baseAngle - 0.8f, baseAngle + 0.8f, 14,
            new Color(Palette.Pale, 0.4f * t), 3f, true);
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not IDamageable dmg || dmg.IsDead || _struck.Contains(dmg))
            return;
        _struck.Add(dmg);
        dmg.TakeHit(1, GlobalPosition, _dir);

        if (_player != null && IsInstanceValid(_player) && !_player.IsDead)
        {
            _player.AddSoul(Player.SoulPerHit);
            if (_downSlam && !_player.IsOnFloor())
                _player.Pogo();
        }

        if (Game.Instance != null)
            Game.Instance.Hitstop(0.06f);
        if (AudioManager.Instance != null)
            AudioManager.Instance.Play("hit");
    }
}
