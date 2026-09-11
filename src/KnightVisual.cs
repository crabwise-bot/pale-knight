using Godot;
using System.Collections.Generic;

namespace PaleKnight;

/// <summary>
/// Procedural Hollow-Knight-style knight, drawn entirely in code.
/// Player owns one; Afterimage snapshots poses for dash ghosts.
/// </summary>
public partial class KnightVisual : Node2D
{
    public struct KnightPose
    {
        public int Facing;      // -1 or 1
        public float RunPhase;  // advanced internally while running
        public bool Grounded;
        public bool Dashing;
        public int WallDir;     // -1/0/1, wall side while airborne
        public bool Healing;
        public float Crouch;    // 0..1
    }

    private KnightPose _pose = new KnightPose { Facing = 1, Grounded = true };
    private float _squash;   // 0..1, set by OnLand
    private float _stretch;  // 0..1, set by OnJump
    private float _runSpeed; // px/s, set by Player each frame
    private bool _healActive;
    private float _healTime;

    /// <summary>How fast the knight is moving horizontally; drives run cycle + cloak sway.</summary>
    public float RunSpeed { get; set; }

    public void SetPose(KnightPose pose)
    {
        // Preserve the internally-animated run phase across SetPose calls.
        float phase = _pose.RunPhase;
        _pose = pose;
        _pose.RunPhase = phase;
        if (_healActive)
            _pose.Healing = true;
    }

    public void OnLand() => _squash = 1f;
    public void OnJump() => _stretch = 1f;

    public void SetHealing(bool healing)
    {
        _healActive = healing;
        if (!healing)
            _healTime = 0f;
    }

    public override void _Process(double delta)
    {
        float d = (float)delta;
        _runSpeed = RunSpeed;

        if (_runSpeed > 1f)
        {
            _pose.RunPhase += d * _runSpeed * 0.045f;
        }
        else
        {
            // Relax the run cycle back to a neutral stance when idle.
            float neutral = Mathf.Round(_pose.RunPhase / Mathf.Tau) * Mathf.Tau;
            _pose.RunPhase = Mathf.LerpAngle(_pose.RunPhase, neutral, 1f - Mathf.Pow(0.002f, d));
        }

        _squash = Mathf.Max(0f, _squash - d / 0.18f);
        _stretch = Mathf.Max(0f, _stretch - d / 0.16f);
        if (_healActive)
            _healTime += d;

        QueueRedraw();
    }

    public override void _Draw()
    {
        float sx = 1f + _squash * 0.22f - _stretch * 0.10f;
        float sy = 1f - _squash * 0.26f + _stretch * 0.20f - _pose.Crouch * 0.12f;
        float lean = (_pose.WallDir != 0 && !_pose.Grounded) ? _pose.WallDir * 0.12f : 0f;
        DrawSetTransform(new Vector2(0f, _pose.Crouch * 4f), lean, new Vector2(sx, sy));
        DrawKnight(this, _pose);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);

        if (_healActive)
        {
            for (int i = 0; i < 10; i++)
            {
                float a = _healTime * 2.6f + i * Mathf.Tau / 10f;
                Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 17f + new Vector2(0f, -8f);
                DrawCircle(p, 2.2f, new Color(Palette.Soul, 0.75f));
            }
        }
    }

    /// <summary>Draws the knight (~40px tall, origin at feet center) onto any CanvasItem.</summary>
    public static void DrawKnight(CanvasItem ci, KnightPose pose)
    {
        if (ci == null)
            return;
        int f = pose.Facing >= 0 ? 1 : -1;
        float sway = Mathf.Sin(pose.RunPhase) * 2.0f;
        float kick = Mathf.Sin(pose.RunPhase) * 3.0f;

        // Feet (drawn first, cloak overlaps them).
        ci.DrawCircle(new Vector2((2f + kick) * f, 13.5f), 2.6f, Palette.ShadeBlack);
        ci.DrawCircle(new Vector2((-2f - kick) * f, 13.5f), 2.6f, Palette.ShadeBlack);

        // Cloak: dark rounded silhouette, hem sways with the run cycle.
        var cloak = new Vector2[]
        {
            new Vector2(-7f * f, -9f),
            new Vector2(7f * f, -8f),
            new Vector2(9f * f, 2f),
            new Vector2((11f + sway) * f, 13f),
            new Vector2(5f * f, 15f),
            new Vector2(-5f * f, 15f),
            new Vector2((-10f + sway * 0.6f) * f, 13f),
            new Vector2(-8f * f, 2f),
        };
        ci.DrawColoredPolygon(cloak, Palette.ShadeBlack, System.Array.Empty<Vector2>(), null);
        var outline = new List<Vector2>(cloak) { cloak[0] };
        ci.DrawPolyline(outline.ToArray(), new Color(Palette.PaleDim, 0.22f), 1.5f, true);

        // Mask: white oval.
        Ellipse(ci, new Vector2(1f * f, -12f), 7f, 8.5f, Palette.Pale, f);

        // Eyes: two black ovals.
        Ellipse(ci, new Vector2(4.5f * f, -13f), 2.2f, 3.4f, Palette.ShadeBlack, f);
        Ellipse(ci, new Vector2(-2.5f * f, -13f), 2.2f, 3.4f, Palette.ShadeBlack, f);

        // Horns.
        ci.DrawColoredPolygon(Tri(new Vector2(-4f, -19f), new Vector2(-10f, -30f), new Vector2(-1f, -21f), f), Palette.Pale);
        ci.DrawColoredPolygon(Tri(new Vector2(5f, -19f), new Vector2(11f, -29f), new Vector2(7f, -20f), f), Palette.Pale);

        // Nail: held low at the side, thrust forward while dashing.
        if (pose.Dashing)
            ci.DrawLine(new Vector2(6f * f, -10f), new Vector2(20f * f, -10f), Palette.Pale, 3f, true);
        else
            ci.DrawLine(new Vector2(7f * f, -2f), new Vector2(14f * f, 8f), Palette.PaleDim, 2.5f, true);
    }

    private static void Ellipse(CanvasItem ci, Vector2 center, float rx, float ry, Color col, int facing)
    {
        var pts = new List<Vector2>();
        for (int i = 0; i < 20; i++)
        {
            float a = i / 20f * Mathf.Tau;
            pts.Add(new Vector2(center.X + Mathf.Cos(a) * rx * facing, center.Y + Mathf.Sin(a) * ry));
        }
        ci.DrawColoredPolygon(pts.ToArray(), col);
    }

    private static Vector2[] Tri(Vector2 a, Vector2 b, Vector2 c, int facing)
    {
        return new Vector2[]
        {
            new Vector2(a.X * facing, a.Y),
            new Vector2(b.X * facing, b.Y),
            new Vector2(c.X * facing, c.Y),
        };
    }
}
