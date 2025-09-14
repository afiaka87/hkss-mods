# HKSS Damage Numbers

A BepInEx mod for Hollow Knight: Silksong that displays floating damage numbers when attacking enemies.

## Features

- **Floating Damage Numbers**: See exactly how much damage you deal with each attack
- **Critical Hits**: Random chance for critical strikes that deal 1.5x damage (shown in gold with "!")
- **Customizable Display**: Configure colors, size, speed, and duration of damage numbers
- **Performance Optimized**: Uses object pooling to minimize performance impact
- **Optional Player Damage**: Can also show damage numbers when the player takes damage

## Installation

1. Install BepInEx 5.x or 6.x to your Hollow Knight: Silksong game directory
2. Build the mod or download the release DLL
3. Place `HKSS.DamageNumbers.dll` in the `BepInEx/plugins/` folder
4. Launch the game

## Configuration

After running the game once with the mod, a configuration file will be created at:
`BepInEx/config/com.hkss.damagenumbers.cfg`

### Configuration Options

- **Enabled**: Enable/disable the mod
- **Duration**: How long damage numbers remain visible (0.5-5 seconds)
- **FloatSpeed**: Speed at which numbers float upward
- **FontSize**: Size of damage text (12-72)
- **ShowCriticalHits**: Enable critical hit display
- **CriticalChance**: Chance for critical hits (0.0-1.0)
- **NormalDamageColor**: Hex color for normal damage
- **CriticalDamageColor**: Hex color for critical damage
- **ShowPlayerDamage**: Also show numbers when player takes damage

## Building

```bash
cd HKSS-DamageNumbers
dotnet restore
dotnet build -c Release
```

The built DLL will be in `bin/BepInEx/plugins/`

## Technical Details

The mod uses:
- **Harmony** patches to intercept damage calculations
- **Unity UI** system for rendering text in world space
- **Object pooling** for efficient number spawning
- **Configurable critical hit system** with visual feedback

## Known Issues

- Damage numbers may occasionally appear at incorrect positions for very fast-moving enemies
- Critical hit calculation is independent of game mechanics (purely visual)

## Credits

Created as a proof-of-concept mod for Hollow Knight: Silksong modding.