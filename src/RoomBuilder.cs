using Godot;
using System.Collections.Generic;

namespace PaleKnight;

/// <summary>
/// Builds rooms from ASCII maps. Tile size 32. Collision: tiles layer 1 / mask 0;
/// Door/Bench/Spike/Area2D triggers layer 0 / mask 1 (detect the player).
/// Map legend: '#' solid, '.' empty, 'P' player start, 'C' crawler, 'F' flyer,
/// '^' spikes, 'B' bench, 'W' warden, 'G' geo cache (8 pickups).
/// </summary>
public static class RoomBuilder
{
    public const int TileSize = 32;

    /// <summary>RoomBuild for the most recently built room (fallback lookup).</summary>
    public static RoomBuild Current { get; private set; }

    public static RoomBuild Build(string roomId)
    {
        return roomId switch
        {
            "grotto" => BuildGrotto(),
            "pass" => BuildPass(),
            "well" => BuildWell(),
            "antechamber" => BuildAntechamber(),
            "warden_arena" => BuildWardenArena(),
            "garden" => BuildGarden(),
            _ => BuildGrotto(), // unknown id: fail safe, never null
        };
    }

    /// <summary>Instantiates every recorded non-boss enemy placement under parent.</summary>
    public static void SpawnEnemies(RoomBuild build, Node parent)
    {
        if (build == null || parent == null) return;
        foreach (var s in build.EnemySpawns)
        {
            Node2D e = s.Type switch
            {
                "crawler" => new Crawler(),
                "flyer" => new Flyer(),
                _ => null,
            };
            if (e == null) continue;
            e.Position = s.Pos;
            e.AddToGroup("enemies");
            parent.AddChild(e);
        }
    }

    // ---------------------------------------------------------------- rooms

    private static RoomBuild BuildGrotto()
    {
        var doors = new List<DoorDef>
        {
            new DoorDef { Tag = "to_pass", CellPos = new Vector2(38, 19), TargetRoom = "pass", TargetDoor = "from_grotto", Inward = new Vector2(-56, 0) },
        };
        return BuildFromMap("grotto", GrottoMap(), doors,
            new BenchInfo { BenchId = "bench_grotto", DoorTag = "start" });
    }

    private static RoomBuild BuildPass()
    {
        var doors = new List<DoorDef>
        {
            new DoorDef { Tag = "from_grotto", CellPos = new Vector2(1, 20), TargetRoom = "grotto", TargetDoor = "to_pass", Inward = new Vector2(56, 0) },
            new DoorDef { Tag = "to_well", CellPos = new Vector2(38, 20), TargetRoom = "well", TargetDoor = "from_pass", Inward = new Vector2(-56, 0) },
        };
        return BuildFromMap("pass", PassMap(), doors, null);
    }

    private static RoomBuild BuildWell()
    {
        var doors = new List<DoorDef>
        {
            new DoorDef { Tag = "from_pass", CellPos = new Vector2(14, 4), TargetRoom = "pass", TargetDoor = "to_well", Inward = new Vector2(0, 32) },
            new DoorDef { Tag = "to_ante", CellPos = new Vector2(36, 42), TargetRoom = "antechamber", TargetDoor = "from_well", Inward = new Vector2(-56, 0) },
        };
        return BuildFromMap("well", WellMap(), doors, null);
    }

    private static RoomBuild BuildAntechamber()
    {
        var doors = new List<DoorDef>
        {
            new DoorDef { Tag = "from_well", CellPos = new Vector2(1, 20), TargetRoom = "well", TargetDoor = "to_ante", Inward = new Vector2(56, 0) },
            new DoorDef { Tag = "to_arena", CellPos = new Vector2(38, 20), TargetRoom = "warden_arena", TargetDoor = "from_ante", Inward = new Vector2(-56, 0) },
        };
        return BuildFromMap("antechamber", AntechamberMap(), doors,
            new BenchInfo { BenchId = "bench_warden", DoorTag = "from_well" });
    }

    private static RoomBuild BuildWardenArena()
    {
        var doors = new List<DoorDef>
        {
            new DoorDef { Tag = "from_ante", CellPos = new Vector2(1, 20), TargetRoom = "antechamber", TargetDoor = "to_arena", Inward = new Vector2(56, 0) },
            new DoorDef { Tag = "to_garden", CellPos = new Vector2(48, 20), TargetRoom = "garden", TargetDoor = "from_arena", Inward = new Vector2(-56, 0) },
        };
        var map = WardenArenaMap();
        var build = BuildFromMap("warden_arena", map, doors, null);
        var root = build.Root;
        bool defeated = Game.Instance?.Data?.BossDefeated ?? false;

        Vector2 westCenter = new Vector2(1 * 32 + 16, 20 * 32 + 16);
        Vector2 eastCenter = new Vector2(48 * 32 + 16, 20 * 32 + 16);

        var gateW = new Gate { Name = "GateWest" };
        gateW.Position = westCenter;
        gateW.Setup(64);
        gateW.AddToGroup("boss_gates");
        root.AddChild(gateW);

        var gateE = new Gate { Name = "GateEast" };
        gateE.Position = eastCenter;
        gateE.Setup(64);
        gateE.AddToGroup("boss_gates");
        root.AddChild(gateE);

        if (defeated)
        {
            gateW.SetClosed(false);
            gateE.SetClosed(false);
        }
        else
        {
            gateW.SetClosed(false); // open until the boss trigger fires
            gateE.SetClosed(true);  // garden path locked until victory

            var w = new Warden();
            w.Position = FindCellCenter(map, 'W');
            w.ArenaBounds = new Rect2(64, 0, 1472, 720);
            w.AddToGroup("warden");
            root.AddChild(w);

            var trigger = new Area2D { Name = "BossTrigger" };
            trigger.CollisionLayer = 0;
            trigger.CollisionMask = 1;
            trigger.AddChild(new CollisionShape2D
            {
                Shape = new RectangleShape2D { Size = new Vector2(220, 260) }
            });
            trigger.Position = westCenter + new Vector2(260, 0);
            bool fired = false;
            trigger.BodyEntered += (Node2D body) =>
            {
                if (fired) return;
                if (body is not Player) return;
                fired = true;
                var tree = trigger.GetTree();
                if (tree != null)
                {
                    foreach (var n in tree.GetNodesInGroup("boss_gates"))
                    {
                        if (n is Gate g) g.Close();
                    }
                    var warden = tree.GetFirstNodeInGroup("warden") as Warden;
                    warden?.Activate();
                }
                trigger.QueueFree();
            };
            root.AddChild(trigger);
        }
        return build;
    }

    private static RoomBuild BuildGarden()
    {
        var doors = new List<DoorDef>
        {
            new DoorDef { Tag = "from_arena", CellPos = new Vector2(1, 20), TargetRoom = "warden_arena", TargetDoor = "to_garden", Inward = new Vector2(56, 0) },
        };
        return BuildFromMap("garden", GardenMap(), doors, null);
    }

    // ------------------------------------------------------------- machinery

    private sealed class BenchInfo
    {
        public string BenchId = "";
        public string DoorTag = "";
    }

    private static RoomBuild BuildFromMap(string roomId, string[] map, List<DoorDef> doors, BenchInfo bench)
    {
        int rows = map.Length;
        int cols = 0;
        foreach (var r in map)
            cols = Mathf.Max(cols, r.Length);
        var grid = new char[cols, rows];
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
                grid[x, y] = x < map[y].Length ? map[y][x] : '.';

        var root = new Node2D { Name = $"Room_{roomId}" };
        var build = new RoomBuild
        {
            Root = root,
            PlayBounds = new Rect2(0, 0, cols * 32, rows * 32),
        };

        // Background fill so there is never void behind the level.
        var bg = new ColorRect
        {
            Color = Palette.BgDeep,
            Position = Vector2.Zero,
            Size = new Vector2(cols * 32, rows * 32),
            ZIndex = -100,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        root.AddChild(bg);

        var layer = new TileMapLayer { TileSet = MakeTileSet() };
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
            {
                if (grid[x, y] != '#') continue;
                int variant = PickVariant(grid, x, y);
                layer.SetCell(new Vector2I(x, y), 0, new Vector2I(variant, 0));
            }
        root.AddChild(layer);

        // Spikes: group contiguous '^' runs per row into one node each.
        for (int y = 0; y < rows; y++)
        {
            int runStart = -1;
            for (int x = 0; x <= cols; x++)
            {
                bool spike = x < cols && grid[x, y] == '^';
                if (spike && runStart < 0) runStart = x;
                if (!spike && runStart >= 0)
                {
                    var s = new Spikes();
                    int cells = x - runStart;
                    // Center the node on the run so the hitbox lines up
                    // with the drawn triangles (shape is centered on node).
                    s.Position = new Vector2(runStart * 32 + cells * 16f, y * 32 + 16);
                    s.Setup(cells);
                    root.AddChild(s);
                    runStart = -1;
                }
            }
        }

        Vector2I? startCell = null;
        Vector2I? benchCell = null;
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
            {
                Vector2 center = new Vector2(x * 32 + 16, y * 32 + 16);
                switch (grid[x, y])
                {
                    case 'P': startCell = new Vector2I(x, y); break;
                    case 'C': build.EnemySpawns.Add(new EnemySpawn { Type = "crawler", Pos = center }); break;
                    case 'F': build.EnemySpawns.Add(new EnemySpawn { Type = "flyer", Pos = center }); break;
                    case 'B': benchCell = new Vector2I(x, y); break;
                    case 'G': GeoPickup.Spawn(root, center, 8); break;
                }
            }

        if (bench != null && benchCell.HasValue)
        {
            var b = new Bench();
            b.Position = new Vector2(benchCell.Value.X * 32 + 16, benchCell.Value.Y * 32 + 16);
            b.Setup(bench.BenchId, roomId, bench.DoorTag);
            root.AddChild(b);
        }

        foreach (var def in doors)
        {
            var d = new Door();
            Vector2 center = new Vector2(def.CellPos.X * 32 + 16, def.CellPos.Y * 32 + 16);
            d.Position = center;
            d.Setup(def);
            root.AddChild(d);
            Vector2 inward = def.Inward;
            if (inward == Vector2.Zero)
            {
                if (def.CellPos.X <= 2) inward = new Vector2(56, 0);
                else if (def.CellPos.X >= cols - 3) inward = new Vector2(-56, 0);
                else if (def.CellPos.Y <= 2) inward = new Vector2(0, 64);
                else inward = new Vector2(0, -64);
            }
            build.DoorSpawns[def.Tag] = center + inward;
        }

        if (startCell.HasValue)
            build.DoorSpawns["start"] = new Vector2(startCell.Value.X * 32 + 16, startCell.Value.Y * 32 + 16);
        else if (doors.Count > 0)
            build.DoorSpawns["start"] = build.DoorSpawns[doors[0].Tag];

        SpawnEnemies(build, root);

        // Reference to this RoomBuild, reachable from the room root node.
        root.AddChild(new RoomBuildHolder { Name = "RoomBuild", Build = build });
        Current = build;
        return build;
    }

    private static Vector2 FindCellCenter(string[] map, char c)
    {
        for (int y = 0; y < map.Length; y++)
        {
            int x = map[y].IndexOf(c);
            if (x >= 0) return new Vector2(x * 32 + 16, y * 32 + 16);
        }
        return new Vector2(64, 64);
    }

    private static int PickVariant(char[,] grid, int x, int y)
    {
        // Top edge (empty cell above) gets the light-capped tile.
        bool aboveEmpty = y == 0 || grid[x, y - 1] != '#';
        if (aboveEmpty) return 1;
        int h = (x * 73856093) ^ (y * 19349663);
        int r = Mathf.Abs(h == int.MinValue ? 0 : h) % 3;
        return r == 0 ? 0 : (r == 1 ? 2 : 3);
    }

    private static TileSet MakeTileSet()
    {
        var img = Image.Create(128, 128, false, Image.Format.Rgba8);
        var noise = new FastNoiseLite();
        noise.NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth;
        noise.Frequency = 0.08f;

        // Base rock noise on all 4 tiles.
        for (int t = 0; t < 4; t++)
        {
            int ox = t * 32;
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float n = noise.GetNoise2D(x + ox * 3.7f, y + t * 53.1f);
                    img.SetPixel(ox + x, y, Palette.Rock.Lerp(Palette.RockDark, (n + 1f) * 0.5f));
                }
        }
        // Variant 1: lighter top-edge band (5px).
        for (int x = 0; x < 32; x++)
            for (int y = 0; y < 5; y++)
            {
                float n = noise.GetNoise2D(x * 1.3f + 7.7f, y * 1.7f);
                img.SetPixel(32 + x, y, Palette.RockEdge.Lerp(Palette.Rock, (n + 1f) * 0.25f));
            }
        // Variant 2: pale speckles.
        var dots = new Vector2I[]
        {
            new Vector2I(4, 7), new Vector2I(11, 20), new Vector2I(19, 5),
            new Vector2I(24, 26), new Vector2I(27, 13), new Vector2I(8, 27),
            new Vector2I(15, 12),
        };
        foreach (var d in dots)
            for (int dy = 0; dy < 2; dy++)
                for (int dx = 0; dx < 2; dx++)
                    img.SetPixel(64 + d.X + dx, d.Y + dy, Palette.PaleDim);
        // Variant 3: dark crevice with a vertical crack.
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float n = noise.GetNoise2D(x * 0.9f + 91.3f, y * 0.9f + 4.2f);
                img.SetPixel(96 + x, y, Palette.RockDark.Lerp(Palette.ShadeBlack, 0.35f + 0.3f * (n + 1f) * 0.5f));
            }
        for (int y = 2; y < 30; y++)
        {
            img.SetPixel(96 + 14, y, Palette.ShadeBlack);
            img.SetPixel(96 + 15, y, Palette.ShadeBlack);
        }

        var tex = ImageTexture.CreateFromImage(img);

        // TileSet first: physics layer 0 must exist before any TileData
        // references it, otherwise tile collision silently fails.
        var ts = new TileSet();
        ts.TileSize = new Vector2I(32, 32);
        ts.AddPhysicsLayer(0);
        ts.SetPhysicsLayerCollisionLayer(0, 1);
        ts.SetPhysicsLayerCollisionMask(0, 0);

        var atlas = new TileSetAtlasSource();
        atlas.Texture = tex;
        atlas.TextureRegionSize = new Vector2I(32, 32);
        ts.AddSource(atlas, 0);
        var poly = new Vector2[]
        {
            new Vector2(-16, -16), new Vector2(16, -16),
            new Vector2(16, 16), new Vector2(-16, 16),
        };
        for (int i = 0; i < 4; i++)
        {
            atlas.CreateTile(new Vector2I(i, 0));
            var td = atlas.GetTileData(new Vector2I(i, 0), 0);
            td.AddCollisionPolygon(0);
            td.SetCollisionPolygonPoints(0, 0, poly);
        }
        return ts;
    }

    // ------------------------------------------------------------------ maps

    private static string[] GrottoMap() => new[]
    {
        "########################################", // r0
        "#......................................#", // r1
        "#......................................#", // r2
        "#......................................#", // r3
        "#......................................#", // r4
        "#......................................#", // r5
        "#......................................#", // r6
        "#......................................#", // r7
        "#......................................#", // r8
        "#......................................#", // r9
        "#......................................#", // r10
        "#......................................#", // r11
        "#..............................######..#", // r12 platform C (cols 31-36)
        "#......................................#", // r13
        "#......................................#", // r14
        "#......................#######.........#", // r15 platform B (cols 23-29)
        "#......................................#", // r16
        "#......................................#", // r17
        "#............#######...................#", // r18 platform A (cols 13-19)
        "#......................................#", // r19
        "#..B.P....................C............#", // r20 bench/P/crawler
        "########################################", // r21 floor
        "########################################", // r22 floor
    };

    private static string[] PassMap() => new[]
    {
        "########################################", // r0
        "#......................................#", // r1
        "#......................................#", // r2
        "#......................................#", // r3
        "#......................................#", // r4
        "#......................................#", // r5
        "#......................................#", // r6
        "#......................................#", // r7
        "#......................................#", // r8
        "#......................................#", // r9
        "#...................F..................#", // r10 flyer
        "#......................................#", // r11
        "#......................................#", // r12
        "#......................................#", // r13
        "#......................................#", // r14
        "#......................................#", // r15
        "#......................................#", // r16
        "#......................................#", // r17
        "#................###.###...............#", // r18 pit platforms
        "#......................................#", // r19
        "#......................................#", // r20
        "#################......#################", // r21 floor w/ pit
        "#################^^^^^^#################", // r22 spikes in pit
    };

    private static string[] WellMap() => new[]
    {
        "#............#............#............#", // r0  shaft walls col 13, 26
        "#............#............#............#", // r1
        "#............#............#............#", // r2
        "#............#............#............#", // r3
        "#............#............#............#", // r4
        "#............#............#............#", // r5
        "#............####.........#............#", // r6  entry floor (cols 13-16)
        "#............#............#............#", // r7
        "#............#............#............#", // r8
        "#............#.......######............#", // r9  right ledge
        "#............#............#............#", // r10
        "#............#............#............#", // r11
        "#............######.......#............#", // r12 left ledge
        "#............#............#............#", // r13
        "#............#............#............#", // r14
        "#............#.......######............#", // r15 right ledge
        "#............#............#............#", // r16
        "#............#............#............#", // r17
        "#............######.......#............#", // r18 left ledge
        "#............#............#............#", // r19
        "#............#.....F......#............#", // r20 flyer
        "#............#.......######............#", // r21 right ledge
        "#............#............#............#", // r22
        "#............#............#............#", // r23
        "#............######.......#............#", // r24 left ledge
        "#............#............#............#", // r25
        "#............#............#............#", // r26
        "#............#.......######............#", // r27 right ledge
        "#............#............#............#", // r28
        "#............#............#............#", // r29
        "#............######.......#............#", // r30 left ledge
        "#............#............#............#", // r31
        "#............#............#............#", // r32
        "#............#.......######............#", // r33 right ledge
        "#............#............#............#", // r34
        "#............#............#............#", // r35
        "#............######.......#............#", // r36 left ledge (last)
        "#......................................#", // r37 chamber
        "#......................................#", // r38
        "#......................................#", // r39
        "#......................................#", // r40
        "#......................................#", // r41
        "#......................................#", // r42
        "#.............^^^^^^^^.................#", // r43 spikes (cols 14-21)
        "########################################", // r44 floor
    };

    private static string[] AntechamberMap() => new[]
    {
        "########################################", // r0
        "#......................................#", // r1
        "#......................................#", // r2
        "#......................................#", // r3
        "#......................................#", // r4
        "#......................................#", // r5
        "#......................................#", // r6
        "#......................................#", // r7
        "#......................................#", // r8
        "#......................................#", // r9
        "#......................................#", // r10
        "#......................................#", // r11
        "#......................................#", // r12
        "#......................................#", // r13
        "#......................................#", // r14
        "#...........................#######....#", // r15 platform (cols 28-34)
        "#......................................#", // r16
        "#.................#######..............#", // r17 platform (cols 18-24)
        "#......................................#", // r18
        "#......................................#", // r19
        "#.........B............................#", // r20 bench_warden
        "########################################", // r21 floor
        "########################################", // r22 floor
    };

    private static string[] WardenArenaMap() => new[]
    {
        "##################################################", // r0 (50 cols)
        "#................................................#", // r1
        "#................................................#", // r2
        "#................................................#", // r3
        "#................................................#", // r4
        "#................................................#", // r5
        "#................................................#", // r6
        "#................................................#", // r7
        "#................................................#", // r8
        "#................................................#", // r9
        "#................................................#", // r10
        "#................................................#", // r11
        "#................................................#", // r12
        "#................................................#", // r13
        "#................................................#", // r14
        "#................................................#", // r15
        "#................................................#", // r16
        "#................................................#", // r17
        "#................................................#", // r18
        "#................................................#", // r19
        "#...................................W............#", // r20 warden
        "##################################################", // r21 floor
        "##################################################", // r22 floor
    };

    private static string[] GardenMap() => new[]
    {
        "########################################", // r0
        "#......................................#", // r1
        "#......................................#", // r2
        "#......................................#", // r3
        "#......................................#", // r4
        "#......................................#", // r5
        "#......................................#", // r6
        "#......................................#", // r7
        "#......................................#", // r8
        "#......................................#", // r9
        "#......................................#", // r10
        "#......................................#", // r11
        "#......................................#", // r12
        "#......................................#", // r13
        "#......................................#", // r14
        "#......................................#", // r15
        "#......................................#", // r16
        "#..................###.................#", // r17 platform (cols 19-21)
        "#......................................#", // r18
        "#......................................#", // r19
        "#.........G.........G.........G........#", // r20 geo caches
        "########################################", // r21 floor
        "########################################", // r22 floor
    };
}
