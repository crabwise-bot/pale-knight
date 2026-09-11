using Godot;

namespace PaleKnight;

/// <summary>Shared art-direction palette. Everything in the game uses these.</summary>
public static class Palette
{
    public static readonly Color Bg = new Color("0b0e14");
    public static readonly Color BgDeep = new Color("070a10");
    public static readonly Color Rock = new Color("1c2433");
    public static readonly Color RockDark = new Color("121826");
    public static readonly Color RockEdge = new Color("31405e");
    public static readonly Color Pale = new Color("e9edf5");
    public static readonly Color PaleDim = new Color("aab3c8");
    public static readonly Color Soul = new Color("54e0c7");
    public static readonly Color SoulDark = new Color("1d7a6c");
    public static readonly Color Geo = new Color("e8d189");
    public static readonly Color Danger = new Color("c8555e");
    public static readonly Color BenchGlow = new Color("ffd98a");
    public static readonly Color Fog = new Color(0.72f, 0.80f, 0.95f, 0.09f);
    public static readonly Color ShadeBlack = new Color(0.02f, 0.03f, 0.05f, 1f);
}
