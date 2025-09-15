# hkss-input-timeline

A BepInEx mod for Hollow Knight: Silksong that displays the 3-5 most recent player actions on screen with polished ASCII visualization.

## Features

- **Recent Action Display**: Shows your last 3-5 actions as they happen
- **Clean ASCII Design**: Polished boxes with action names and character symbols
- **Input Detection**: Tracks jumps, attacks, dashes, healing, and landing
- **Analog Stick Support**: Detects left analog stick movements (single-shot, not continuous)
- **D-Pad Support**: Separate tracking for D-Pad/arrow key inputs
- **Visual Feedback**: Most recent action highlighted, older actions fade out
- **Configurable Display**: Adjust position, opacity, and number of actions shown

## Installation (Recommended: Nexus Mods BepInEx)

The easiest way to install is using the preconfigured BepInEx from Nexus Mods:

1. Download BepInEx with Configuration Manager from: https://www.nexusmods.com/hollowknightsilksong/mods/26
2. Extract all files to your Hollow Knight: Silksong game directory
3. Build this mod (see Step 2 below) or download the release DLL
4. Place `HKSS.InputTimeline.dll` in the `BepInEx/plugins/` folder
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
cd /path/to/hkss-input-timeline
```

2. Build in Release configuration:
```bash
dotnet build -c Release
```

This will create the compiled DLL at `bin/BepInEx/plugins/netstandard2.1/HKSS.InputTimeline.dll`

### Step 4: Install the Mod

Copy the built DLL directly to the plugins folder (NOT in a subfolder):
```bash
cp bin/BepInEx/plugins/netstandard2.1/HKSS.InputTimeline.dll "[GAME_PATH]/BepInEx/plugins/"
```

Example:
```bash
cp bin/BepInEx/plugins/netstandard2.1/HKSS.InputTimeline.dll "/home/deck/.local/share/Steam/steamapps/common/Hollow Knight Silksong/BepInEx/plugins/"
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
- `BepInEx/plugins/HKSS.InputTimeline.dll` (the mod)

Launch the game. BepInEx will create a `LogOutput.log` file in the BepInEx folder if it's working.

### Troubleshooting

**Game won't start:**
- Verify you're using the Windows version of BepInEx (not Linux)
- Ensure Proton is selected, not Steam Linux Runtime
- Check launch options are exactly as shown (typos will break it)

**Mod not loading:**
- Check the DLL is directly in `BepInEx/plugins/` (not in a subfolder like `netstandard2.1`)
- Look for `HKSS.InputTimeline` in `BepInEx/LogOutput.log`

**Common Mistakes:**
- Using Linux BepInEx with Proton (won't work - need Windows version)
- Missing launch options or typos in `WINEDLLOVERRIDES`
- Putting mod DLL in wrong location (must be directly in plugins folder)
- Not creating the required BepInEx subdirectories (config, cache, patchers)

**Note:** The Nexus Mods preconfigured BepInEx is the recommended installation method as it includes Configuration Manager and proper directory structure out of the box.

## Configuration

After running the game once with the mod, a configuration file will be created at:
`BepInEx/config/com.hkss.inputtimeline.cfg`

### Configuration Options

- **Enabled**: Enable/disable the mod
- **MaxRecentActions**: Number of recent actions to display (3-10, default: 5)
- **Position**: Screen position (Top/Bottom/Center)
- **TimeWindow**: How long actions remain visible (2-10 seconds, default: 5)
- **Opacity**: Overall display opacity (0.1-1.0)
- **ShowTimestamps**: Show time since each action occurred
- **ShowBackground**: Display background strip behind actions
- **ActionBoxColor**: Color for normal action boxes
- **HighlightColor**: Color for most recent action
- **BackgroundColor**: Color of background strip

## Action Display

The mod displays actions in bordered ASCII boxes with the following format:

```
┌─────┐
│JUMP │  <- Action name
│ [^] │  <- Character symbol
└─────┘
```

### Tracked Actions

**Core Actions:**
- `JUMP [^]` - Jump action
- `ATTK [X]` - Attack
- `DASH [>]` - Dash
- `HEAL [+]` - Focus/Heal
- `LAND [v]` - Landing (displays air time)

**Analog Stick (Left stick):**
- `L< [<]` - Left Analog Left
- `L> [>]` - Left Analog Right
- `L^ [^]` - Left Analog Up
- `Lv [v]` - Left Analog Down

**D-Pad/Arrow Keys:**
- `D< [<]` - D-Pad/Arrow Left
- `D> [>]` - D-Pad/Arrow Right
- `D^ [^]` - D-Pad/Arrow Up
- `Dv [v]` - D-Pad/Arrow Down

All directional inputs are single-shot (won't spam when held) with a 0.5 threshold for analog stick detection.

## Building

```bash
cd hkss-input-timeline
dotnet restore
dotnet build -c Release
```

The built DLL will be in `bin/BepInEx/plugins/netstandard2.1/`

## Technical Details

The mod uses:
- **Harmony** patches to hook into HeroController state transitions
- **Unity OnGUI** for rendering ASCII boxes and text
- **Queue-based** action history with configurable size
- **Single-shot detection** for directional inputs to prevent spam
- **Fade effects** based on time since action occurred

## Known Issues

- D-Pad axes might not be detected on all controller types (falls back to arrow keys)
- Very rapid inputs may occasionally be missed if they occur within the same frame

## Credits

Created as part of the Hollow Knight: Silksong info display mods collection.