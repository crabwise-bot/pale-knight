# 🦀 Pale Knight

A Hollow Knight-inspired metroidvania built with **Godot 4.7 (.NET / C#)**.

You are a small pale knight exploring the fallen burrows — a dark, quiet
underworld of caverns, spikes, and restless husks. Slash through crawlers,
dive past flyers, rest at benches, gather soul, and face **The Warden** in
the sealed arena below.

All art is original (procedural silhouettes + generated backgrounds).
All sound is synthesized in-repo.

## Controls

| Action | Key | Gamepad |
|---|---|---|
| Move | A / D or ← / → | Left stick / D-pad |
| Jump | Space / W / ↑ | A (bottom button) |
| Dash | Shift / L | RT / B |
| Attack (nail slash) | J / X | X (left button) |
| Hold to heal (focus) | K / C | Y (top button) |
| Interact (bench/door prompt) | E / ↑ | — |
| Pause | Esc / P | Start |

Down + Attack in midair performs a downward slash — pogo off enemies!

## How to run

Requires **Godot 4.7 .NET edition** (a.k.a. the C# / Mono build).

1. Clone this repo.
2. Open `project.godot` in the Godot 4.7 .NET editor (it will import assets
   on first open).
3. Press **Play** (F5). That's it — no build step needed; the editor
   compiles the C# automatically.

From the command line (if you have the Godot binary + .NET 8 SDK):

```sh
dotnet build PaleKnight.csproj
godot --path . # then press Play in the editor, or run the main scene
```

## Features (Phase 1)

- Player: run / variable jump / coyote time / jump buffering / dash with
  i-frames + afterimages / wall slide + wall jump / 4-directional nail slash
  with pogo / soul meter / focus-heal / knockback + i-frames / bench respawn
- Enemies: ground crawler (patrols, turns at ledges) and dive-bombing flyer
- Boss: **The Warden** — authentic HK rules (no HP bar; damage shown through
  hit-flash and behavior; hit-count stagger system; enrage phase below 50%;
  telegraphed leap slam / arena dash / arcing projectiles)
- World: 6 hand-designed rooms with room-transition camera slides, spikes,
  benches (heal + respawn + save), parallax backgrounds, fog, dynamic 2D
  lighting, ambient dust
- UI: title screen, HUD (masks / soul vessel / geo), pause menu, death screen
- Save system: JSON to `user://`, stores room/bench/health/geo/boss state
- Audio: fully synthesized SFX + ambient drone + boss music (see `tools/`)

## Roadmap (post-Phase 1)

- NPCs + dialogue
- World map system
- Charms / upgrades
- More bosses, more areas
- Gamepad rumble, accessibility options

## Project layout

```
project.godot          Engine project (Godot 4.7, C#, 1280x720)
PaleKnight.csproj      .NET 8 project (Godot.NET.Sdk/4.7.2)
scenes/Main.tscn       Entry scene (everything else is built in code)
src/                   All C# — player, enemies, boss, world, UI, systems
assets/sfx/            Synthesized WAV sound effects + music loops
assets/textures/       Generated + procedural textures
tools/synth_sfx.py     The synthesizer that made every sound in the game
```
