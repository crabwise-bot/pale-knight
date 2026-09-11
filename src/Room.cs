using Godot;
using System.Collections.Generic;

namespace PaleKnight;

/// <summary>Door metadata used by RoomBuilder. CellPos is in tile cells.</summary>
public class DoorDef
{
    public string Tag = "";
    public Vector2 CellPos;
    public string TargetRoom = "";
    public string TargetDoor = "";
    /// <summary>
    /// Spawn offset from the door center (pushes the player just inside the room).
    /// If left as Vector2.Zero, RoomBuilder derives it from the nearest map edge.
    /// </summary>
    public Vector2 Inward;
}

/// <summary>One non-boss enemy placement, recorded so benches can respawn it.</summary>
public class EnemySpawn
{
    public string Type = ""; // "crawler" | "flyer"
    public Vector2 Pos;
}

/// <summary>Everything Build() produces for one room.</summary>
public class RoomBuild
{
    public Node2D Root;
    public Rect2 PlayBounds;
    public Dictionary<string, Vector2> DoorSpawns = new();
    /// <summary>Non-boss enemy placements ('C'/'F' cells). The Warden is never recorded here.</summary>
    public List<EnemySpawn> EnemySpawns = new();
}

/// <summary>
/// Child node placed on the room root so gameplay code (e.g. Bench) can reach
/// the RoomBuild that produced the current room without globals.
/// </summary>
public partial class RoomBuildHolder : Node
{
    public RoomBuild Build;
}
