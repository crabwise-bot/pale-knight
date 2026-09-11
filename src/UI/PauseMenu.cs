using Godot;

namespace PaleKnight;

/// <summary>Pause overlay. ProcessMode Always so it works while the tree is paused.</summary>
public partial class PauseMenu : CanvasLayer
{
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = 20;

        var dim = new ColorRect { Color = new Color(0.02f, 0.03f, 0.05f, 0.72f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 14);
        center.AddChild(vbox);

        var title = new Label { Text = "PAUSED", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 56);
        title.AddThemeColorOverride("font_color", Palette.Pale);
        vbox.AddChild(title);

        vbox.AddChild(new Control { CustomMinimumSize = new Vector2(1f, 24f) });

        Button resume = MakeButton("Resume");
        Button quit = MakeButton("Quit to Title");
        vbox.AddChild(resume);
        vbox.AddChild(quit);

        resume.FocusNeighborBottom = quit.GetPath();
        quit.FocusNeighborTop = resume.GetPath();
        resume.FocusNeighborTop = quit.GetPath();
        quit.FocusNeighborBottom = resume.GetPath();

        resume.Pressed += () => { Click(); Game.Instance.ResumeGame(); };
        quit.Pressed += () => { Click(); Game.Instance.QuitToTitle(); };

        resume.GrabFocus();
    }

    public override void _Input(InputEvent ev)
    {
        if (ev.IsActionPressed("pause"))
        {
            GetViewport().SetInputAsHandled();
            Game.Instance.ResumeGame();
        }
    }

    private static Button MakeButton(string text)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(260f, 56f),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            FocusMode = Control.FocusModeEnum.All,
        };
        button.AddThemeFontSizeOverride("font_size", 28);
        button.AddThemeColorOverride("font_color", Palette.Pale);
        button.AddThemeColorOverride("font_hover_color", Palette.Soul);

        var normal = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.07f, 0.11f, 0.85f),
            BorderColor = new Color(0.05f, 0.07f, 0.11f, 0.85f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
        };
        var hover = (StyleBoxFlat)normal.Duplicate();
        hover.BorderColor = Palette.Soul;

        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", normal);
        button.AddThemeStyleboxOverride("focus", hover);
        return button;
    }

    private static void Click() => AudioManager.Instance?.Play("ui_click");
}
