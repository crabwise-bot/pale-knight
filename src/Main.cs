using Godot;

namespace PaleKnight;

/// <summary>
/// Scene root (attached to scenes/Main.tscn). Owns the world tree, camera,
/// parallax/fog/dust ambience, and top-level UI swapping.
/// Game owns state; Main only builds and swaps.
/// </summary>
public partial class Main : Node
{
    private CanvasLayer? _uiLayer;
    private Node2D? _worldRoot;
    private Camera2D? _camera;

    private TitleScreen? _title;
    private Hud? _hud;
    private PauseMenu? _pauseMenu;
    private DeathScreen? _deathScreen;

    public override void _Ready()
    {
        Game.Instance.Main = this;

        _uiLayer = new CanvasLayer { Name = "UILayer", Layer = 10 };
        AddChild(_uiLayer);

        _worldRoot = new Node2D { Name = "WorldRoot" };
        AddChild(_worldRoot);

        _camera = new Camera2D
        {
            Name = "Camera",
            PositionSmoothingEnabled = true,
            PositionSmoothingSpeed = 6f,
        };
        AddChild(_camera);
        _camera.Enabled = true;
        Game.Instance.Camera = _camera;

        AddChild(new CanvasModulate { Color = new Color(0.42f, 0.46f, 0.58f) });

        ShowTitle();
    }

    // ---------------------------------------------------------------- screens

    public void ShowTitle()
    {
        ClearWorld();
        HidePause();
        HideDeath();
        if (_hud != null)
        {
            _hud.QueueFree();
            _hud = null;
            Game.Instance.Hud = null;
        }
        if (_title != null)
            return;
        _title = new TitleScreen();
        _uiLayer?.AddChild(_title);
        AudioManager.Instance?.PlayMusic("ambient_drone");
    }

    public void QuitToTitle()
    {
        ShowTitle();
    }

    public void StartPlay(string roomId, string doorTag)
    {
        if (_title != null)
        {
            _title.QueueFree();
            _title = null;
        }
        BuildWorld(roomId, doorTag);
        if (_hud == null && _uiLayer != null)
        {
            _hud = new Hud();
            _uiLayer.AddChild(_hud);
            Game.Instance.Hud = _hud;
        }
    }

    public void ShowPause()
    {
        if (_pauseMenu == null && _uiLayer != null)
        {
            _pauseMenu = new PauseMenu();
            _uiLayer.AddChild(_pauseMenu);
        }
    }

    public void HidePause()
    {
        if (_pauseMenu != null)
        {
            _pauseMenu.QueueFree();
            _pauseMenu = null;
        }
    }

    public void ShowDeath()
    {
        if (_deathScreen == null && _uiLayer != null)
        {
            _deathScreen = new DeathScreen();
            _uiLayer.AddChild(_deathScreen);
        }
    }

    public void HideDeath()
    {
        if (_deathScreen != null)
        {
            _deathScreen.QueueFree();
            _deathScreen = null;
        }
    }

    // ---------------------------------------------------------------- world

    private void ClearWorld()
    {
        if (_worldRoot == null)
            return;
        foreach (Node child in _worldRoot.GetChildren())
            child.QueueFree();
    }

    public void BuildWorld(string roomId, string doorTag)
    {
        if (_worldRoot == null)
            return;

        ClearWorld();
        var world = new Node2D { Name = "World" };
        _worldRoot.AddChild(world);

        RoomBuild build = RoomBuilder.Build(roomId);
        world.AddChild(build.Root);

        var player = new Player();
        build.Root.AddChild(player);
        Vector2 spawn = build.DoorSpawns.TryGetValue(doorTag, out Vector2 s)
            ? s
            : build.DoorSpawns.TryGetValue("start", out Vector2 d) ? d : Vector2.Zero;
        player.Position = spawn;
        player.LastSafeGround = spawn;
        Game.Instance.Player = player;
        Game.Instance.NotifyHealth();
        Game.Instance.NotifySoul();
        Game.Instance.SpawnShadeIfNeeded(build.Root, roomId);

        if (_camera != null)
        {
            Rect2 b = build.PlayBounds;
            _camera.LimitLeft = (int)b.Position.X;
            _camera.LimitTop = (int)b.Position.Y;
            _camera.LimitRight = (int)b.End.X;
            _camera.LimitBottom = (int)b.End.Y;
            _camera.Position = spawn;
            _camera.ResetSmoothing();
        }

        AddParallax(world, "res://assets/textures/bg_far.png", 0.25f, build.PlayBounds);
        AddParallax(world, "res://assets/textures/bg_mid.png", 0.55f, build.PlayBounds);
        AddFog(world, build.PlayBounds);
        AddDust(world, build.PlayBounds);

        bool bossPending = roomId == "warden_arena" && !Game.Instance.Data.BossDefeated;
        if (!bossPending)
            AudioManager.Instance?.PlayMusic("ambient_drone");
    }

    private void AddParallax(Node parent, string texPath, float scale, Rect2 bounds)
    {
        var tex = GD.Load<Texture2D>(texPath);
        if (tex == null)
        {
            GD.PushWarning($"Main: missing parallax texture '{texPath}'.");
            return;
        }
        var bg = new ParallaxBackground { Layer = -10, ScrollIgnoreCameraZoom = true };
        var layer = new ParallaxLayer
        {
            MotionScale = new Vector2(scale, scale),
            MotionMirroring = new Vector2(tex.GetWidth(), 0f),
        };
        layer.AddChild(new Sprite2D
        {
            Texture = tex,
            Centered = true,
            Position = bounds.GetCenter(),
        });
        bg.AddChild(layer);
        parent.AddChild(bg);
    }

    private void AddFog(Node parent, Rect2 bounds)
    {
        var tex = GD.Load<Texture2D>("res://assets/textures/fog_soft.png");
        if (tex == null)
        {
            GD.PushWarning("Main: missing fog texture 'res://assets/textures/fog_soft.png'.");
            return;
        }
        Vector2 center = bounds.GetCenter();
        for (int i = 0; i < 3; i++)
        {
            var sprite = new Sprite2D
            {
                Texture = tex,
                Modulate = new Color(1f, 1f, 1f, 0.16f),
                Position = center + new Vector2((i - 1) * bounds.Size.X * 0.35f, (i - 1) * 24f),
                Scale = new Vector2(3f, 2f),
            };
            parent.AddChild(sprite);
            var tween = sprite.CreateTween().SetLoops();
            float drift = 120f + i * 40f;
            float dur = 14f + i * 4f;
            tween.TweenProperty(sprite, "position:x", sprite.Position.X + drift, dur)
                .SetTrans(Tween.TransitionType.Sine);
            tween.TweenProperty(sprite, "position:x", sprite.Position.X - drift, dur)
                .SetTrans(Tween.TransitionType.Sine);
        }
    }

    private void AddDust(Node parent, Rect2 bounds)
    {
        var particles = new GpuParticles2D
        {
            Name = "Dust",
            Amount = 48,
            Lifetime = 6f,
            Position = bounds.GetCenter(),
        };
        var dustTex = GD.Load<Texture2D>("res://assets/textures/dust.png");
        if (dustTex != null)
            particles.Texture = dustTex;
        particles.ProcessMaterial = new ParticleProcessMaterial
        {
            Gravity = Vector3.Zero,
            Direction = new Vector3(0f, -1f, 0f),
            Spread = 180f,
            InitialVelocityMin = 6f,
            InitialVelocityMax = 18f,
            ScaleMin = 2f,
            ScaleMax = 4f,
            Color = new Color(0.91f, 0.93f, 0.96f, 0.25f),
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(bounds.Size.X / 2f, bounds.Size.Y / 2f, 1f),
        };
        parent.AddChild(particles);
    }
}
