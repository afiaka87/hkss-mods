# hkss-dash-cooldown-radial

A BepInEx mod for Hollow Knight: Silksong that displays a radial cooldown indicator for the dash ability.

## Quick Download

https://github.com/afiaka87/hkss-mods/raw/main/releases/HKSS.DashCooldownRadial.dll

## Features

- **Radial Cooldown Indicator**: Visual circular indicator showing dash cooldown progress
- **Dynamic Positioning**: Tracks with the character's movement and adjusts based on facing direction
- **Customizable Display**: Configure size, opacity, colors, and auto-hide behavior
- **Performance Optimized**: Pre-rendered textures and efficient caching for minimal performance impact
- **Visual Feedback**: Optional pulse effect when dash becomes ready

## Installation (Recommended: Nexus Mods BepInEx)

The easiest way to install is using the preconfigured BepInEx from Nexus Mods:

1. Download BepInEx with Configuration Manager from: https://www.nexusmods.com/hollowknightsilksong/mods/26
2. Extract all files to your Hollow Knight: Silksong game directory
3. Download the [HKSS.DashCooldownRadial.dll](https://github.com/afiaka87/hkss-mods/raw/main/releases/HKSS.DashCooldownRadial.dll) (or build from source - see Step 2 below)
4. Place `HKSS.DashCooldownRadial.dll` in the `BepInEx/plugins/` folder
5. Configure Steam for Proton and set launch options (see Steam Configuration below)
6. Launch the game

For manual BepInEx installation, see Advanced Installation section below.

## Advanced Installation (Steam Deck / Linux with Proton)

### Prerequisites

**Finding your game installation:**
- Open Steam and navigate to your library
- Right-click on Hollow Knight: Silksong
- Select "Manage" → "Browse Local Files"
- Note the path (common locations):
  - Internal: `/home/deck/.local/share/Steam/steamapps/common/Hollow Knight Silksong`
  - SD Card/External: `/run/media/deck/[DRIVE_NAME]/steamapps/common/Hollow Knight Silksong`

### Step 1: Install BepInEx

**Option A: Nexus Mods Preconfigured (Recommended)**
1. Download from: https://www.nexusmods.com/hollowknightsilksong/mods/26
2. Extract all files directly to your game directory
3. The package includes Configuration Manager for easy in-game config changes (press F1)

**Option B: Manual Installation**

When using Proton, you need the Windows version of BepInEx:

1. Download BepInEx Windows x64:
```bash
cd /tmp
wget https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.3/BepInEx_win_x64_5.4.23.3.zip
```

2. Extract and prepare:
```bash
unzip -q BepInEx_win_x64_5.4.23.3.zip
cd BepInEx && mkdir -p config plugins cache patchers && cd ..
```

3. Copy to game directory:
```bash
cp -r BepInEx "[GAME_PATH]/"
cp winhttp.dll "[GAME_PATH]/"
cp doorstop_config.ini "[GAME_PATH]/"
```

### Step 2: Install .NET SDK

The mod needs to be compiled from source, which requires the .NET SDK.

**Note for Steam Deck:** If you haven't set a password yet, run `passwd` first to create one.

```bash
sudo pacman -S dotnet-sdk
```

You'll need to enter your password when prompted.

### Step 3: Build the Mod

1. Navigate to the mod directory:
```bash
cd /path/to/hkss-dash-cooldown-radial
```

2. Build in Release configuration:
```bash
dotnet build -c Release
```

This will create the compiled DLL at `bin/BepInEx/plugins/netstandard2.1/HKSS.DashCooldownRadial.dll`

### Step 4: Install the Mod

Copy the built DLL directly to the plugins folder (NOT in a subfolder):
```bash
cp bin/BepInEx/plugins/netstandard2.1/HKSS.DashCooldownRadial.dll "[GAME_PATH]/BepInEx/plugins/"
```

Example:
```bash
cp bin/BepInEx/plugins/netstandard2.1/HKSS.DashCooldownRadial.dll "/home/deck/.local/share/Steam/steamapps/common/Hollow Knight Silksong/BepInEx/plugins/"
```

### Steam Configuration (CRITICAL)

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

### Verify Installation

Your game folder should contain:
- `BepInEx/` folder with subdirectories: `core/`, `config/`, `plugins/`, `cache/`, `patchers/`
- `winhttp.dll` (Windows DLL for BepInEx hook)
- `doorstop_config.ini` (BepInEx configuration)
- `BepInEx/plugins/HKSS.DashCooldownRadial.dll` (the mod)

Launch the game. BepInEx will create a `LogOutput.log` file in the BepInEx folder if it's working.

### Troubleshooting

**Game won't start:**
- Verify you're using the Windows version of BepInEx (not Linux)
- Ensure Proton is selected, not Steam Linux Runtime
- Check launch options are exactly as shown (typos will break it)

**Mod not loading:**
- Check the DLL is directly in `BepInEx/plugins/` (not in a subfolder like `netstandard2.1`)
- Look for `HKSS.DashCooldownRadial` in `BepInEx/LogOutput.log`

**Common Mistakes:**
- Using Linux BepInEx with Proton (won't work - need Windows version)
- Missing launch options or typos in `WINEDLLOVERRIDES`
- Putting mod DLL in wrong location (must be directly in plugins folder)
- Not creating the required BepInEx subdirectories (config, cache, patchers)

**Note:** The Nexus Mods preconfigured BepInEx is the recommended installation method as it includes Configuration Manager and proper directory structure out of the box.

## Configuration

After running the game once with the mod, a configuration file will be created at:
`BepInEx/config/com.hkss.dashcooldownradial.cfg`

### Configuration Options

- **Enabled**: Enable/disable the mod
- **RadialSize**: Size of the radial indicator relative to character (0.5-2.0)
- **Opacity**: Opacity of the radial indicator (0.1-1.0)
- **PulseWhenReady**: Enable pulse animation when dash is ready
- **HideWhenAvailable**: Auto-hide indicator after dash becomes available
- **HideDelay**: Delay before hiding when dash is available (0-2 seconds)
- **CooldownColor**: Color when dash is on cooldown (default: HK Red)
- **ReadyColor**: Color when dash is ready (default: Pastel Green)
- **Position**: Position of the radial indicator (AroundCharacter/AboveCharacter/BelowCharacter)

## Building

```bash
cd hkss-dash-cooldown-radial
dotnet restore
dotnet build -c Release
```

The built DLL will be in `bin/BepInEx/plugins/`

## Technical Details

The mod uses:
- **Harmony** patches to detect dash usage and cooldown
- **Unity OnGUI** system for rendering the radial indicator
- **Pre-rendered textures** for performance optimization
- **Dynamic positioning** based on character collider bounds

## Known Issues

- The radial indicator may briefly flicker when transitioning between scenes
- Position tracking may lag slightly during very rapid movements

## Credits

Created as a HUD enhancement mod for Hollow Knight: Silksong modding.