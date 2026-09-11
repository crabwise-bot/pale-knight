using Godot;

namespace PaleKnight;

/// <summary>
/// Title screen: moody backdrop with slow drift, fog, rising motes,
/// glowing title, and New Game / Continue / Quit.
/// </summary>
public partial class TitleScreen : Control
{
    private TextureRect? _bg;
    private Label? _title;
    private float _time;

    public override void _Ready()
    {
        SetAnchorsPreset(Control.LayoutPreset.FullRect);

        // Backdrop: let the art stay dark and moody (no gray wash).
        _bg = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/textures/title_backdrop.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
        };
        _bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _bg.PivotOffset = new Vector2(640f, 360f);
        AddChild(_bg);
        // Slow Ken Burns drift.
        var drift = CreateTween().SetLoops();
        drift.TweenProperty(_bg, "scale", new Vector2(1.06f, 1.06f), 14f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        drift.TweenProperty(_bg, "scale", Vector2.One, 14f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

        AddFog();
        AddMotes();

        // Dark edge bars top and bottom to draw the eye to the center.
        var top = new ColorRect { Color = new Color(0f, 0f, 0f, 0.45f) };
        top.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        top.CustomMinimumSize = new Vector2(1f, 90f);
        top.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(top);
        var bottom = new ColorRect { Color = new Color(0f, 0f, 0f, 0.45f) };
        bottom.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        bottom.CustomMinimumSize = new Vector2(1f, 90f);
        bottom.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(bottom);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 14);
        center.AddChild(vbox);

        // Title with a soft soul-colored glow echo behind it.
        var titleStack = new Control { CustomMinimumSize = new Vector2(700f, 110f) };
        titleStack.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        vbox.AddChild(titleStack);

        var glow = new Label
        {
            Text = "PALE KNIGHT",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        glow.AddThemeFontSizeOverride("font_size", 86);
        glow.AddThemeColorOverride("font_color", new Color(Palette.Soul, 0.35f));
        glow.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        glow.Position += new Vector2(0f, 3f);
        titleStack.AddChild(glow);

        _title = new Label
        {
            Text = "PALE KNIGHT",
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(1f, 1f, 1f, 0f),
        };
        _title.AddThemeFontSizeOverride("font_size", 84);
        _title.AddThemeColorOverride("font_color", Palette.Pale);
        _title.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.8f));
        _title.AddThemeConstantOverride("shadow_offset_x", 3);
        _title.AddThemeConstantOverride("shadow_offset_y", 3);
        _title.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        titleStack.AddChild(_title);
        // Fade the title in on load.
        var fade = CreateTween();
        fade.TweenProperty(_title, "modulate:a", 1f, 2.2f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

        var subtitle = new Label
        {
            Text = "a pale little metroidvania",
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(1f, 1f, 1f, 0f),
        };
        subtitle.AddThemeFontSizeOverride("font_size", 20);
        subtitle.AddThemeColorOverride("font_color", Palette.PaleDim);
        vbox.AddChild(subtitle);
        var fade2 = CreateTween();
        fade2.TweenInterval(0.8f);
        fade2.TweenProperty(subtitle, "modulate:a", 1f, 1.6f);

        vbox.AddChild(new Control { CustomMinimumSize = new Vector2(1f, 36f) });

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

        // Controls hint footer.
        var hint = new Label
        {
            Text = "move: A/D or arrows   jump: Space   slash: J   spell/heal: K   dash: Shift",
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(1f, 1f, 1f, 0f),
        };
        hint.AddThemeFontSizeOverride("font_size", 15);
        hint.AddThemeColorOverride("font_color", new Color(Palette.PaleDim, 0.8f));
        hint.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        hint.Position = new Vector2(0f, -34f);
        AddChild(hint);
        var fade3 = CreateTween();
        fade3.TweenInterval(1.4f);
        fade3.TweenProperty(hint, "modulate:a", 1f, 1.6f);

        newGame.GrabFocus();
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        // Gentle breathing glow on the title.
        if (_title != null && _title.Modulate.A > 0.9f)
        {
            float p = 0.5f + 0.5f * Mathf.Sin(_time * 1.4f);
            _title.Modulate = new Color(1f, 1f, 1f, 0.92f + 0.08f * p);
        }
    }

    private void AddFog()
    {
        var tex = GD.Load<Texture2D>("res://assets/textures/fog_soft.png");
        if (tex == null)
            return;
        for (int i = 0; i < 3; i++)
        {
            var sprite = new TextureRect
            {
                Texture = tex,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                Modulate = new Color(1f, 1f, 1f, 0.10f),
                CustomMinimumSize = new Vector2(900f, 420f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            sprite.Position = new Vector2(120f + i * 320f, 220f + (i % 2) * 160f);
            AddChild(sprite);
            var tween = CreateTween().SetLoops();
            float drift = 140f + i * 50f;
            float dur = 16f + i * 5f;
            tween.TweenProperty(sprite, "position:x", sprite.Position.X + drift, dur)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(sprite, "position:x", sprite.Position.X - drift, dur)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        }
    }

    private void AddMotes()
    {
        var tex = GD.Load<Texture2D>("res://assets/textures/dust.png");
        if (tex == null)
            return;
        var rng = new RandomNumberGenerator { Seed = 1234 };
        for (int i = 0; i < 26; i++)
        {
            var mote = new TextureRect
            {
                Texture = tex,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(10f, 10f),
                Modulate = new Color(0.9f, 0.93f, 1f, rng.RandfRange(0.15f, 0.4f)),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            mote.Position = new Vector2(rng.RandfRange(0f, 1280f), rng.RandfRange(0f, 720f));
            AddChild(mote);
            float rise = rng.RandfRange(60f, 160f);
            float dur = rng.RandfRange(7f, 14f);
            var tween = CreateTween().SetLoops();
            tween.TweenProperty(mote, "position:y", mote.Position.Y - rise, dur)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(mote, "position:y", mote.Position.Y, dur)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
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
