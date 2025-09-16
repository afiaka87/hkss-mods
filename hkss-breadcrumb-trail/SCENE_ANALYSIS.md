# Scene Analysis System for Breadcrumb Trail Mod

## Overview

The breadcrumb trail mod now includes comprehensive scene analysis to understand Silksong's scene management system. This is the first step in making trails persist across room transitions.

## Features Added

### SceneAnalyzer Class
- Hooks into Unity's SceneManager events
- Logs all scene loads, unloads, and transitions
- Captures player position during transitions
- Analyzes scene coordinate systems and bounds
- Detects scene naming patterns
- Creates detailed logs for analysis

### Enhanced Harmony Patches
- `HeroController.EnterScene` - Captures transition point details
- `GameManager.LoadScene` - Logs scene loading requests
- `GameManager.BeginSceneTransition` - Captures transition metadata
- `HeroController.SetHeroParent` - Tracks scene hierarchy changes
- `HeroController.SetPositionToRespawn` - Monitors position resets

## Log Location

Scene analysis logs are saved to:
```
[Game Directory]/BepInEx/plugins/SceneLogs/scene_analysis_[timestamp].log
```

On Steam Deck/Linux:
```
~/.steam/steam/steamapps/common/Hollow Knight Silksong/BepInEx/plugins/SceneLogs/
```

## Log Contents

### Initial Scene State
- Active scene name and details
- All loaded scenes
- Root GameObjects in each scene
- Scene bounds and coordinate system

### Per-Transition Data
- Scene names (from/to)
- Transition timestamps and duration
- Player position at transition
- Transition type (additive/single)
- Active scenes list
- Coordinate system analysis

### Summary Section
- Total transitions count
- Unique scenes visited
- Transition history with timings
- Scene naming patterns detected

## Testing Instructions

1. **Install the mod**:
   ```bash
   cp releases/HKSS.BreadcrumbTrail.dll "~/.steam/steam/steamapps/common/Hollow Knight Silksong/BepInEx/plugins/"
   ```

2. **Start the game** and play normally, moving between several rooms/areas

3. **Trigger various transitions**:
   - Walk through doors
   - Use fast travel if available
   - Die and respawn
   - Enter/exit special areas

4. **Check the logs**:
   - Look for the SceneLogs folder
   - Open the latest scene_analysis_*.log file
   - Review the scene naming patterns and coordinate systems

## Key Information to Gather

### Scene Naming Convention
- Do scenes have consistent prefixes/suffixes?
- Are there zone/area identifiers?
- How are connected scenes named?

### Coordinate Systems
- Does each scene have its own coordinate space?
- What are typical scene bounds?
- How do player positions change between scenes?

### Transition Mechanics
- What TransitionPoint data is available?
- Are scenes loaded additively or replaced?
- How long do scenes remain loaded?

## Next Steps

Based on the analysis data, we will:
1. Build a scene identification system
2. Create per-scene trail containers
3. Implement coordinate transformations
4. Design the persistence system

## Debug Commands

The mod also logs to BepInEx console:
```
tail -f "~/.steam/steam/steamapps/common/Hollow Knight Silksong/BepInEx/LogOutput.log" | grep -E "(SceneAnalyzer|SCENE_|HERO_|GameManager)"
```

## Known Issues

- Scene analysis runs even when trail rendering is disabled (intentional for data gathering)
- Log files can grow large during extended play sessions
- Some reflection-based field access may fail silently (logged as warnings)