# Hollow Knight: Silksong Info Display Mods

A collection of 25 info display mods for Hollow Knight: Silksong using BepInEx framework.

## Mod Implementation Status

### Completed (8/25)
- [x] **HKSS-DamageNumbers** - Floating damage numbers above enemies
- [x] **HKSS-VelocityVector** - Real-time speed/direction display with arrow indicator
- [x] **HKSS-AirTime** - Jump duration tracking with session statistics
- [x] **HKSS-DashCooldownRadial** - Circular cooldown indicator around character
- [x] **HKSS-BreadcrumbTrail** - Fading path history with LineRenderer
- [x] **HKSS-ParryWindowFlash** - Counter timing indicator with screen flash
- [x] **HKSS-SecretProximity** - Hidden collectible detector with pulse indicator
- [x] **HKSS-DataExportBus** - External tool integration (OBS, LiveSplit, CSV/NDJSON)

### To Be Implemented (17/25)

#### S-Tier (High Value, Easy Implementation)
- [ ] Input Timeline Strip - 3-second input history
- [ ] Room Timer with Golds - Per-room speedrun splits
- [ ] Silk Analytics Dashboard - Healing resource tracking

#### A-Tier (Popular Demand, Moderate Complexity)
- [ ] Rosary Flow Tracker - Currency income/loss visualization
- [ ] Elevation Profile Chart - Height changes over time
- [ ] Attempt Heatmap - Death clustering visualization

#### B-Tier (Specialized but Valuable)
- [ ] Jump Arc Predictor - Parabolic trajectory preview
- [ ] Tool/Buff Timeline - Active effect durations
- [ ] Heal Safety Predictor - Safe healing opportunity detection
- [ ] Boss Stagger Meter - Hidden posture/stagger tracking

#### C-Tier (Nice-to-Have QoL)
- [ ] Room Completion Widget - Collectibles percentage
- [ ] Fast Travel Network Map - Bench connection overlay
- [ ] Shop Spend Planner - Purchase optimization
- [ ] Stream-Safe Compact HUD - Minimal UI for streaming

#### Accessibility & Polish
- [ ] Colorblind Palette Presets - Visual accessibility modes
- [ ] High-Contrast Glyph Mode - Bold symbol replacements
- [ ] Minimalist Pace Bar - Pattern-based speedrun indicator
- [ ] Coyote Time Visualizer - Jump grace period display

## Installation Guide

### Prerequisites
1. Install BepInEx 5.x to your Hollow Knight: Silksong game directory
   - Download from: https://github.com/BepInEx/BepInEx
   - Extract to game root (where the .exe is located)
2. Ensure .NET 6.0+ SDK is installed for building

### Building Mods

```bash
# Build individual mod
cd hkss-[mod-name]
dotnet build -c Release

# Build all completed mods (example)
for dir in hkss-*/; do
  echo "Building $dir"
  cd "$dir" && dotnet build -c Release && cd ..
done
```

### Installing Individual Mods

Each mod builds to its own `bin/BepInEx/plugins/` directory. Copy the DLL files to your game's BepInEx plugins folder:

#### Linux/Steam Deck
```bash
# General pattern
cp [mod-directory]/bin/BepInEx/plugins/netstandard2.1/HKSS.[ModName].* \
   ~/.steam/steam/steamapps/common/Hollow\ Knight\ Silksong/BepInEx/plugins/

# Specific examples:
# Damage Numbers
cp hkss-damage-numbers/bin/BepInEx/plugins/netstandard2.1/HKSS.DamageNumbers.* \
   ~/.steam/steam/steamapps/common/Hollow\ Knight\ Silksong/BepInEx/plugins/

# Velocity Vector
cp hkss-velocity-vector/bin/BepInEx/plugins/netstandard2.1/HKSS.VelocityVector.* \
   ~/.steam/steam/steamapps/common/Hollow\ Knight\ Silksong/BepInEx/plugins/

# Air Time Counter
cp hkss-air-time/bin/BepInEx/plugins/netstandard2.1/HKSS.AirTime.* \
   ~/.steam/steam/steamapps/common/Hollow\ Knight\ Silksong/BepInEx/plugins/

# Dash Cooldown Radial
cp hkss-dash-cooldown-radial/bin/BepInEx/plugins/netstandard2.1/HKSS.DashCooldown.* \
   ~/.steam/steam/steamapps/common/Hollow\ Knight\ Silksong/BepInEx/plugins/

# Breadcrumb Trail
cp hkss-breadcrumb-trail/bin/BepInEx/plugins/netstandard2.1/HKSS.BreadcrumbTrail.* \
   ~/.steam/steam/steamapps/common/Hollow\ Knight\ Silksong/BepInEx/plugins/

# Parry Window Flash
cp hkss-parry-window-flash/bin/BepInEx/plugins/netstandard2.1/HKSS.ParryWindowFlash.* \
   ~/.steam/steam/steamapps/common/Hollow\ Knight\ Silksong/BepInEx/plugins/

# Secret Proximity Sensor
cp hkss-secret-proximity-sensor/bin/BepInEx/plugins/netstandard2.1/HKSS.SecretProximity.* \
   ~/.steam/steam/steamapps/common/Hollow\ Knight\ Silksong/BepInEx/plugins/

# Data Export Bus
cp hkss-data-export-bus/bin/BepInEx/plugins/netstandard2.1/HKSS.DataExportBus.* \
   ~/.steam/steam/steamapps/common/Hollow\ Knight\ Silksong/BepInEx/plugins/
```

#### Windows
```batch
REM General pattern
copy [mod-directory]\bin\BepInEx\plugins\netstandard2.1\HKSS.[ModName].* ^
     "C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong\BepInEx\plugins\"

REM Example for Damage Numbers
copy hkss-damage-numbers\bin\BepInEx\plugins\netstandard2.1\HKSS.DamageNumbers.* ^
     "C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong\BepInEx\plugins\"
```

### Configuration

After first run, configuration files are created in:
- **Linux**: `~/.steam/steam/steamapps/common/Hollow Knight Silksong/BepInEx/config/`
- **Windows**: `C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong\BepInEx\config\`

Config files are named: `com.hkss.[modname].cfg`

### Special Instructions

#### Data Export Bus
After installation, access the web dashboard at `http://localhost:8080/` while the game is running. For LiveSplit integration, connect to `localhost:9090`. For OBS, use WebSocket on port `9091`.

## Documentation

See [DESIGN.md](DESIGN.md) for detailed implementation specifications for all 25 mods.

## Requirements

- BepInEx 5.x or 6.0
- .NET 6.0+ SDK
- Hollow Knight: Silksong
- Unity 6000.x compatible

## License

MIT