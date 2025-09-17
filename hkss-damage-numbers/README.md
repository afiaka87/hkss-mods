# hkss-damage-numbers

A BepInEx mod for Hollow Knight: Silksong that displays floating damage numbers when attacking enemies or taking damage.

## Features

- **Enemy Damage Numbers**: Golden floating numbers that rise when you damage enemies
- **Player Damage Numbers**: Crimson sinking numbers with warning pulse when you take damage
- **Distinct Visual Feedback**: Positive (enemy damage) vs negative (player damage) animations
- **Resolution Scaling**: Automatically adjusts text size for different screen resolutions (720p to 4K)
- **Fully Configurable**: Customize colors, size, speed, and duration
- **Performance Optimized**: Uses object pooling to minimize performance impact

## Quick Download

**[Download HKSS.DamageNumbers.dll](https://github.com/afiaka87/hkss-mods/raw/main/releases/HKSS.DamageNumbers.dll)**

## Installation

For detailed installation instructions, see the [main README](../README.md#installation-quick-start).

Quick steps:
1. Install BepInEx (see [parent README](../README.md))
2. Download the DLL above
3. Place in `BepInEx/plugins/` folder
4. Launch the game

## Configuration

After first run, configure at: `BepInEx/config/com.hkss.damagenumbers.cfg`

- **EnemyDamageColor**: Color for enemy damage (default: golden #FFD700)
- **PlayerDamageColor**: Color for player damage (default: crimson #DC143C)
- **ShowPlayerDamage**: Show player damage numbers (default: true)
- **Duration**: How long numbers stay visible (0.5-5 seconds)
- **FloatSpeed**: Animation speed
- **BaseFontSize**: Base text size that scales with resolution (12-72)
- **AutoScaleResolution**: Automatically scale font size based on screen resolution (default: true)

## Building from Source

```bash
dotnet build -c Release
```

See [parent README](../README.md#building-from-source) for full build instructions.

## Known Issues

- Damage numbers may occasionally appear at incorrect positions for very fast-moving enemies
- Numbers can overlap when multiple hits occur in the same location quickly
- Config changes require game restart to take effect (no hot-reload)
- Damage numbers don't scale with camera zoom/distance
- No damage type indicators (critical hits, elemental damage, etc.) - shows raw damage only
- Numbers may briefly appear through walls/geometry