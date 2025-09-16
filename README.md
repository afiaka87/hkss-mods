# Hollow Knight: Silksong Info Display Mods

A collection of info display mods for Hollow Knight: Silksong using BepInEx framework.

## Quick Downloads

| Mod | Description | Download |
|-----|-------------|----------|
| **Damage Numbers** | Floating damage numbers above enemies | [Download](https://github.com/afiaka87/hkss-mods/raw/main/releases/HKSS.DamageNumbers.dll) |
| **Velocity Vector** | Real-time speed/direction display | [Download](https://github.com/afiaka87/hkss-mods/raw/main/releases/HKSS.VelocityVector.dll) |
| **Dash Cooldown Radial** | Circular cooldown indicator | [Download](https://github.com/afiaka87/hkss-mods/raw/main/releases/HKSS.DashCooldownRadial.dll) |
| **Breadcrumb Trail** | Visual path history trail | [Download](https://github.com/afiaka87/hkss-mods/raw/main/releases/HKSS.BreadcrumbTrail.dll) |
| **Air Time** | Jump duration tracking | [Download](https://github.com/afiaka87/hkss-mods/raw/main/releases/HKSS.AirTime.dll) |
| **Secret Proximity** | Hidden collectible detector | [Download](https://github.com/afiaka87/hkss-mods/raw/main/releases/HKSS.SecretProximity.dll) |
| **Data Export Bus** | External tool integration | [Download](https://github.com/afiaka87/hkss-mods/raw/main/releases/HKSS.DataExportBus.dll) |
| **Input Timeline** | Recent input history display | [Download](https://github.com/afiaka87/hkss-mods/raw/main/releases/HKSS.InputTimeline.dll) |

## Installation (Quick Start)

### Recommended: Nexus Mods BepInEx Package

The easiest installation method:

1. Download BepInEx with Configuration Manager from: https://www.nexusmods.com/hollowknightsilksong/mods/26
2. Extract all files to your Hollow Knight: Silksong game directory
3. Download mod DLLs from the table above
4. Place downloaded `.dll` files in the `BepInEx/plugins/` folder
5. Configure Steam for Proton (Linux/Steam Deck only - see below)
6. Launch the game

### Steam Deck / Linux with Proton

**IMPORTANT**: You MUST use the Windows version of BepInEx even on Linux/Steam Deck, as the game runs through Proton (Wine).

#### Steam Configuration (CRITICAL)

1. **Set Proton Compatibility:**
   - Right-click Hollow Knight: Silksong in Steam
   - Properties → Compatibility
   - Check "Force the use of a specific Steam Play compatibility tool"
   - Select Proton (e.g., Proton Experimental or Proton 9.0)
   - **DO NOT use Steam Linux Runtime**

2. **Set Launch Options:**
   - Properties → General
   - In "Launch Options", enter exactly:
   ```
   WINEDLLOVERRIDES="winhttp.dll=n,b" %command%
   ```

### Windows

1. Download BepInEx Windows x64 from: https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.3/BepInEx_win_x64_5.4.23.3.zip
2. Extract to your game directory
3. Download mod DLLs from the table above
4. Place in `BepInEx/plugins/`
5. Launch the game

### Manual BepInEx Installation (Advanced)

For manual installation from scratch:

```bash
# Download Windows BepInEx (even for Linux/Steam Deck!)
cd /tmp
wget https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.3/BepInEx_win_x64_5.4.23.3.zip

# Extract and prepare
unzip -q BepInEx_win_x64_5.4.23.3.zip
cd BepInEx && mkdir -p config plugins cache patchers && cd ..

# Copy to game directory
cp -r BepInEx "[GAME_PATH]/"
cp winhttp.dll "[GAME_PATH]/"
cp doorstop_config.ini "[GAME_PATH]/"
``` 

## Mod Implementation Status

### Completed (9/25)
- [x] **HKSS-DamageNumbers** - Floating damage numbers above enemies
- [x] **HKSS-VelocityVector** - Real-time speed/direction display with arrow indicator
- [x] **HKSS-AirTime** - Jump duration tracking with session statistics
- [x] **HKSS-BreadcrumbTrail** - Fading path history with LineRenderer
- [x] **HKSS-SecretProximity** - Hidden collectible detector with pulse indicator

- [x] **HKSS-DashCooldownRadial** - Circular cooldown indicator around character
- [x] **HKSS-DataExportBus** - External tool integration (OBS, LiveSplit, CSV/NDJSON)
- [x] **HKSS-InputTimeline** - 3-5 second input history display

### To Be Implemented (15/25)

#### S-Tier (High Value, Easy Implementation)
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

## Building from Source

### Prerequisites
1. Install .NET SDK (for building):
   ```bash
   # Steam Deck - set password first with 'passwd' if needed
   sudo pacman -S dotnet-sdk
   ```
2. Clone this repository

### Building Mods

```bash
# Build individual mod
cd hkss-[mod-name]
dotnet build -c Release

# Build all mods
for dir in hkss-*/; do
  if [ -f "$dir"/*.csproj ]; then
    echo "Building $dir"
    cd "$dir" && dotnet build -c Release && cd ..
  fi
done
```

### Installing Built Mods

After building, copy the DLL files to your game's BepInEx plugins folder:

#### Option 1: Use Pre-built Releases (Recommended)
Download directly from the table above and place in `BepInEx/plugins/`

#### Option 2: Copy from Build Output

**Linux/Steam Deck:**
```bash
# Copy specific mod
cp hkss-[mod-name]/bin/BepInEx/plugins/netstandard2.1/HKSS.*.dll \
   ~/.steam/steam/steamapps/common/Hollow\ Knight\ Silksong/BepInEx/plugins/

# Copy all built mods at once
for dll in hkss-*/bin/BepInEx/plugins/netstandard2.1/*.dll; do
  cp "$dll" ~/.steam/steam/steamapps/common/Hollow\ Knight\ Silksong/BepInEx/plugins/
done
```

**Windows:**
```batch
REM Copy specific mod
copy hkss-[mod-name]\bin\BepInEx\plugins\netstandard2.1\HKSS.*.dll ^
     "C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong\BepInEx\plugins\"
```

## Configuration

After running the game once with mods installed, configuration files are created in:
`BepInEx/config/com.hkss.[modname].cfg`

You can edit these files while the game is closed to customize each mod's behavior.

## Troubleshooting

**Game won't start (Steam Deck/Linux):**
- Verify you're using the Windows version of BepInEx (not Linux)
- Ensure Proton is selected in Steam compatibility settings
- Check launch options are exactly: `WINEDLLOVERRIDES="winhttp.dll=n,b" %command%`

**Mods not loading:**
- Check BepInEx/LogOutput.log for errors
- Ensure DLLs are directly in `BepInEx/plugins/` (not in subfolders)
- Verify you downloaded the Windows x64 version of BepInEx

**Common mistakes:**
- Using Linux BepInEx with Proton (won't work)
- Forgetting to set Steam launch options
- Missing BepInEx subdirectories (config, cache, patchers)

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
