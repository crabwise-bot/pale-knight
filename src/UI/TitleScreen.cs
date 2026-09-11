using Godot;

namespace PaleKnight;

/// <summary>Title screen: backdrop, logo, New Game / Continue / Quit.</summary>
public partial class TitleScreen : Control
{
    public override void _Ready()
    {
        SetAnchorsPreset(Control.LayoutPreset.FullRect);

        var bg = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/textures/title_backdrop.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            Modulate = new Color(0.55f, 0.6f, 0.72f),
        };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 14);
        center.AddChild(vbox);

        var title = new Label { Text = "PALE KNIGHT", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 84);
        title.AddThemeColorOverride("font_color", Palette.Pale);
        vbox.AddChild(title);

        var subtitle = new Label
        {
            Text = "a pale little metroidvania",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        subtitle.AddThemeFontSizeOverride("font_size", 20);
        subtitle.AddThemeColorOverride("font_color", Palette.PaleDim);
        vbox.AddChild(subtitle);

        vbox.AddChild(new Control { CustomMinimumSize = new Vector2(1f, 40f) });

        Button newGame = MakeButton("New Game");
        Button cont = MakeButton("Continue");
        Button quit = MakeButton("Quit");
        vbox.AddChild(newGame);
        vbox.AddChild(cont);
        vbox.AddChild(quit);

        Button[] buttons = { newGame, cont, quit };
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].FocusNeighborTop = buttons[(i + buttons.Length - 1) % buttons.Length].GetPath();
            buttons[i].FocusNeighborBottom = buttons[(i + 1) % buttons.Length].GetPath();
        }

        newGame.Pressed += () => { Click(); Game.Instance.NewGame(); };
        cont.Pressed += () => { Click(); Game.Instance.ContinueGame(); };
        quit.Pressed += () => { Click(); GetTree().Quit(); };
        cont.Disabled = !Game.Instance.HasSave();

        newGame.GrabFocus();
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
        button.AddThemeColorOverride("font_pressed_color", Palette.Pale);
        button.AddThemeColorOverride("font_disabled_color", Palette.PaleDim);

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
            ContentMarginLeft = 16f,
            ContentMarginRight = 16f,
            ContentMarginTop = 8f,
            ContentMarginBottom = 8f,
        };
        var hover = (StyleBoxFlat)normal.Duplicate();
        hover.BorderColor = Palette.Soul;
        var pressed = (StyleBoxFlat)normal.Duplicate();
        pressed.BgColor = new Color(0.1f, 0.14f, 0.2f, 0.9f);
        pressed.BorderColor = Palette.Soul;

        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", pressed);
        button.AddThemeStyleboxOverride("focus", hover);
        button.AddThemeStyleboxOverride("disabled", normal);
        return button;
    }

    private static void Click() => AudioManager.Instance?.Play("ui_click");
}
