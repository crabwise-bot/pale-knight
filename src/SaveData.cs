using Godot;

namespace PaleKnight;

/// <summary>Serializable run state. Game.cs owns save/load of this.</summary>
public class SaveData
{
    public string RoomId = "grotto";
    public string DoorTag = "start";
    public string BenchRoom = "grotto";
    public string BenchDoor = "start";
    public int Health = 5;
    public int MaxHealth = 5;
    public int Soul = 0;
    public int Geo = 0;
    public bool BossDefeated = false;
    public float PlayTime = 0f;

    // Shade (death penalty) state
    public bool ShadeActive = false;
    public string ShadeRoom = "grotto";
    public Vector2 ShadePos = Vector2.Zero;
    public int ShadeGeo = 0;

    public SaveData Clone()
    {
        return (SaveData)MemberwiseClone();
    }
}
