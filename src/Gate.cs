using Godot;

namespace PaleKnight;

/// <summary>
/// Iron gate used to lock the player in the Warden arena (and to lock the
/// post-boss path until victory). Slides up to open, down to close.
/// Node origin is the gate center; bars span 96px (3 tiles) vertically.
/// </summary>
public partial class Gate : StaticBody2D
{
    private CollisionShape2D _shape;
    private float _baseY;
    private float _widthPx = 64f;
    private bool _closed;

    /// <summary>Call after Position is set, before AddChild.</summary>
    public void Setup(float widthPx)
    {
        _widthPx = widthPx;
        _baseY = Position.Y;
        CollisionLayer = 1;
        CollisionMask = 0;
        _shape = new CollisionShape2D
        {
            Name = "CollisionShape2D",
            Shape = new RectangleShape2D { Size = new Vector2(widthPx, 96) }
        };
        AddChild(_shape);
    }

    public void Open()
    {
        if (!_closed) return;
        _closed = false;
        AudioManager.Instance?.Play("gate");
        var tw = CreateTween().SetTrans(Tween.TransitionType.Sine);
        tw.TweenProperty(this, "position:y", _baseY - 110f, 0.6f);
        tw.TweenCallback(Callable.From(() =>
        {
            if (_shape != null && IsInstanceValid(_shape))
                _shape.SetDeferred("disabled", true);
        }));
    }

    public void Close()
    {
        if (_closed) return;
        _closed = true;
        AudioManager.Instance?.Play("gate");
        var tw = CreateTween().SetTrans(Tween.TransitionType.Sine);
        tw.TweenProperty(this, "position:y", _baseY, 0.6f);
        tw.TweenCallback(Callable.From(() =>
        {
            if (_shape != null && IsInstanceValid(_shape))
                _shape.SetDeferred("disabled", false);
        }));
    }

    /// <summary>Instant state, no tween (for room setup).</summary>
    public void SetClosed(bool closed)
    {
        _closed = closed;
        Position = new Vector2(Position.X, closed ? _baseY : _baseY - 110f);
        if (_shape != null)
            _shape.SetDeferred("disabled", !closed);
    }

    public override void _Process(double delta)
    {
        // Defensive: if the boss was defeated elsewhere (Warden worker sets
        // Data.BossDefeated), make sure the gates never stay shut.
        if (_closed && Game.Instance?.Data?.BossDefeated == true)
            Open();
    }

    public override void _Draw()
    {
        float half = _widthPx / 2f;
        // Backing shadow.
        DrawRect(new Rect2(-half, -48, _widthPx, 96), Palette.ShadeBlack);
        // Vertical bars: 10px bar, 8px gap.
        for (float bx = -half; bx + 10f <= half + 0.01f; bx += 18f)
        {
            DrawRect(new Rect2(bx, -48, 10, 96), Palette.RockDark);
            DrawLine(new Vector2(bx + 1.5f, -48), new Vector2(bx + 1.5f, 48), Palette.PaleDim, 2f);
        }
        // Horizontal crossbars.
        foreach (float cy in new float[] { -30f, 0f, 30f })
            DrawRect(new Rect2(-half, cy - 2, _widthPx, 4), Palette.RockEdge);
        // Top/bottom rails.
        DrawRect(new Rect2(-half - 4, -54, _widthPx + 8, 8), Palette.RockEdge);
        DrawRect(new Rect2(-half - 4, 46, _widthPx + 8, 8), Palette.RockEdge);
    }
}
