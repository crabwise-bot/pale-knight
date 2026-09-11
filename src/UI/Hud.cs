using System.Threading.Tasks;
using Godot;

namespace PaleKnight;

/// <summary>
/// In-game HUD: health masks, soul vessel, geo counter, toast messages,
/// and the black fade rect used for room transitions.
/// </summary>
public partial class Hud : CanvasLayer
{
    private MasksDisplay? _masks;
    private SoulVessel? _vessel;
    private Label? _geoLabel;
    private Label? _toast;
    private ColorRect? _fade;
    private Tween? _toastTween;

    // ------------------------------------------------------------- masks

    private partial class MasksDisplay : Control
    {
        public int Hp = 5;
        public int MaxHp = 5;

        public void Refresh(int hp, int maxHp)
        {
            Hp = hp;
            MaxHp = maxHp;
            CustomMinimumSize = new Vector2(maxHp * 40f + 34f, 48f);
            QueueRedraw();
        }

        public override void _Draw()
        {
            const float size = 15f;
            const float step = 40f;
            for (int i = 0; i < MaxHp; i++)
            {
                var c = new Vector2(step * i + size + 4f, size + 4f);
                var diamond = new Vector2[]
                {
                    c + new Vector2(0f, -size),
                    c + new Vector2(size * 0.72f, 0f),
                    c + new Vector2(0f, size),
                    c + new Vector2(-size * 0.72f, 0f),
                };
                if (i < Hp)
                {
                    DrawColoredPolygon(diamond, Palette.Pale, System.Array.Empty<Vector2>(), null);
                }
                else
                {
                    var outline = new Vector2[]
                        { diamond[0], diamond[1], diamond[2], diamond[3], diamond[0] };
                    DrawPolyline(outline, new Color(Palette.PaleDim, 0.45f), 2f, true);
                }
            }
        }
    }

    // ------------------------------------------------------------- soul

    private partial class SoulVessel : Control
    {
        private const int MaxSoul = 99;
        private const int Segment = 33;
        public int Soul;
        private int _lastSoul;
        private float _flashT;

        public void Refresh(int soul)
        {
            // Flash when crossing a castable threshold upward (HK meter flash).
            for (int t = Segment; t <= MaxSoul; t += Segment)
            {
                if (_lastSoul < t && soul >= t)
                {
                    _flashT = 0.7f;
                    break;
                }
            }
            _lastSoul = soul;
            Soul = soul;
            QueueRedraw();
        }

        public override void _Process(double delta)
        {
            if (_flashT > 0f)
            {
                _flashT -= (float)delta;
                QueueRedraw();
            }
        }

        public override void _Draw()
        {
            int cap = Game.Instance?.Player?.SoulCap ?? MaxSoul;
            var c = new Vector2(22f, 22f);
            const float r = 17f;
            var baseCol = new Color(Palette.Soul, 0.35f);
            DrawArc(c, r, 0f, Mathf.Tau, 48, baseCol, 4f, true);
            // Segment ticks at 33 / 66.
            for (int t = Segment; t < MaxSoul; t += Segment)
            {
                float a = -Mathf.Pi / 2f + ((float)t / MaxSoul) * Mathf.Tau;
                var p1 = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r - 4f);
                var p2 = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r + 4f);
                DrawLine(p1, p2, new Color(Palette.Soul, 0.6f), 2f, true);
            }
            float frac = Mathf.Clamp((float)Soul / Mathf.Max(1, cap), 0f, 1f);
            if (frac > 0.001f)
                DrawArc(c, r, -Mathf.Pi / 2f, -Mathf.Pi / 2f + frac * Mathf.Tau, 48, Palette.Soul, 4f, true);
            if (_flashT > 0f)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(_flashT * 25f);
                DrawArc(c, r + 4f, 0f, Mathf.Tau, 48,
                    new Color(Palette.Soul, 0.35f + 0.45f * pulse), 2f, true);
            }
        }
    }

    // ------------------------------------------------------------- build

    public override void _Ready()
    {
        Layer = 10;

        _masks = new MasksDisplay { Position = new Vector2(24f, 20f) };
        int hp = Game.Instance.Player?.Health ?? Game.Instance.Data.Health;
        int maxHp = Player.MaxHealth;
        _masks.Refresh(hp, maxHp);
        AddChild(_masks);

        _vessel = new SoulVessel
        {
            Position = new Vector2(24f, 68f),
            CustomMinimumSize = new Vector2(44f, 44f),
        };
        _vessel.Refresh(Game.Instance.Player?.Soul ?? Game.Instance.Data.Soul);
        AddChild(_vessel);

        _geoLabel = new Label();
        _geoLabel.AnchorLeft = 1f;
        _geoLabel.AnchorRight = 1f;
        _geoLabel.OffsetLeft = -260f;
        _geoLabel.OffsetRight = -24f;
        _geoLabel.OffsetTop = 20f;
        _geoLabel.OffsetBottom = 56f;
        _geoLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _geoLabel.AddThemeFontSizeOverride("font_size", 28);
        _geoLabel.AddThemeColorOverride("font_color", Palette.Geo);
        _geoLabel.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.8f));
        _geoLabel.AddThemeConstantOverride("shadow_offset_x", 2);
        _geoLabel.AddThemeConstantOverride("shadow_offset_y", 2);
        AddChild(_geoLabel);
        UpdateGeo(Game.Instance.Data.Geo);

        _toast = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _toast.AnchorLeft = 0.5f;
        _toast.AnchorRight = 0.5f;
        _toast.AnchorTop = 1f;
        _toast.AnchorBottom = 1f;
        _toast.OffsetLeft = -400f;
        _toast.OffsetRight = 400f;
        _toast.OffsetTop = -120f;
        _toast.OffsetBottom = -76f;
        _toast.AddThemeFontSizeOverride("font_size", 22);
        _toast.AddThemeColorOverride("font_color", Palette.Pale);
        _toast.Modulate = new Color(1f, 1f, 1f, 0f);
        _toast.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(_toast);

        _fade = new ColorRect { Color = new Color(0f, 0f, 0f, 1f) };
        _fade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _fade.MouseFilter = Control.MouseFilterEnum.Ignore;
        _fade.Modulate = new Color(1f, 1f, 1f, 0f);
        _fade.Visible = false;
        AddChild(_fade);

        Game.Instance.HealthChanged += OnHealthChanged;
        Game.Instance.SoulChanged += OnSoulChanged;
        Game.Instance.GeoChanged += UpdateGeo;
    }

    // ------------------------------------------------------------- state

    private void OnHealthChanged(int hp, int maxHp) => _masks?.Refresh(hp, maxHp);
    private void OnSoulChanged(int soul) => _vessel?.Refresh(soul);

    private void UpdateGeo(int geo)
    {
        if (_geoLabel != null)
            _geoLabel.Text = $"◆ {geo}";
    }

    public void Toast(string text)
    {
        if (_toast == null)
            return;
        _toast.Text = text;
        _toastTween?.Kill();
        _toast.Modulate = new Color(1f, 1f, 1f, 0f);
        _toastTween = CreateTween();
        _toastTween.TweenProperty(_toast, "modulate:a", 1f, 0.25f);
        _toastTween.TweenInterval(2.2f);
        _toastTween.TweenProperty(_toast, "modulate:a", 0f, 0.6f);
    }

    public async Task FadeToBlack(float t)
    {
        if (_fade == null)
            return;
        _fade.Visible = true;
        var tween = CreateTween();
        tween.TweenProperty(_fade, "modulate:a", 1f, t);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    public async Task FadeFromBlack(float t)
    {
        if (_fade == null)
            return;
        _fade.Visible = true;
        var tween = CreateTween();
        tween.TweenProperty(_fade, "modulate:a", 0f, t);
        await ToSignal(tween, Tween.SignalName.Finished);
        _fade.Visible = false;
    }
}
