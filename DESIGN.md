# MODS_IDEAS.md - Hollow Knight: Silksong Info Display Mods

## Overview

This document serves as the definitive source of truth for 25 info display mods for Hollow Knight: Silksong. Each mod is designed to provide valuable gameplay information without modifying core game mechanics. Mods are categorized by implementation track (External/Legal vs Injected/Practice) and complexity tier.

## Implementation Tracks

### Track A: External/Leaderboard-Legal
Mods that read game memory or files without injection. Safe for competitive speedrunning and leaderboard submissions. These connect to external tools like LiveSplit or browser overlays.

### Track B: Injected/Practice-Only
Mods that use BepInEx and Harmony to hook into the game directly. Provides deeper integration but disqualifies runs from leaderboards. Perfect for practice, analysis, and streaming.

---

## S-Tier Mods (High Value, Easy Implementation)

### 1. Velocity Vector HUD
**Purpose**: Shows player's current speed and direction as both a numeric value (m/s) and directional arrow. Essential for speedrunners optimizing movement tech and newer players learning momentum mechanics.

**Implementation**:
```csharp
// Hook into HeroController's Rigidbody2D
[HarmonyPatch(typeof(HeroController), "FixedUpdate")]
class VelocityPatch {
    static void Postfix(HeroController __instance) {
        Vector2 velocity = __instance.rb2d.velocity;
        float speed = velocity.magnitude;
        float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
        VelocityDisplay.UpdateDisplay(speed, angle);
    }
}
```

**Display**: Arrow overlay near character with speed text. Uses OnGUI for rendering with configurable position anchoring (9 screen positions).

**Configuration**:
- DisplayUnits: "m/s", "units/s", "pixels/frame"
- ArrowScale: 0.5-2.0x size multiplier
- ShowPeakSpeed: Track session maximum
- Position: TopLeft, TopCenter, TopRight, etc.

**Build**: `dotnet build -c Release` → copies to `BepInEx/plugins/`

---

### 2. Air Time Counter
**Purpose**: Tracks time spent airborne per jump and cumulative session air time. Helps players optimize jump chains and understand coyote time windows.

**Implementation**:
```csharp
private float currentAirTime = 0f;
private float sessionTotalAirTime = 0f;
private bool wasGrounded = true;

void Update() {
    bool grounded = HeroController.instance.cState.onGround;

    if (!grounded) {
        currentAirTime += Time.deltaTime;
        sessionTotalAirTime += Time.deltaTime;
    } else if (!wasGrounded && grounded) {
        // Just landed - log the jump duration
        Logger.LogInfo($"Jump duration: {currentAirTime:F2}s");
        currentAirTime = 0f;
    }
    wasGrounded = grounded;
}
```

**Display**: Two counters - current jump timer (resets on landing) and session total. Optional histogram of jump durations.

**Configuration**:
- ShowCurrentJump: true/false
- ShowSessionTotal: true/false
- ShowJumpHistory: Last 10 jumps as mini bar chart
- DecimalPlaces: 1-3 precision

---

### 3. Input Timeline Strip
**Purpose**: Rolling 3-second visual history of all inputs. Invaluable for tutorials, debugging input drops, and verifying frame-perfect sequences.

**Implementation (External Track)**:
Uses memory reading to capture input state without injection:
```csharp
// Memory addresses found via Cheat Engine pattern scanning
IntPtr inputStateAddr = gameProcess.MainModule.BaseAddress + 0x1A2B3C4;
byte[] inputBuffer = ReadProcessMemory(inputStateAddr, 16);
// Bit flags: Jump=0x01, Dash=0x02, Attack=0x04, etc.
```

**Implementation (Injected Track)**:
```csharp
[HarmonyPatch(typeof(InputHandler), "Update")]
static void Postfix(InputHandler __instance) {
    InputFrame frame = new InputFrame {
        timestamp = Time.time,
        jump = __instance.inputActions.jump.IsPressed,
        dash = __instance.inputActions.dash.IsPressed,
        attack = __instance.inputActions.attack.IsPressed,
        // ... other inputs
    };
    InputTimeline.RecordFrame(frame);
}
```

**Display**: Horizontal scrolling strip showing button icons. Held inputs shown as bars, taps as dots. Color coding for simultaneous inputs.

**Configuration**:
- TimeWindow: 1-5 seconds of history
- CompactMode: Merge rapid inputs
- ShowAnalog: Include stick positions
- Export: Save to CSV for analysis

---

### 4. Room Timer with Golds
**Purpose**: Tracks time spent in each room/scene with personal best comparisons. Shows ahead/behind splits for route optimization.

**Implementation**:
```csharp
private Dictionary<string, float> roomGolds = new Dictionary<string, float>();
private float currentRoomTime = 0f;
private string currentRoom = "";

[HarmonyPatch(typeof(SceneManager), "LoadScene")]
static void Prefix(string sceneName) {
    // Save time for previous room
    if (!string.IsNullOrEmpty(currentRoom)) {
        float previousBest = roomGolds.GetValueOrDefault(currentRoom, float.MaxValue);
        if (currentRoomTime < previousBest) {
            roomGolds[currentRoom] = currentRoomTime;
            SaveGoldsToFile();
        }
    }
    currentRoom = sceneName;
    currentRoomTime = 0f;
}
```

**LiveSplit Integration**:
```csharp
// Write to named pipe for LiveSplit to read
using (var pipe = new NamedPipeClientStream("LiveSplitPipe")) {
    pipe.Connect();
    byte[] data = Encoding.UTF8.GetBytes($"room:{sceneName}:{currentRoomTime}");
    pipe.Write(data, 0, data.Length);
}
```

**Display**: Timer with +/- differential. Green when ahead of PB, red when behind. Optional sum of zones/areas.

**Configuration**:
- ComparisonMode: "Personal Best", "Sum of Best", "Average"
- AutoReset: Reset on death/bench
- ExportFormat: LiveSplit XML, CSV, JSON

---

### 5. Silk Analytics Dashboard
**Purpose**: Comprehensive tracking of silk (healing resource) usage patterns. Shows opportunities per minute, efficiency percentage, and waste analysis.

**Implementation**:
```csharp
public class SilkAnalytics {
    private int healOpportunities = 0;  // Times at low health with silk available
    private int healsUsed = 0;
    private float totalSilkGained = 0f;
    private float totalSilkSpent = 0f;

    [HarmonyPatch(typeof(PlayerData), "AddSilk")]
    static void Postfix(int amount) {
        totalSilkGained += amount;
    }

    [HarmonyPatch(typeof(HeroController), "Heal")]
    static void Postfix() {
        healsUsed++;
        totalSilkSpent += HEAL_COST;
    }

    void CheckHealOpportunity() {
        if (PlayerData.instance.health < PlayerData.instance.maxHealth * 0.5f &&
            PlayerData.instance.silk >= HEAL_COST) {
            healOpportunities++;
        }
    }
}
```

**Display**: Multi-stat panel showing efficiency %, opportunities/minute, average time between heals, silk income rate.

**Configuration**:
- TrackingPeriod: Session, Last Hour, Per Boss
- EfficiencyFormula: Custom thresholds
- WarnOnWaste: Flash when overhealing

---

## A-Tier Mods (Popular Demand, Moderate Complexity)

### 6. Dash Cooldown Radial
**Purpose**: Circular cooldown indicator that follows Hornet, showing dash availability at a glance without looking at UI corners.

**Implementation**:
```csharp
// Create world-space canvas as child of player
GameObject radialObject = new GameObject("DashRadial");
radialObject.transform.SetParent(HeroController.instance.transform);
radialObject.transform.localPosition = Vector3.zero;

Canvas worldCanvas = radialObject.AddComponent<Canvas>();
worldCanvas.renderMode = RenderMode.WorldSpace;
worldCanvas.sortingOrder = 100;

// Radial fill image
Image radialFill = CreateRadialImage();
radialFill.type = Image.Type.Filled;
radialFill.fillMethod = Image.FillMethod.Radial360;
radialFill.fillOrigin = Image.Origin360.Top;

// Update fill based on cooldown
void Update() {
    float cooldownPercent = GetDashCooldownPercent();
    radialFill.fillAmount = 1f - cooldownPercent;
    radialFill.color = cooldownPercent > 0 ? Color.red : Color.green;
}
```

**Display**: Semi-transparent ring around character. Fills clockwise as dash recharges. Pulses when ready.

**Configuration**:
- RadialSize: 0.5-2.0x character height
- Opacity: 10-100%
- PulseWhenReady: true/false
- HideWhenAvailable: Auto-hide after 0.5s

---

### 7. Rosary Flow Tracker
**Purpose**: Visualizes currency (rosary beads) income and expenditure rates. Shows death penalties and earning efficiency over time.

**Implementation**:
```csharp
public class RosaryFlow {
    private Queue<RosaryEvent> recentEvents = new Queue<RosaryEvent>();
    private float incomePerMinute = 0f;
    private float lossPerMinute = 0f;

    [HarmonyPatch(typeof(PlayerData), "AddRosaries")]
    static void Postfix(int amount) {
        recentEvents.Enqueue(new RosaryEvent {
            amount = amount,
            timestamp = Time.time,
            eventType = amount > 0 ? EventType.Gain : EventType.Loss
        });
        RecalculateRates();
    }

    void RecalculateRates() {
        float cutoff = Time.time - 60f;  // Last minute
        var validEvents = recentEvents.Where(e => e.timestamp > cutoff);
        incomePerMinute = validEvents.Where(e => e.eventType == EventType.Gain)
                                     .Sum(e => e.amount);
        lossPerMinute = Math.Abs(validEvents.Where(e => e.eventType == EventType.Loss)
                                            .Sum(e => e.amount));
    }
}
```

**Display**: Animated ticker showing +/- flow. Optional particle stream visualization or graph.

**Configuration**:
- DisplayMode: "Ticker", "Graph", "Particles"
- TimeWindow: 30s, 1min, 5min
- ShowDeathPenalties: Highlight in red
- NetIncomeOnly: Show only final rate

---

### 8. Breadcrumb Trail
**Purpose**: Shows fading trail of player's recent path. Helps identify movement patterns and backtracking routes.

**Implementation**:
```csharp
public class BreadcrumbTrail : MonoBehaviour {
    private List<TrailPoint> trail = new List<TrailPoint>();
    private LineRenderer lineRenderer;
    private float dropInterval = 0.1f;
    private float lastDropTime = 0f;

    void Start() {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
    }

    void Update() {
        if (Time.time - lastDropTime > dropInterval) {
            trail.Add(new TrailPoint {
                position = transform.position,
                timestamp = Time.time
            });
            lastDropTime = Time.time;

            // Remove old points
            trail.RemoveAll(p => Time.time - p.timestamp > trailDuration);

            // Update line renderer
            lineRenderer.positionCount = trail.Count;
            for (int i = 0; i < trail.Count; i++) {
                lineRenderer.SetPosition(i, trail[i].position);
                float alpha = 1f - (Time.time - trail[i].timestamp) / trailDuration;
                lineRenderer.startColor = new Color(1, 1, 1, alpha);
            }
        }
    }
}
```

**Display**: Fading line behind player. Color shifts based on speed or state (combat/exploration).

**Configuration**:
- TrailDuration: 3-30 seconds
- DropFrequency: 0.05-0.5s intervals
- ColorMode: "Speed", "State", "Height"
- FadeStyle: Linear, Exponential

---

### 9. Elevation Profile Chart
**Purpose**: Mini graph showing height changes over time. Useful for understanding verticality in routes and fall distances.

**Implementation**:
```csharp
public class ElevationProfiler {
    private CircularBuffer<float> heightHistory = new CircularBuffer<float>(200);
    private float sampleInterval = 0.1f;
    private Texture2D graphTexture;

    void Start() {
        graphTexture = new Texture2D(200, 100);
    }

    void Update() {
        heightHistory.Add(HeroController.instance.transform.position.y);
        UpdateGraph();
    }

    void UpdateGraph() {
        // Clear texture
        Color[] clearColors = new Color[graphTexture.width * graphTexture.height];
        graphTexture.SetPixels(clearColors);

        // Draw height line
        float minHeight = heightHistory.Min();
        float maxHeight = heightHistory.Max();
        float range = maxHeight - minHeight;

        for (int x = 0; x < heightHistory.Count - 1; x++) {
            float y1 = (heightHistory[x] - minHeight) / range * graphTexture.height;
            float y2 = (heightHistory[x + 1] - minHeight) / range * graphTexture.height;
            DrawLine(graphTexture, x, (int)y1, x + 1, (int)y2, Color.green);
        }

        graphTexture.Apply();
    }
}
```

**Display**: Small scrolling graph in corner. Shows last 20 seconds of elevation data.

**Configuration**:
- GraphWidth: 100-400 pixels
- TimeWindow: 10-60 seconds
- ShowFallDamageZones: Red areas for dangerous drops
- MarkBenches: Icons at rest heights

---

### 10. Attempt Heatmap
**Purpose**: Visualizes death clustering in rooms to identify problem areas. Exports data for route analysis.

**Implementation**:
```csharp
public class AttemptHeatmap {
    private Dictionary<string, List<DeathPoint>> deathsByRoom;
    private Texture2D heatmapTexture;

    [HarmonyPatch(typeof(HeroController), "Die")]
    static void Postfix() {
        string room = SceneManager.GetActiveScene().name;
        Vector2 position = HeroController.instance.transform.position;

        if (!deathsByRoom.ContainsKey(room))
            deathsByRoom[room] = new List<DeathPoint>();

        deathsByRoom[room].Add(new DeathPoint {
            position = position,
            timestamp = DateTime.Now,
            bossName = GetCurrentBoss()  // null if not boss fight
        });

        SaveToJSON();
        GenerateHeatmap(room);
    }

    void GenerateHeatmap(string room) {
        // Create gaussian blur around death points
        foreach (var death in deathsByRoom[room]) {
            Vector2 screenPos = WorldToMinimapPosition(death.position);
            DrawGaussianBlob(heatmapTexture, screenPos, intensity: 1.0f);
        }
    }
}
```

**Display**: Overlay on pause menu minimap. Red intensity = death frequency.

**Configuration**:
- HeatmapOpacity: 20-80%
- BlurRadius: 5-20 pixels
- ColorGradient: Red-Yellow-White
- ExportFormat: JSON, CSV, PNG heatmap

**Build**: `dotnet build -c Release && copy bin/Release/*.dll "../BepInEx/plugins/"`

---

## B-Tier Mods (Specialized but Valuable)

### 11. Jump Arc Predictor
**Purpose**: Shows parabolic trajectory preview for jumps. Helps learn new skip routes and precise platforming.

**Implementation**:
```csharp
public class JumpArcPredictor {
    private LineRenderer arcRenderer;
    private int predictionSteps = 30;
    private float timeStep = 0.1f;

    void PredictArc() {
        Vector2 position = rb2d.position;
        Vector2 velocity = rb2d.velocity;
        Vector2 gravity = Physics2D.gravity * rb2d.gravityScale;

        Vector3[] points = new Vector3[predictionSteps];

        for (int i = 0; i < predictionSteps; i++) {
            float t = i * timeStep;
            points[i] = position + velocity * t + 0.5f * gravity * t * t;

            // Check for collision
            RaycastHit2D hit = Physics2D.Raycast(position, points[i] - (Vector3)position);
            if (hit.collider != null) {
                Array.Resize(ref points, i + 1);
                points[i] = hit.point;
                break;
            }
        }

        arcRenderer.positionCount = points.Length;
        arcRenderer.SetPositions(points);
    }
}
```

**Display**: Dotted arc showing next 3 seconds of trajectory. Red when collision detected.

**Configuration**:
- PredictionTime: 1-5 seconds
- ShowOnlyWhileJumping: true/false
- CollisionDetection: Precise or Fast
- ArcStyle: Dots, Solid, Gradient

---

### 12. Perfect Parry Window Flash
**Purpose**: Visual indicator for frame-perfect counter opportunities. Flashes screen edge when parry timing is optimal.

**Implementation**:
```csharp
[HarmonyPatch(typeof(EnemyAttack), "WindupComplete")]
static void Postfix(EnemyAttack __instance) {
    // Enemy attack is about to hit - this is parry window
    float distance = Vector2.Distance(__instance.transform.position,
                                     HeroController.instance.transform.position);

    if (distance < parryRange) {
        float timeToImpact = distance / __instance.attackSpeed;
        ParryIndicator.FlashInTime(timeToImpact - parryWindowOffset);
    }
}

public class ParryIndicator {
    public static void FlashInTime(float delay) {
        instance.StartCoroutine(FlashCoroutine(delay));
    }

    IEnumerator FlashCoroutine(float delay) {
        yield return new WaitForSeconds(delay);

        // Flash screen edges
        screenBorder.color = Color.white;
        yield return new WaitForSeconds(0.1f);
        screenBorder.color = Color.clear;
    }
}
```

**Display**: Screen edge flash or character outline glow during parry windows.

**Configuration**:
- FlashColor: White, Blue, Custom
- FlashDuration: 50-200ms
- EarlyWarning: Show approach indicator
- AudioCue: Optional sound effect

---

### 13. Tool/Buff Timeline
**Purpose**: Horizontal bars showing active buffs, debuffs, and tool durations with time remaining.

**Implementation**:
```csharp
public class BuffTimeline {
    private List<ActiveBuff> buffs = new List<ActiveBuff>();

    [HarmonyPatch(typeof(BuffManager), "ApplyBuff")]
    static void Postfix(BuffType type, float duration) {
        buffs.Add(new ActiveBuff {
            type = type,
            startTime = Time.time,
            duration = duration,
            icon = GetBuffIcon(type)
        });
    }

    void OnGUI() {
        float y = 10;
        foreach (var buff in buffs) {
            float remaining = buff.duration - (Time.time - buff.startTime);
            if (remaining <= 0) continue;

            float fillPercent = remaining / buff.duration;

            // Draw bar
            GUI.DrawTexture(new Rect(10, y, 200 * fillPercent, 20), barTexture);

            // Draw icon
            GUI.DrawTexture(new Rect(10, y, 20, 20), buff.icon);

            // Draw timer
            GUI.Label(new Rect(220, y, 50, 20), $"{remaining:F1}s");

            y += 25;
        }
    }
}
```

**Display**: Stacked horizontal bars with icons. Bars shrink as time expires.

**Configuration**:
- BarPosition: Top, Bottom, Side
- ShowNegativeEffects: Separate color
- CompactMode: Combine similar buffs
- WarningThreshold: Flash when < 2s

---

### 14. Heal Safety Predictor
**Purpose**: Analyzes enemy positions and AI states to determine if it's safe to heal/bind.

**Implementation**:
```csharp
public class HealSafetyAnalyzer {
    private bool IsSafeToHeal() {
        // Find all nearby enemies
        Collider2D[] enemies = Physics2D.OverlapCircleAll(
            HeroController.instance.transform.position,
            dangerRadius,
            enemyLayer
        );

        foreach (var enemy in enemies) {
            EnemyAI ai = enemy.GetComponent<EnemyAI>();

            // Check if enemy is in attack state
            if (ai.currentState == AIState.Attacking ||
                ai.currentState == AIState.Pursuing) {
                return false;
            }

            // Check if enemy can reach us during heal
            float timeToReach = Vector2.Distance(enemy.transform.position,
                                               transform.position) / ai.moveSpeed;
            if (timeToReach < HEAL_DURATION) {
                return false;
            }
        }

        return true;
    }
}
```

**Display**: Green/red indicator near health bar. Optional danger zones overlay.

**Configuration**:
- DangerRadius: 5-15 units
- PredictionDepth: Simple or AI-aware
- ShowDangerZones: Visual ranges
- ConsiderProjectiles: Track bullets

---

### 15. Boss Stagger Meter
**Purpose**: Shows hidden stagger/posture meter for bosses (not health). Indicates when boss will be stunned.

**Implementation**:
```csharp
[HarmonyPatch(typeof(BossController), "TakeHit")]
static void Postfix(BossController __instance, int damage) {
    // Most bosses have hidden stagger value
    float stagger = Traverse.Create(__instance).Field("staggerValue").GetValue<float>();
    float maxStagger = Traverse.Create(__instance).Field("maxStagger").GetValue<float>();

    StaggerDisplay.UpdateBossStagger(__instance.bossName, stagger, maxStagger);
}

public class StaggerDisplay {
    private static Dictionary<string, BossStaggerInfo> staggerMeters;

    public static void UpdateBossStagger(string boss, float current, float max) {
        if (!staggerMeters.ContainsKey(boss)) {
            staggerMeters[boss] = new BossStaggerInfo();
        }

        staggerMeters[boss].current = current;
        staggerMeters[boss].max = max;
        staggerMeters[boss].lastUpdate = Time.time;
    }
}
```

**Display**: Separate bar under boss health. Yellow fill with flash at max.

**Configuration**:
- BarStyle: Sekiro-like, Bloodborne-like
- ShowNumbers: Display as percentage
- DecayIndicator: Show recovery rate
- PredictStun: Flash before threshold

---

## C-Tier Mods (Nice-to-Have QoL)

### 16. Room Completion Widget
**Purpose**: Shows collectibles and secrets found in current room as percentage.

**Implementation**:
```csharp
[HarmonyPatch(typeof(SceneManager), "OnSceneLoaded")]
static void Postfix(Scene scene) {
    // Parse scene for collectibles
    var collectibles = GameObject.FindGameObjectsWithTag("Collectible");
    var secrets = GameObject.FindGameObjectsWithTag("Secret");

    int found = 0;
    int total = collectibles.Length + secrets.Length;

    foreach (var item in collectibles) {
        if (SaveData.HasCollected(item.name)) found++;
    }

    CompletionWidget.ShowForRoom(scene.name, found, total);
}
```

**Display**: Small "2/5 Secrets" text that fades after entering room.

**Configuration**:
- DisplayDuration: 2-10 seconds
- ShowOnlyIncomplete: Hide 100% rooms
- IconMode: Show item types
- PersistentMode: Always visible

---

### 17. Secret Proximity Sensor
**Purpose**: Pulses or glows when near breakable walls or hidden passages.

**Implementation**:
```csharp
public class SecretDetector {
    private float pulseRate = 0f;

    void Update() {
        // Find nearby secret markers
        Collider2D[] secrets = Physics2D.OverlapCircleAll(
            transform.position,
            detectionRadius,
            secretLayer
        );

        if (secrets.Length > 0) {
            float closestDistance = secrets.Min(s =>
                Vector2.Distance(s.transform.position, transform.position));

            // Pulse faster when closer
            pulseRate = Mathf.Lerp(0.5f, 3f, 1f - (closestDistance / detectionRadius));
            VibrateController(pulseRate);
        }
    }
}
```

**Display**: Screen edge pulse or controller vibration. Intensity based on proximity.

**Configuration**:
- DetectionRadius: 3-10 units
- IndicatorType: Visual, Haptic, Audio
- SensitivityCurve: Linear, Exponential
- FilterDestructibles: true/false

---

### 18. Fast Travel Network Map
**Purpose**: Overlay showing bench connections and optimal travel routes.

**Implementation (External)**:
```csharp
// Static data - no game hooks needed
public class TravelNetwork {
    private Dictionary<string, List<string>> connections = LoadFromJSON();

    public Path GetShortestPath(string from, string to) {
        // Dijkstra's algorithm
        var distances = new Dictionary<string, float>();
        var previous = new Dictionary<string, string>();
        var queue = new PriorityQueue<string>();

        // ... pathfinding implementation

        return ReconstructPath(previous, to);
    }
}
```

**Display**: Minimap overlay with node graph. Highlights suggested route.

**Configuration**:
- ShowAllBenches: Or only discovered
- RouteMode: Fastest, Safest, Scenic
- AutoSuggest: Based on objectives
- ExportRoutes: Share with community

---

### 19. Shop Spend Planner
**Purpose**: Calculates optimal purchase order for shop upgrades based on route.

**Implementation**:
```csharp
public class ShopOptimizer {
    public PurchasePlan OptimizeRoute(int currentRosaries, List<ShopItem> available) {
        // Dynamic programming solution
        var dp = new int[currentRosaries + 1];
        var items = new List<ShopItem>[currentRosaries + 1];

        foreach (var item in available) {
            for (int cost = currentRosaries; cost >= item.price; cost--) {
                int value = dp[cost - item.price] + item.routeValue;
                if (value > dp[cost]) {
                    dp[cost] = value;
                    items[cost] = new List<ShopItem>(items[cost - item.price] ?? new());
                    items[cost].Add(item);
                }
            }
        }

        return new PurchasePlan { items = items[currentRosaries] };
    }
}
```

**Display**: Checklist overlay in shops. Shows priority items for current route.

**Configuration**:
- RouteProfile: Any%, 100%, Low%
- ValueWeights: Custom priorities
- BudgetMode: Current or projected
- CompareOptions: Show alternatives

---

### 20. Stream-Safe Compact HUD
**Purpose**: One-button toggle to minimal UI for streaming. Preserves screen space.

**Implementation**:
```csharp
public class CompactHUD {
    private bool compactMode = false;
    private Dictionary<GameObject, Vector3> originalPositions;

    void ToggleCompact() {
        compactMode = !compactMode;

        if (compactMode) {
            // Move UI elements to edges
            HealthBar.transform.position = new Vector3(10, Screen.height - 30, 0);
            HealthBar.transform.localScale *= 0.7f;

            SilkMeter.SetActive(false);  // Hide non-essential

            // Add margin for webcam
            Canvas.SetMargin(100, 100, 200, 150);
        } else {
            RestoreOriginalLayout();
        }
    }
}
```

**Display**: Compressed UI elements at screen edges. Webcam-safe zones.

**Configuration**:
- WebcamPosition: 9 positions
- ElementsToHide: Checklist
- ScaleFactor: 50-100%
- Hotkey: Single key toggle

---

## Accessibility & Polish Mods

### 21. Colorblind Palette Presets
**Purpose**: Swaps colors for deuteranopia, protanopia, and tritanopia visibility.

**Implementation**:
```csharp
public class ColorblindMode {
    private ColorGradingModel.Settings original;

    void ApplyColorblindFilter(ColorblindType type) {
        var postProcess = Camera.main.GetComponent<PostProcessingBehaviour>();
        var colorGrading = postProcess.profile.colorGrading.settings;

        switch (type) {
            case ColorblindType.Deuteranopia:
                colorGrading.basic.saturation = 0.8f;
                colorGrading.channelMixer.red = new Vector3(0.8f, 0.2f, 0);
                colorGrading.channelMixer.green = new Vector3(0.258f, 0.742f, 0);
                break;
            case ColorblindType.Protanopia:
                colorGrading.channelMixer.red = new Vector3(0.567f, 0.433f, 0);
                colorGrading.channelMixer.green = new Vector3(0.558f, 0.442f, 0);
                break;
        }

        postProcess.profile.colorGrading.settings = colorGrading;
    }
}
```

**Display**: Full-screen color correction. Instant toggle.

**Configuration**:
- FilterType: 3 colorblind types + custom
- Intensity: 50-100% correction
- HighContrastMode: Extra borders
- PatternOverlays: For critical elements

---

### 22. High-Contrast Glyph Mode
**Purpose**: Replaces detailed icons with bold, simple symbols for clarity.

**Implementation**:
```csharp
[HarmonyPatch(typeof(UIIconLoader), "GetIcon")]
static bool Prefix(string iconName, ref Sprite __result) {
    if (!HighContrastMode.enabled) return true;

    // Replace with high contrast version
    __result = HighContrastMode.GetGlyphForIcon(iconName);
    return false;  // Skip original method
}

public static class HighContrastMode {
    private static Dictionary<string, Sprite> glyphMap = new() {
        ["health"] = LoadGlyph("heart_bold"),
        ["silk"] = LoadGlyph("thread_bold"),
        ["rosary"] = LoadGlyph("coin_bold"),
        // ...
    };
}
```

**Display**: Bold geometric shapes replace detailed art. Black/white only.

**Configuration**:
- GlyphSet: Geometric, Letters, Numbers
- Size: 1-3x normal
- Outline: Thickness 0-5px
- AnimateChanges: Pulse on update

---

### 23. Minimalist Pace Bar
**Purpose**: Tiny ahead/behind indicator without numbers. Pattern-based for colorblind users.

**Implementation**:
```csharp
public class MinimalPaceBar {
    void OnGUI() {
        float diff = currentTime - comparisonTime;

        // Just 5 segments: far behind, behind, even, ahead, far ahead
        int segment = Mathf.Clamp(Mathf.RoundToInt(diff / 5f) + 2, 0, 4);

        string[] patterns = { "▓▓░░░", "▓▓▓░░", "▓▓▓▓░", "▓▓▓▓▓", "▓▓▓▓▓" };
        string[] colors = { "red", "orange", "yellow", "lime", "green" };

        GUI.Label(new Rect(Screen.width/2 - 25, 10, 50, 20),
                  $"<color={colors[segment]}>{patterns[segment]}</color>");
    }
}
```

**Display**: 5-segment bar using patterns, not just color. Center screen.

**Configuration**:
- PatternStyle: Blocks, Arrows, Dots
- Position: 9 screen positions
- ThresholdSeconds: Sensitivity
- PulseOnPB: Animation when ahead

---

### 24. Data Export Bus
**Purpose**: Streams game metrics to external tools (LiveSplit, OBS, Discord, spreadsheets).

**Implementation**:
```csharp
public class DataExportBus {
    private TcpListener tcpListener;
    private List<StreamWriter> clients = new List<StreamWriter>();

    void Start() {
        tcpListener = new TcpListener(IPAddress.Any, 9090);
        tcpListener.Start();
        AcceptClients();
    }

    public void BroadcastMetric(string metric, object value) {
        string json = JsonConvert.SerializeObject(new {
            metric = metric,
            value = value,
            timestamp = DateTime.UtcNow.ToUnixTime()
        });

        // Write to all connected clients
        foreach (var client in clients) {
            client.WriteLine(json);
            client.Flush();
        }

        // Also write to file for offline analysis
        File.AppendAllText("metrics.jsonl", json + "\n");
    }
}

// Usage throughout other mods
DataExportBus.Instance.BroadcastMetric("player.health", health);
DataExportBus.Instance.BroadcastMetric("room.entered", sceneName);
DataExportBus.Instance.BroadcastMetric("boss.damaged", damageDealt);
```

**Export Formats**:
- NDJSON stream (newline-delimited JSON)
- CSV with rotating files
- WebSocket for browser overlays
- Named pipes for LiveSplit

**Configuration**:
- Port: TCP port number
- BufferSize: Events before flush
- Metrics: Whitelist/blacklist
- Compression: gzip for large streams

---

### 25. Coyote Time Visualizer
**Purpose**: Shows remaining grace period for late jumps after leaving platforms.

**Implementation**:
```csharp
public class CoyoteTimeVisualizer {
    private float coyoteTimeRemaining = 0f;
    private bool showingCoyoteTime = false;

    [HarmonyPatch(typeof(HeroController), "Update")]
    static void Postfix(HeroController __instance) {
        bool grounded = __instance.cState.onGround;

        if (grounded) {
            coyoteTimeRemaining = COYOTE_TIME_DURATION;
        } else if (coyoteTimeRemaining > 0) {
            coyoteTimeRemaining -= Time.deltaTime;
            showingCoyoteTime = true;
        } else {
            showingCoyoteTime = false;
        }
    }

    void OnGUI() {
        if (!showingCoyoteTime) return;

        // Draw shrinking bar under character
        Vector3 screenPos = Camera.main.WorldToScreenPoint(
            HeroController.instance.transform.position);

        float barWidth = 50f * (coyoteTimeRemaining / COYOTE_TIME_DURATION);
        GUI.Box(new Rect(screenPos.x - 25, Screen.height - screenPos.y + 20,
                         barWidth, 5), "");
    }
}
```

**Display**: Shrinking bar under character when grace period active. Red when almost expired.

**Configuration**:
- BarStyle: Horizontal, Circular, Fade
- ShowAlways: Or only when relevant
- ColorGradient: Green to red
- AudioCue: Tick when expiring

---

## Build and Installation

### Standard Build Process

All mods use the same basic build process:

```bash
# Navigate to mod directory
cd HKSS-[ModName]

# Restore NuGet packages
dotnet restore

# Build in release mode
dotnet build -c Release

# Output will be in bin/Release/net[version]/
# Copy DLL to game directory
cp bin/Release/net*/HKSS-[ModName].dll "../Hollow Knight Silksong/BepInEx/plugins/"
```

### Project Structure Template

Each mod follows this structure:
```
HKSS-[ModName]/
├── HKSS-[ModName].csproj      # Project file with dependencies
├── [ModName]Plugin.cs          # BepInEx plugin entry point
├── [ModName]Display.cs         # UI/rendering logic
├── [ModName]Patches.cs         # Harmony patches
├── Configuration.cs            # Config bindings
└── README.md                   # Mod-specific documentation
```

### Common Dependencies

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="BepInEx.Core" Version="5.*" />
    <PackageReference Include="BepInEx.PluginInfoProps" Version="2.*" />
    <PackageReference Include="UnityEngine.Modules" Version="6000.0.50" IncludeAssets="compile" />
    <PackageReference Include="Silksong.GameLibs" Version="1.0.1-silksong1.0.28324"/>
  </ItemGroup>
</Project>
```

### Testing Commands

```bash
# Run game with BepInEx console
cd "Hollow Knight Silksong"
./run_bepinex_verbose.sh

# Monitor BepInEx log
tail -f BepInEx/LogOutput.log

# Test specific mod in isolation
./test_mod.sh HKSS-VelocityVector

# Profile performance impact
dotnet trace collect --process-id $(pidof "Hollow Knight Silksong")
```

## Performance Guidelines

### Do's
- Use object pooling for frequently created objects (damage numbers, trail points)
- Cache component references in Awake()
- Use Unity job system for heavy calculations
- Batch GUI draw calls
- Limit Update() logic to essential per-frame operations

### Don'ts
- Don't use GetComponent in Update loops
- Don't create new objects every frame
- Don't use LINQ in performance-critical paths
- Don't hook methods called hundreds of times per frame
- Don't use reflection in hot paths

### Profiling
```csharp
// Add performance monitoring to any mod
public class PerformanceMonitor {
    private Stopwatch frameTimer = new Stopwatch();

    void Update() {
        frameTimer.Restart();

        // Mod logic here
        ActualModUpdate();

        frameTimer.Stop();
        if (frameTimer.ElapsedMilliseconds > 1) {
            Logger.LogWarning($"Frame took {frameTimer.ElapsedMilliseconds}ms!");
        }
    }
}
```

## Configuration Best Practices

All mods should use BepInEx configuration system:

```csharp
public class ModConfig {
    private ConfigFile config;

    // Define all config entries
    public ConfigEntry<bool> Enabled { get; private set; }
    public ConfigEntry<KeyCode> ToggleKey { get; private set; }
    public ConfigEntry<float> UpdateInterval { get; private set; }

    public ModConfig(ConfigFile cfg) {
        config = cfg;

        Enabled = config.Bind("General", "Enabled", true,
            "Enable or disable the mod");

        ToggleKey = config.Bind("Hotkeys", "ToggleKey", KeyCode.F1,
            "Key to toggle mod on/off");

        UpdateInterval = config.Bind("Performance", "UpdateInterval", 0.1f,
            new ConfigDescription("How often to update (seconds)",
                new AcceptableValueRange<float>(0.01f, 1f)));
    }
}
```

## Legal and Competitive Considerations

### External Mods (Leaderboard Legal)
- Must not modify game memory
- Read-only access via memory scanning
- No gameplay advantages
- Purely informational displays
- Can connect to LiveSplit for official timing

### Injected Mods (Practice Only)
- Full game modification capability
- Not allowed in competitive runs
- Marked as "Practice Tool" in mod descriptions
- Should include warning in UI
- Can modify game behavior for training

### Disclaimer Template
```csharp
[BepInPlugin("com.author.modname", "Mod Name [PRACTICE TOOL]", "1.0.0")]
[BepInIncompatibility("com.speedrun.timer")]  // Incompatible with official timer
public class ModPlugin : BaseUnityPlugin {
    void Awake() {
        if (IsCompetitiveMode()) {
            Logger.LogError("This mod is not allowed in competitive play!");
            enabled = false;
            return;
        }
    }
}
```

## Community and Support

### Mod Submission Checklist
- [ ] Follows naming convention: HKSS-[ModName]
- [ ] Includes comprehensive README
- [ ] Configuration file with sensible defaults
- [ ] Performance tested (< 1ms per frame impact)
- [ ] Error handling for missing game components
- [ ] Version compatible with latest Silksong patch
- [ ] Screenshots/GIFs demonstrating functionality
- [ ] Credits for any borrowed code
- [ ] License file (MIT recommended)

### Common Issues and Solutions

**Issue**: Mod doesn't appear in BepInEx
**Solution**: Check [BepInPlugin] attribute and ensure DLL is in plugins folder

**Issue**: NullReferenceException on game start
**Solution**: Add null checks and wait for scene load:
```csharp
IEnumerator WaitForInit() {
    while (HeroController.instance == null)
        yield return new WaitForSeconds(0.1f);

    // Now safe to initialize
    Initialize();
}
```

**Issue**: Performance degradation
**Solution**: Profile with Unity Profiler, reduce Update frequency, use coroutines

**Issue**: Incompatible with other mods
**Solution**: Use [BepInDependency] and [BepInIncompatibility] attributes

### Version History Tracking

Each mod should maintain a changelog:
```markdown
## Version 1.1.0
- Added colorblind mode support
- Fixed performance issue with large room tracking
- Improved configuration options

## Version 1.0.0
- Initial release
- Basic functionality implemented
```

---

## Implementation Priority Matrix

| Week | Mods to Implement | Complexity | Track |
|------|-------------------|------------|-------|
| 1 | Velocity HUD, Air Time Counter, Elevation Chart, Stream-Safe HUD | Easy | Both |
| 2 | Room Timer, Input Timeline, Breadcrumb Trail, Data Export | Medium | Both |
| 3 | Silk Analytics, Rosary Flow, Dash Radial, Attempt Heatmap | Medium | Injected |
| 4 | Jump Arc, Parry Window, Buff Timeline, Heal Safety | Hard | Injected |
| 5 | Boss Stagger, Room Completion, Secret Sensor, Travel Map | Medium | Mixed |
| 6 | Shop Planner, Colorblind, Glyph Mode, Pace Bar, Coyote Time | Easy-Medium | Mixed |

---

This document represents the complete specification for all 25 Hollow Knight: Silksong info display mods. Each mod has been designed for practical implementation using the BepInEx framework and standard Unity/C# patterns. Follow the build instructions and guidelines above to create any of these mods.

For questions or contributions, reference this document as the authoritative source for the project's technical direction and implementation standards.