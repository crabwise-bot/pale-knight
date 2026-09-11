using Godot;

namespace PaleKnight;

/// <summary>
/// Frozen knight silhouette left behind while dashing. Fades out over 0.35s.
/// </summary>
public partial class Afterimage : Node2D
{
    private KnightVisual.KnightPose _pose;
    private Color _tint = new(0.45f, 1f, 0.9f, 0.5f);

    public void Setup(KnightVisual.KnightPose pose, Color tint)
    {
        _pose = pose;
        _pose.Dashing = true;
        _tint = tint;
    }

    public override void _Ready()
    {
        Modulate = _tint;
        QueueRedraw();
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 0f, 0.35f);
        tween.TweenCallback(Callable.From(QueueFree));
    }

    public override void _Draw()
    {
        KnightVisual.DrawKnight(this, _pose);
    }
}
