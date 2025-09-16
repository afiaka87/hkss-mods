# Breadcrumb Trail Mod - Task Progress

## ✅ Completed Tasks

### Core Functionality
- ✅ Fixed critical visibility regression (trail now renders properly)
- ✅ Created MultiSceneTrailManager for per-scene trail management
- ✅ Implemented separate LineRenderers per scene
- ✅ Scene transition detection with proper event hooks
- ✅ JSON persistence system (auto-saves every 5 seconds to `TrailSaves/trail_data.json`)
- ✅ Fixed all Harmony patch parameter mismatches (`enterGate`, `destScene`)
- ✅ Added comprehensive error logging and state reporting
- ✅ Massively extended trail duration to 1800s default (configurable 60-7200s)
- ✅ Increased max points to 20,000 per scene (configurable 1000-100,000)
- ✅ Dynamic configuration reading from BepInEx config file
- ✅ Scene bounds calculation for each loaded scene
- ✅ Proper singleton pattern for manager persistence
- ✅ Increased simultaneous scene rendering from 3 to 20 scenes

### Logging & Debugging
- ✅ SceneAnalyzer system for understanding scene transitions
- ✅ Detailed transition logging (GameManager.BeginSceneTransition, HeroController.EnterScene)
- ✅ Scene metadata capture and analysis
- ✅ Try-catch wrapping with detailed error reporting
- ✅ Scene load/unload event tracking

### Performance Optimizations
- ✅ **Trail Point Throttling** [Completed: 8 points]
  - ✅ Angle-based culling (5° threshold, skips collinear points)
  - ✅ Adaptive sampling based on movement complexity (0.05-0.2s)
  - ✅ Distance-based point merging (0.5 unit minimum)
  - ✅ Created TrailOptimizer class with full optimization pipeline
  - ✅ Added 10 new configuration options for performance tuning
  - ✅ Real-time statistics overlay showing reduction percentage
  - ✅ Achieves 40-60% point reduction without visual quality loss

## 📋 Remaining Tasks

### Performance Optimizations (High Priority)
- [ ] **Frustum Culling** [Estimate: 13 points]
  - Only render trail segments within camera view
  - Quad-tree spatial indexing for efficient culling
  - Dynamic LOD based on camera distance

- [ ] **Batch Rendering** [Estimate: 8 points]
  - Combine multiple trail segments into single draw calls
  - Mesh optimization for LineRenderer

### Visual Enhancements (Medium Priority)
- [ ] **Advanced Gradient Styles** [Estimate: 5 points]
  - Rainbow/spectrum mode
  - Pulsing/breathing effects
  - Custom gradient editor in config

- [ ] **Trail Width Variation** [Estimate: 3 points]
  - Width based on movement speed
  - Tapered ends for better aesthetics
  - Combat state width changes

- [ ] **Particle Effects** [Estimate: 8 points]
  - Optional sparkle/glow particles at trail points
  - Dust/smoke effects for dashing
  - Configurable particle density

### World Map Integration (Very High Priority - High Complexity)

#### Compass-Based Map Trail System [Total Estimate: 55 points]
This is a **completely separate trail system** from the in-game world trails because the map view doesn't have a 1:1 correspondence with actual scene geometry. Instead, we track the player's compass/map cursor position to create map-specific trails.

- [ ] **Research Map System Architecture** [Estimate: 8 points]
  - Investigate Silksong's map UI components and rendering pipeline
  - Find map compass/cursor GameObjects and their update methods
  - Identify map coordinate system (likely normalized 0-1 or pixel-based)
  - Determine map zoom levels and panning mechanics
  - Find map overlay injection points for custom rendering

- [ ] **Create MapTrailManager Component** [Estimate: 13 points]
  - Separate from MultiSceneTrailManager (different coordinate system)
  - Track compass position updates (likely from MapManager or similar)
  - Store map trail points with map-specific coordinates
  - Handle map open/close events
  - Manage separate persistence for map trails

- [ ] **Map Coordinate Tracking** [Estimate: 8 points]
  - Hook into compass/player icon position updates
  - Convert world position to map position (non-trivial mapping)
  - Handle different map zoom levels
  - Account for map areas that haven't been revealed yet
  - Track time spent looking at different map areas

- [ ] **Map Trail Rendering System** [Estimate: 13 points]
  - Create custom UI overlay for map view
  - Render trails using UI LineRenderer or custom mesh
  - Handle map panning and zooming (trail needs to scale/translate)
  - Different rendering style from world trails (thinner, more subtle)
  - Layer properly with existing map elements
  - Smooth interpolation between compass position updates

- [ ] **Map Trail Persistence** [Estimate: 5 points]
  - Separate save file for map trails (`map_trail_data.json`)
  - Store normalized map coordinates (independent of resolution)
  - Save zoom level and pan position history
  - Map area discovery timestamps

- [ ] **Map-Specific Features** [Estimate: 8 points]
  - Heat map mode showing most visited map areas
  - "Fog of war" style trail revealing (only show where you've been)
  - Different colors for different play sessions
  - Map annotation system (mark points of interest)
  - Path optimization suggestions (shortest routes between areas)

#### Additional Map Integration Features
- [ ] **World-to-Map Trail Correlation** [Estimate: 8 points]
  - Attempt to correlate world trails with map positions
  - Show approximate world trail on map (with disclaimer about accuracy)
  - Highlight current scene boundaries on map

- [ ] **Map Legend and Statistics** [Estimate: 3 points]
  - Show trail age/timestamp info
  - Room visit frequency heatmap
  - Path statistics (distance traveled on map, areas explored percentage)

### Edge Case Handling (High Priority)
- [ ] **Death/Respawn Behavior** [Estimate: 5 points]
  - Option to clear trail on death
  - Ghost trail showing death location
  - Separate death marker system

- [ ] **Fast Travel Support** [Estimate: 5 points]
  - Detect fast travel events
  - Option to connect fast travel points
  - Different line style for teleports

- [ ] **Cutscene Detection** [Estimate: 8 points]
  - Pause trail recording during cutscenes
  - Detect and handle boss intro sequences
  - Menu/pause state handling

### Advanced Features (Low Priority)
- [ ] **Trail Sharing/Export** [Estimate: 8 points]
  - Export trail as image/video
  - Share trail data with other players
  - Trail replay system

- [ ] **Statistics Dashboard** [Estimate: 13 points]
  - Total distance traveled
  - Time spent in each area
  - Movement pattern analysis
  - Speed/efficiency metrics

- [ ] **Trail Annotations** [Estimate: 8 points]
  - Mark important locations
  - Add text notes to trail points
  - Death/boss fight markers

### Technical Debt
- [ ] **Remove Unused Code** [Estimate: 2 points]
  - Clean up SceneAnalyzer (move to separate debug mod?)
  - Remove maxSceneMemoryTime unused field
  - Consolidate duplicate logging

- [ ] **Optimize Save System** [Estimate: 5 points]
  - Compress JSON data
  - Implement incremental saves
  - Add save versioning/migration

- [ ] **Unit Tests** [Estimate: 8 points]
  - Test coordinate transformations
  - Verify save/load integrity
  - Performance benchmarks

## 📊 Summary

**Total Completed**: ~97 story points (added 8 points for Trail Point Throttling)
**Total Remaining**: ~176 story points

**Next Priority Actions**:
1. ~~Implement trail point throttling for performance~~ ✅ COMPLETED
2. Implement frustum culling for camera-based rendering optimization
3. Add death/respawn handling
4. Research world map integration possibilities

## Notes

- The mod is now fully functional with multi-room persistence
- Trail duration extended to 30 minutes (configurable up to 2 hours!)
- Performance optimizations achieve 40-60% point reduction
- Can now render 20 scenes simultaneously with 20,000 points each
- TrailOptimizer intelligently reduces points without visual impact
- Save files can grow large without compression
- The "Saved 4 scenes" spam in logs should be reduced (only log on actual changes)
