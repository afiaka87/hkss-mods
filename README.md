# Hollow Knight: Silksong Info Display Mods

A collection of 25 info display mods for Hollow Knight: Silksong using BepInEx framework.

## Mod Implementation Status

### Completed
- [x] **HKSS-DamageNumbers** - Floating damage numbers above enemies

### To Be Implemented

#### S-Tier (High Value, Easy Implementation)
- [ ] Velocity Vector HUD - Real-time speed/direction display
- [ ] Air Time Counter - Jump duration tracking
- [ ] Input Timeline Strip - 3-second input history
- [ ] Room Timer with Golds - Per-room speedrun splits
- [ ] Silk Analytics Dashboard - Healing resource tracking

#### A-Tier (Popular Demand, Moderate Complexity)
- [ ] Dash Cooldown Radial - Circular cooldown indicator
- [ ] Rosary Flow Tracker - Currency income/loss visualization
- [ ] Breadcrumb Trail - Fading path history
- [ ] Elevation Profile Chart - Height changes over time
- [ ] Attempt Heatmap - Death clustering visualization

#### B-Tier (Specialized but Valuable)
- [ ] Jump Arc Predictor - Parabolic trajectory preview
- [ ] Perfect Parry Window Flash - Counter timing indicator
- [ ] Tool/Buff Timeline - Active effect durations
- [ ] Heal Safety Predictor - Safe healing opportunity detection
- [ ] Boss Stagger Meter - Hidden posture/stagger tracking

#### C-Tier (Nice-to-Have QoL)
- [ ] Room Completion Widget - Collectibles percentage
- [ ] Secret Proximity Sensor - Hidden passage detector
- [ ] Fast Travel Network Map - Bench connection overlay
- [ ] Shop Spend Planner - Purchase optimization
- [ ] Stream-Safe Compact HUD - Minimal UI for streaming

#### Accessibility & Polish
- [ ] Colorblind Palette Presets - Visual accessibility modes
- [ ] High-Contrast Glyph Mode - Bold symbol replacements
- [ ] Minimalist Pace Bar - Pattern-based speedrun indicator
- [ ] Data Export Bus - External tool integration
- [ ] Coyote Time Visualizer - Jump grace period display

## Quick Start

```bash
# Build all completed mods
cd hkss-damage-numbers
dotnet build -c Release

# Install to game (See BepInEx installation instructions at https://github.com/BepInEx/BepInEx first)
cp bin/Release/net*/*.dll "../Hollow Knight Silksong/BepInEx/plugins/"
```

## Documentation

See [MODS_IDEAS.md](MODS_IDEAS.md) for detailed implementation specifications for all 25 mods.

## Requirements

- BepInEx 5.x or 6.0
- .NET 6.0+ SDK
- Hollow Knight: Silksong
- Unity 6000.x compatible

## License

MIT