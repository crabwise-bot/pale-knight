using System;
using System.Collections.Generic;
using Godot;

namespace PaleKnight;

/// <summary>
/// Global game state + save/load + input map + camera shake + hitstop.
/// Autoload named "Game". Owns all state transitions.
/// </summary>
public partial class Game : Node
{
    public static Game Instance { get; private set; } = null!;

    public enum GameState { Title, Playing, Paused, Dead, Transition }

    public GameState State { get; private set; } = GameState.Title;
    public SaveData Data { get; private set; } = new SaveData();

    /// <summary>True while the player's Shade is loose (soul meter capped at 66).</summary>
    public bool ShadeActive => Data.ShadeActive;

    public Player? Player { get; set; }
    public Hud? Hud { get; set; }
    public Main? Main { get; set; }
    public Camera2D? Camera { get; set; }

    [Signal] public delegate void GeoChangedEventHandler(int geo);
    [Signal] public delegate void HealthChangedEventHandler(int hp, int maxHp);
    [Signal] public delegate void SoulChangedEventHandler(int soul);

    public const string SavePath = "user://pale_knight_save.json";
    private const string SaveFileName = "pale_knight_save.json";

    private float _shakeStrength;
    private float _shakeT;
    private float _shakeDuration;
    private int _hitstopCount;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        // Stay active while the tree is paused so pause toggle + camera shake still work.
        ProcessMode = ProcessModeEnum.Always;
        EnsureInput();
    }

    public override void _Process(double delta)
    {
        if (State == GameState.Playing)
            Data.PlayTime += (float)delta;
        UpdateCameraShake((float)delta);
    }

    public override void _Input(InputEvent ev)
    {
        if (ev.IsActionPressed("pause"))
        {
            if (State == GameState.Playing)
                PauseGame();
            else if (State == GameState.Paused)
                ResumeGame();
        }
    }

    // ---------------------------------------------------------------- input

    private void EnsureInput()
    {
        var keyboard = new Dictionary<string, Key[]>
        {
            ["move_left"] = new[] { Key.A, Key.Left },
            ["move_right"] = new[] { Key.D, Key.Right },
            ["move_up"] = new[] { Key.W, Key.Up },
            ["move_down"] = new[] { Key.S, Key.Down },
            ["jump"] = new[] { Key.Space },
            ["dash"] = new[] { Key.Shift },
            ["attack"] = new[] { Key.J },
            ["cast"] = new[] { Key.K },
            ["interact"] = new[] { Key.E },
            ["pause"] = new[] { Key.Escape },
        };
        var gamepad = new Dictionary<string, JoyButton[]>
        {
            ["jump"] = new[] { JoyButton.A },
            ["dash"] = new[] { JoyButton.B },
            ["attack"] = new[] { JoyButton.X },
            ["cast"] = new[] { JoyButton.Y },
            ["pause"] = new[] { JoyButton.Start },
            ["move_left"] = new[] { JoyButton.DpadLeft },
            ["move_right"] = new[] { JoyButton.DpadRight },
            ["move_up"] = new[] { JoyButton.DpadUp },
            ["move_down"] = new[] { JoyButton.DpadDown },
        };

        foreach (var kv in keyboard)
        {
            if (InputMap.HasAction(kv.Key))
                continue;
            InputMap.AddAction(kv.Key);
            foreach (Key k in kv.Value)
                InputMap.ActionAddEvent(kv.Key, new InputEventKey { PhysicalKeycode = k });
        }

        foreach (var kv in gamepad)
        {
            if (!InputMap.HasAction(kv.Key))
                InputMap.AddAction(kv.Key);
            foreach (JoyButton b in kv.Value)
                InputMap.ActionAddEvent(kv.Key, new InputEventJoypadButton { ButtonIndex = b });
        }
    }

    // ---------------------------------------------------------------- save

    public bool HasSave() => FileAccess.FileExists(SavePath);

    public void NewGame()
    {
        Data = new SaveData();
        SaveGame();
        Main?.StartPlay("grotto", "start");
        State = GameState.Playing;
    }

    public bool ContinueGame()
    {
        if (!HasSave())
            return false;

        try
        {
            string json = FileAccess.GetFileAsString(SavePath);
            var dict = Json.ParseString(json).AsGodotDictionary();
            Data = new SaveData
            {
                RoomId = GetStr(dict, "room_id", "grotto"),
                DoorTag = GetStr(dict, "door_tag", "start"),
                BenchRoom = GetStr(dict, "bench_room", "grotto"),
                BenchDoor = GetStr(dict, "bench_door", "start"),
                Health = GetInt(dict, "health", 5),
                MaxHealth = GetInt(dict, "max_health", 5),
                Soul = GetInt(dict, "soul", 0),
                Geo = GetInt(dict, "geo", 0),
                BossDefeated = GetBool(dict, "boss_defeated", false),
                PlayTime = GetFloat(dict, "play_time", 0f),
                ShadeActive = GetBool(dict, "shade_active", false),
                ShadeRoom = GetStr(dict, "shade_room", "grotto"),
                ShadePos = new Vector2(
                    GetFloat(dict, "shade_pos_x", 0f),
                    GetFloat(dict, "shade_pos_y", 0f)),
                ShadeGeo = GetInt(dict, "shade_geo", 0),
            };
        }
        catch (Exception e)
        {
            GD.PushWarning($"Game.ContinueGame: failed to parse save: {e.Message}");
            return false;
        }

        Main?.StartPlay(Data.RoomId, Data.DoorTag);
        State = GameState.Playing;
        EmitHealth(Data.Health, Data.MaxHealth);
        EmitSoul(Data.Soul);
        EmitSignal(SignalName.GeoChanged, Data.Geo);
        return true;
    }

    public void SaveGame()
    {
        try
        {
            var dict = new Godot.Collections.Dictionary
            {
                ["room_id"] = Data.RoomId,
                ["door_tag"] = Data.DoorTag,
                ["bench_room"] = Data.BenchRoom,
                ["bench_door"] = Data.BenchDoor,
                ["health"] = Data.Health,
                ["max_health"] = Data.MaxHealth,
                ["soul"] = Data.Soul,
                ["geo"] = Data.Geo,
                ["boss_defeated"] = Data.BossDefeated,
                ["play_time"] = Data.PlayTime,
                ["shade_active"] = Data.ShadeActive,
                ["shade_room"] = Data.ShadeRoom,
                ["shade_pos_x"] = Data.ShadePos.X,
                ["shade_pos_y"] = Data.ShadePos.Y,
                ["shade_geo"] = Data.ShadeGeo,
            };
            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PushWarning("Game.SaveGame: could not open save file for writing.");
                return;
            }
            file.StoreString(Json.Stringify(dict));
        }
        catch (Exception e)
        {
            GD.PushWarning($"Game.SaveGame failed: {e.Message}");
        }
    }

    public void DeleteSave()
    {
        try
        {
            using var dir = DirAccess.Open("user://");
            if (dir != null && dir.FileExists(SaveFileName))
                dir.Remove(SaveFileName);
        }
        catch (Exception e)
        {
            GD.PushWarning($"Game.DeleteSave failed: {e.Message}");
        }
    }

    private static string GetStr(Godot.Collections.Dictionary d, string key, string fallback) =>
        d.TryGetValue(key, out Variant v) && v.VariantType == Variant.Type.String ? v.AsString() : fallback;

    private static int GetInt(Godot.Collections.Dictionary d, string key, int fallback) =>
        d.TryGetValue(key, out Variant v) && (v.VariantType == Variant.Type.Int || v.VariantType == Variant.Type.Float)
            ? v.AsInt32() : fallback;

    private static float GetFloat(Godot.Collections.Dictionary d, string key, float fallback) =>
        d.TryGetValue(key, out Variant v) && (v.VariantType == Variant.Type.Int || v.VariantType == Variant.Type.Float)
            ? v.AsSingle() : fallback;

    private static bool GetBool(Godot.Collections.Dictionary d, string key, bool fallback) =>
        d.TryGetValue(key, out Variant v) && v.VariantType == Variant.Type.Bool ? v.AsBool() : fallback;

    // ---------------------------------------------------------------- flow

    public async void RequestRoomChange(string roomId, string doorTag)
    {
        if (State != GameState.Playing)
            return;
        State = GameState.Transition;
        if (Hud != null)
            await Hud.FadeToBlack(0.35f);
        Main?.BuildWorld(roomId, doorTag);
        Data.RoomId = roomId;
        Data.DoorTag = doorTag;
        if (Hud != null)
            await Hud.FadeFromBlack(0.35f);
        State = GameState.Playing;
    }

    private Vector2 _pendingShadePos = Vector2.Zero;
    private string _pendingShadeRoom = "grotto";
    private int _pendingShadeGeo = 0;

    public async void OnPlayerDied()
    {
        if (State != GameState.Playing)
            return;
        State = GameState.Dead;
        // Capture death state for the Shade BEFORE the world is rebuilt.
        _pendingShadePos = Player != null ? Player.GlobalPosition : Vector2.Zero;
        _pendingShadeRoom = Data.RoomId;
        _pendingShadeGeo = Data.Geo;
        Hitstop(0.25f, 0.15f);
        await ToSignal(GetTree().CreateTimer(0.9f), SceneTreeTimer.SignalName.Timeout);
        Main?.ShowDeath();
    }

    public void Respawn()
    {
        // The old Shade (if any) is freed by the world rebuild below; its geo
        // is lost forever — the new Shade holds only this run's geo.
        Data.ShadeActive = true;
        Data.ShadeRoom = _pendingShadeRoom;
        Data.ShadePos = _pendingShadePos;
        Data.ShadeGeo = _pendingShadeGeo;
        Data.Geo = 0;

        Data.RoomId = Data.BenchRoom;
        Data.DoorTag = Data.BenchDoor;
        Data.Health = Data.MaxHealth;
        Data.Soul = 0;
        Main?.BuildWorld(Data.RoomId, Data.DoorTag);
        Main?.HideDeath();
        State = GameState.Playing;
        // Boss resets naturally: the arena room is rebuilt fresh. Do NOT save here.
        EmitHealth(Data.MaxHealth, Data.MaxHealth);
        EmitSoul(0);
        SaveGame();
    }

    /// <summary>
    /// Spawn the Shade in a freshly built room, if it belongs here.
    /// Called by Main.BuildWorld after the player is placed.
    /// </summary>
    public void SpawnShadeIfNeeded(Node parent, string roomId)
    {
        if (!Data.ShadeActive || Data.ShadeRoom != roomId || parent == null)
            return;
        if (parent.GetTree().GetNodesInGroup("shade").Count > 0)
            return;
        var shade = new Shade();
        parent.AddChild(shade);
        shade.GlobalPosition = Data.ShadePos;
        shade.HeldGeo = Data.ShadeGeo;
    }

    /// <summary>Called by Shade on death: release the held geo, restore the soul meter.</summary>
    public void OnShadeKilled(Vector2 pos)
    {
        Data.ShadeActive = false;
        int geo = Data.ShadeGeo;
        Data.ShadeGeo = 0;
        var parent = Player != null && IsInstanceValid(Player) ? Player.GetParent() : null;
        if (parent != null && geo > 0)
            GeoPickup.Spawn(parent, pos, geo);
        SaveGame();
        Hud?.Toast("Shade defeated. Soul restored.");
    }

    public void PauseGame()
    {
        if (State != GameState.Playing)
            return;
        State = GameState.Paused;
        GetTree().Paused = true;
        Main?.ShowPause();
    }

    public void ResumeGame()
    {
        if (State != GameState.Paused)
            return;
        Main?.HidePause();
        GetTree().Paused = false;
        State = GameState.Playing;
    }

    public void QuitToTitle()
    {
        GetTree().Paused = false;
        State = GameState.Title;
        Main?.QuitToTitle();
    }

    // ---------------------------------------------------------------- stats

    public void AddGeo(int n)
    {
        Data.Geo = Math.Max(0, Data.Geo + n);
        EmitSignal(SignalName.GeoChanged, Data.Geo);
    }

    /// <summary>Called by Player when its health changes.</summary>
    public void EmitHealth(int hp, int maxHp) => EmitSignal(SignalName.HealthChanged, hp, maxHp);

    /// <summary>Called by Player when its soul changes.</summary>
    public void EmitSoul(int soul) => EmitSignal(SignalName.SoulChanged, soul);

    /// <summary>Pull current values from the live Player and broadcast them.</summary>
    public void NotifyHealth()
    {
        if (Player != null)
            EmitHealth(Player.Health, Player.MaxHealth);
    }

    /// <summary>Pull current values from the live Player and broadcast them.</summary>
    public void NotifySoul()
    {
        if (Player != null)
            EmitSoul(Player.Soul);
    }

    public void OnBossDefeated()
    {
        Data.BossDefeated = true;
        SaveGame();
        Hud?.Toast("The Warden has fallen. The way down is open.");
    }

    // ---------------------------------------------------------------- camera

    public void ShakeCamera(float strength, float seconds)
    {
        if (seconds <= 0f || strength <= 0f)
            return;
        _shakeStrength = Math.Max(_shakeStrength, strength);
        _shakeT = Math.Max(_shakeT, seconds);
        _shakeDuration = Math.Max(_shakeDuration, seconds);
    }

    private void UpdateCameraShake(float delta)
    {
        if (Camera == null)
            return;
        if (_shakeT <= 0f)
        {
            if (_shakeStrength > 0f)
            {
                Camera.Offset = Vector2.Zero;
                _shakeStrength = 0f;
            }
            return;
        }
        _shakeT -= delta;
        float k = _shakeDuration > 0f ? Math.Max(0f, _shakeT / _shakeDuration) : 0f;
        Vector2 dir = Vector2.Right.Rotated(GD.Randf() * Mathf.Tau);
        Camera.Offset = dir * _shakeStrength * k;
    }

    // ---------------------------------------------------------------- hitstop

    /// <summary>Full freeze for dur seconds.</summary>
    public void Hitstop(float dur) => Hitstop(dur, 0f);

    public async void Hitstop(float dur, float scale)
    {
        if (dur <= 0f)
            return;
        _hitstopCount++;
        Engine.TimeScale = scale;
        await ToSignal(GetTree().CreateTimer(dur, true, false, true), SceneTreeTimer.SignalName.Timeout);
        _hitstopCount--;
        if (_hitstopCount <= 0)
        {
            _hitstopCount = 0;
            Engine.TimeScale = 1f;
        }
    }
}
