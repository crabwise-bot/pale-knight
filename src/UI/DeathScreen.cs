using Godot;

namespace PaleKnight;

/// <summary>
/// Death overlay. The tree is NOT paused on death; any key/joy/mouse press
/// returns the little ghost to the last bench.
/// </summary>
public partial class DeathScreen : CanvasLayer
{
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = 30;

        var bg = new ColorRect { Color = new Color(0.01f, 0.015f, 0.03f, 0f) };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(bg);
        var fadeIn = CreateTween();
        fadeIn.TweenProperty(bg, "color:a", 1f, 1.2f);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 18);
        center.AddChild(vbox);

        var title = new Label { Text = "S H A T T E R E D", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 64);
        title.AddThemeColorOverride("font_color", Palette.Pale);
        vbox.AddChild(title);

        var sub = new Label
        {
            Text = "the little ghost returns to the last bench…",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        sub.AddThemeFontSizeOverride("font_size", 20);
        sub.AddThemeColorOverride("font_color", Palette.PaleDim);
        vbox.AddChild(sub);

        vbox.AddChild(new Control { CustomMinimumSize = new Vector2(1f, 32f) });

        var prompt = new Label { Text = "— press any key —", HorizontalAlignment = HorizontalAlignment.Center };
        prompt.AddThemeFontSizeOverride("font_size", 24);
        prompt.AddThemeColorOverride("font_color", Palette.Soul);
        vbox.AddChild(prompt);

        var blink = CreateTween().SetLoops();
        blink.TweenProperty(prompt, "modulate:a", 0.15f, 0.9f);
        blink.TweenProperty(prompt, "modulate:a", 1f, 0.9f);
    }

    public override void _Input(InputEvent ev)
    {
        bool pressed = (ev is InputEventKey key && key.Pressed && !key.Echo)
            || (ev is InputEventJoypadButton joy && joy.Pressed)
            || (ev is InputEventMouseButton mouse && mouse.Pressed);
        if (!pressed)
            return;
        GetViewport().SetInputAsHandled();
        Game.Instance.Respawn();
    }
}
