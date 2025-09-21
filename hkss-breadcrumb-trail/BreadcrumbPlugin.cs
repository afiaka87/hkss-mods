using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;

namespace HKSS.BreadcrumbTrail
{
    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.hkss.breadcrumbtrail";
        public const string PLUGIN_NAME = "Breadcrumb Trail";
        public const string PLUGIN_VERSION = "0.1.1";
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInProcess("Hollow Knight Silksong.exe")]
    public class BreadcrumbPlugin : BaseUnityPlugin
    {
        public static ManualLogSource ModLogger { get; private set; }
        public static BreadcrumbPlugin Instance { get; private set; }

        private Harmony harmony;
        private GameObject trailObject;
        private SceneAnalyzer sceneAnalyzer;

        public ConfigEntry<bool> Enabled { get; private set; }
        public ConfigEntry<float> TrailDuration { get; private set; }
        public ConfigEntry<float> DropFrequency { get; private set; }
        public ConfigEntry<ColorMode> TrailColorMode { get; private set; }
        public ConfigEntry<FadeStyle> TrailFadeStyle { get; private set; }
        public ConfigEntry<float> TrailWidth { get; private set; }
        public ConfigEntry<Color> BaseColor { get; private set; }
        public ConfigEntry<Color> SpeedColor { get; private set; }
        public ConfigEntry<Color> CombatColor { get; private set; }
        public ConfigEntry<bool> ShowInCombat { get; private set; }
        public ConfigEntry<int> MaxPoints { get; private set; }

        // Performance Optimization Settings
        public ConfigEntry<bool> EnableOptimizations { get; private set; }
        public ConfigEntry<float> AngleCullThreshold { get; private set; }
        public ConfigEntry<float> MinPointDistance { get; private set; }
        public ConfigEntry<bool> UseAdaptiveSampling { get; private set; }
        public ConfigEntry<bool> EnableFrustumCulling { get; private set; }
        public ConfigEntry<bool> EnableLOD { get; private set; }
        public ConfigEntry<float> LODNearDistance { get; private set; }
        public ConfigEntry<float> LODFarDistance { get; private set; }
        public ConfigEntry<bool> UseBatchedRenderer { get; private set; }
        public ConfigEntry<bool> ShowOptimizationStats { get; private set; }
        public ConfigEntry<KeyCode> ToggleKey { get; private set; }

        // Runtime state
        public bool TrailVisible { get; set; } = true;

        void Awake()
        {
            try
            {
                Instance = this;
                ModLogger = Logger;

                Logger.LogInfo("========================================");
                Logger.LogInfo("[BreadcrumbPlugin] STARTING INITIALIZATION");
                Logger.LogInfo("========================================");

                // Initialize config with error handling
                try
                {
                    InitializeConfig();
                    Logger.LogInfo("[BreadcrumbPlugin] ✓ Config initialized successfully");
                    Logger.LogInfo($"  - Enabled: {Enabled.Value}");
                    Logger.LogInfo($"  - TrailDuration: {TrailDuration.Value}");
                    Logger.LogInfo($"  - MaxPoints: {MaxPoints.Value}");
                }
                catch (Exception configEx)
                {
                    Logger.LogError($"[BreadcrumbPlugin] ✗ Config initialization failed: {configEx.Message}");
                    Logger.LogError($"  Stack: {configEx.StackTrace}");
                    return;
                }

                if (!Enabled.Value)
                {
                    Logger.LogInfo("[BreadcrumbPlugin] Breadcrumb Trail is disabled in config - exiting");
                    return;
                }

                // Create trail object FIRST before any patches
                try
                {
                    Logger.LogInfo("[BreadcrumbPlugin] Creating trail GameObject...");
                    CreateTrailObject();
                    Logger.LogInfo($"[BreadcrumbPlugin] ✓ Trail object created: {trailObject != null}");

                    if (trailObject != null)
                    {
                        var breadcrumb = trailObject.GetComponent<BreadcrumbTrail>();
                        Logger.LogInfo($"  - BreadcrumbTrail component: {breadcrumb != null}");
                        Logger.LogInfo($"  - GameObject name: {trailObject.name}");
                        Logger.LogInfo($"  - Is active: {trailObject.activeSelf}");
                    }
                }
                catch (Exception trailEx)
                {
                    Logger.LogError($"[BreadcrumbPlugin] ✗ Trail object creation failed: {trailEx.Message}");
                    Logger.LogError($"  Stack: {trailEx.StackTrace}");
                    // Continue anyway - maybe patches will work
                }

                // Apply harmony patches with detailed error handling
                try
                {
                    Logger.LogInfo("[BreadcrumbPlugin] Applying Harmony patches...");
                    harmony = new Harmony(PluginInfo.PLUGIN_GUID);

                    // Manually patch to control error handling
                    var assembly = typeof(BreadcrumbPlugin).Assembly;
                    var types = assembly.GetTypes();

                    foreach (var type in types)
                    {
                        try
                        {
                            if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0)
                            {
                                Logger.LogInfo($"  - Patching type: {type.FullName}");
                                harmony.CreateClassProcessor(type).Patch();
                                Logger.LogInfo($"    ✓ Successfully patched");
                            }
                        }
                        catch (Exception patchEx)
                        {
                            Logger.LogError($"  ✗ Failed to patch {type.FullName}: {patchEx.Message}");
                            // Continue with other patches
                        }
                    }

                    Logger.LogInfo("[BreadcrumbPlugin] ✓ Harmony patch process completed");
                }
                catch (Exception harmonyEx)
                {
                    Logger.LogError($"[BreadcrumbPlugin] ✗ Harmony patching failed: {harmonyEx.Message}");
                    Logger.LogError($"  Stack: {harmonyEx.StackTrace}");
                    // Continue anyway - mod might still work without patches
                }

                // Initialize scene analyzer
                try
                {
                    sceneAnalyzer = new SceneAnalyzer();
                    sceneAnalyzer.StartAnalysis();
                    Logger.LogInfo("[BreadcrumbPlugin] ✓ Scene analyzer started");
                }
                catch (Exception analyzerEx)
                {
                    Logger.LogError($"[BreadcrumbPlugin] ✗ Scene analyzer failed: {analyzerEx.Message}");
                    // Non-critical, continue
                }

                Logger.LogInfo("========================================");
                Logger.LogInfo($"[BreadcrumbPlugin] Breadcrumb Trail v{PluginInfo.PLUGIN_VERSION} initialization complete!");
                Logger.LogInfo($"  Trail object exists: {trailObject != null}");
                Logger.LogInfo($"  Harmony instance: {harmony != null}");
                Logger.LogInfo($"  Scene analyzer: {sceneAnalyzer != null}");
                Logger.LogInfo("========================================");
            }
            catch (Exception ex)
            {
                Logger.LogError("========================================");
                Logger.LogError("[BreadcrumbPlugin] CRITICAL INITIALIZATION FAILURE");
                Logger.LogError($"Exception type: {ex.GetType().FullName}");
                Logger.LogError($"Message: {ex.Message}");
                Logger.LogError($"Stack trace:\n{ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    Logger.LogError($"Inner exception: {ex.InnerException.Message}");
                    Logger.LogError($"Inner stack:\n{ex.InnerException.StackTrace}");
                }

                Logger.LogError($"Current state:");
                Logger.LogError($"  - Instance set: {Instance != null}");
                Logger.LogError($"  - ModLogger set: {ModLogger != null}");
                Logger.LogError($"  - Config initialized: {Enabled != null}");
                Logger.LogError($"  - Trail object: {trailObject != null}");
                Logger.LogError($"  - Harmony: {harmony != null}");
                Logger.LogError("========================================");
            }
        }

        void OnDestroy()
        {
            sceneAnalyzer?.StopAnalysis();
            harmony?.UnpatchSelf();
            if (trailObject != null)
            {
                Destroy(trailObject);
            }
        }

        private void InitializeConfig()
        {
            Enabled = Config.Bind("General", "Enabled", true,
                "Enable or disable the breadcrumb trail");

            TrailDuration = Config.Bind("Trail", "Duration", 1800f,
                new ConfigDescription("How long trail points remain visible (seconds)",
                new AcceptableValueRange<float>(60f, 7200f))); // 1 minute to 2 hours

            DropFrequency = Config.Bind("Trail", "DropFrequency", 0.1f,
                new ConfigDescription("How often to drop trail points (seconds)",
                new AcceptableValueRange<float>(0.05f, 0.5f)));

            TrailColorMode = Config.Bind("Display", "ColorMode", ColorMode.Speed,
                "How to color the trail (Speed, State, Height, Static)");

            TrailFadeStyle = Config.Bind("Display", "FadeStyle", FadeStyle.Linear,
                "How the trail fades over time");

            TrailWidth = Config.Bind("Display", "Width", 0.15f,
                new ConfigDescription("Width of the trail line",
                new AcceptableValueRange<float>(0.05f, 0.5f)));

            BaseColor = Config.Bind("Colors", "BaseColor", new Color(0.5f, 0.5f, 1f, 0.8f),
                "Default trail color");

            SpeedColor = Config.Bind("Colors", "SpeedColor", new Color(1f, 0.5f, 0.5f, 0.8f),
                "Color when moving fast");

            CombatColor = Config.Bind("Colors", "CombatColor", new Color(1f, 0.2f, 0.2f, 0.8f),
                "Color during combat");

            ShowInCombat = Config.Bind("Display", "ShowInCombat", true,
                "Whether to show trail during combat");

            MaxPoints = Config.Bind("Performance", "MaxPoints", 20000,
                new ConfigDescription("Maximum number of trail points per scene",
                new AcceptableValueRange<int>(1000, 100000))); // Allow up to 100k points per scene

            // Performance Optimization Settings
            EnableOptimizations = Config.Bind("Optimization", "EnableOptimizations", true,
                "Enable performance optimizations (point throttling, culling, etc.)");

            AngleCullThreshold = Config.Bind("Optimization", "AngleCullThreshold", 5f,
                new ConfigDescription("Minimum angle change (degrees) to add a new point",
                new AcceptableValueRange<float>(1f, 30f)));

            MinPointDistance = Config.Bind("Optimization", "MinPointDistance", 0.5f,
                new ConfigDescription("Minimum distance (units) between consecutive trail points",
                new AcceptableValueRange<float>(0.1f, 2f)));

            UseAdaptiveSampling = Config.Bind("Optimization", "UseAdaptiveSampling", true,
                "Dynamically adjust sampling rate based on movement complexity");

            EnableFrustumCulling = Config.Bind("Optimization", "EnableFrustumCulling", true,
                "Only render trail segments visible in camera view");

            EnableLOD = Config.Bind("Optimization", "EnableLOD", true,
                "Enable level-of-detail system for distant trail segments");

            LODNearDistance = Config.Bind("Optimization", "LODNearDistance", 10f,
                new ConfigDescription("Distance for full detail rendering",
                new AcceptableValueRange<float>(5f, 20f)));

            LODFarDistance = Config.Bind("Optimization", "LODFarDistance", 30f,
                new ConfigDescription("Distance for lowest detail rendering",
                new AcceptableValueRange<float>(20f, 100f)));

            UseBatchedRenderer = Config.Bind("Optimization", "UseBatchedRenderer", false,
                "Use custom batched mesh renderer instead of LineRenderer (experimental)");

            ShowOptimizationStats = Config.Bind("Optimization", "ShowOptimizationStats", false,
                "Display optimization statistics overlay");

            ToggleKey = Config.Bind("Controls", "ToggleKey", KeyCode.F2,
                "Key to toggle trail visibility on/off");
        }

        public void CreateTrailObject()
        {
            try
            {
                if (trailObject != null)
                {
                    Logger.LogInfo("[CreateTrailObject] Trail object already exists, skipping creation");
                    return;
                }

                Logger.LogInfo("[CreateTrailObject] Creating new GameObject...");
                trailObject = new GameObject("BreadcrumbTrail");
                Logger.LogInfo($"[CreateTrailObject] GameObject created: {trailObject != null}");

                Logger.LogInfo("[CreateTrailObject] Adding BreadcrumbTrail component...");
                var component = trailObject.AddComponent<BreadcrumbTrail>();
                Logger.LogInfo($"[CreateTrailObject] Component added: {component != null}");

                Logger.LogInfo("[CreateTrailObject] Setting DontDestroyOnLoad...");
                DontDestroyOnLoad(trailObject);

                Logger.LogInfo($"[CreateTrailObject] ✓ Trail object setup complete");
                Logger.LogInfo($"  - Name: {trailObject.name}");
                Logger.LogInfo($"  - Active: {trailObject.activeSelf}");
                Logger.LogInfo($"  - Component count: {trailObject.GetComponents<Component>().Length}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[CreateTrailObject] Failed to create trail object: {ex.Message}");
                Logger.LogError($"  Stack: {ex.StackTrace}");
                throw; // Re-throw to be caught by Awake
            }
        }

        public void DestroyTrailObject()
        {
            if (trailObject != null)
            {
                Destroy(trailObject);
                trailObject = null;
            }
        }
    }

    public enum ColorMode
    {
        Static,
        Speed,
        State,
        Height
    }

    public enum FadeStyle
    {
        Linear,
        Exponential,
        Stepped
    }

    // Scene Analysis System for understanding Silksong's scene management
    public class SceneAnalyzer
    {
        private List<SceneTransitionData> transitions = new List<SceneTransitionData>();
        private string currentSceneName = "";
        private DateTime sceneLoadTime;
        private string logPath;
        private StreamWriter logWriter;

        public class SceneTransitionData
        {
            public string FromScene { get; set; }
            public string ToScene { get; set; }
            public DateTime Timestamp { get; set; }
            public Vector3 PlayerPosition { get; set; }
            public string TransitionType { get; set; }
            public float SceneDuration { get; set; }
            public List<string> ActiveScenes { get; set; }
            public Dictionary<string, object> SceneMetadata { get; set; }
        }

        public void StartAnalysis()
        {
            BreadcrumbPlugin.ModLogger?.LogInfo("[SceneAnalyzer] Starting scene system analysis...");

            // Create log directory
            string modDir = Path.GetDirectoryName(typeof(BreadcrumbPlugin).Assembly.Location);
            string logDir = Path.Combine(modDir, "SceneLogs");
            Directory.CreateDirectory(logDir);

            logPath = Path.Combine(logDir, $"scene_analysis_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            logWriter = new StreamWriter(logPath, true);
            logWriter.WriteLine($"=== Silksong Scene Analysis Started: {DateTime.Now} ===");
            logWriter.WriteLine($"Unity Version: {Application.unityVersion}");
            logWriter.WriteLine($"Platform: {Application.platform}");
            logWriter.Flush();

            // Hook into Unity's scene management
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;

            // Log current state
            LogCurrentSceneState();
        }

        public void StopAnalysis()
        {
            BreadcrumbPlugin.ModLogger?.LogInfo("[SceneAnalyzer] Stopping scene analysis...");

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;

            // Write transition summary
            WriteSummary();

            logWriter?.Close();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            string logMsg = $"[SCENE_LOADED] Name: {scene.name}, Path: {scene.path}, BuildIndex: {scene.buildIndex}, Mode: {mode}";
            BreadcrumbPlugin.ModLogger?.LogInfo(logMsg);
            logWriter?.WriteLine($"{DateTime.Now:HH:mm:ss.fff} - {logMsg}");

            // Log scene details
            LogSceneDetails(scene);

            // Track transition
            var transition = new SceneTransitionData
            {
                FromScene = currentSceneName,
                ToScene = scene.name,
                Timestamp = DateTime.Now,
                TransitionType = mode.ToString(),
                SceneDuration = currentSceneName != "" ? (float)(DateTime.Now - sceneLoadTime).TotalSeconds : 0,
                ActiveScenes = GetAllActiveScenes()
            };

            // Try to get player position
            if (HeroController.instance != null)
            {
                transition.PlayerPosition = HeroController.instance.transform.position;
                logWriter?.WriteLine($"  Player Position: {transition.PlayerPosition}");
            }

            transitions.Add(transition);
            currentSceneName = scene.name;
            sceneLoadTime = DateTime.Now;

            logWriter?.Flush();
        }

        private void OnSceneUnloaded(Scene scene)
        {
            string logMsg = $"[SCENE_UNLOADED] Name: {scene.name}";
            BreadcrumbPlugin.ModLogger?.LogInfo(logMsg);
            logWriter?.WriteLine($"{DateTime.Now:HH:mm:ss.fff} - {logMsg}");
            logWriter?.Flush();
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene newScene)
        {
            string logMsg = $"[ACTIVE_SCENE_CHANGED] From: {previousScene.name} To: {newScene.name}";
            BreadcrumbPlugin.ModLogger?.LogInfo(logMsg);
            logWriter?.WriteLine($"{DateTime.Now:HH:mm:ss.fff} - {logMsg}");

            // Log coordinate system info
            LogCoordinateSystem(newScene);

            logWriter?.Flush();
        }

        private void LogSceneDetails(Scene scene)
        {
            logWriter?.WriteLine($"  === Scene Details: {scene.name} ===");
            logWriter?.WriteLine($"  Is Valid: {scene.IsValid()}");
            logWriter?.WriteLine($"  Is Loaded: {scene.isLoaded}");
            logWriter?.WriteLine($"  Root Count: {scene.rootCount}");

            // Get root GameObjects
            GameObject[] roots = scene.GetRootGameObjects();
            logWriter?.WriteLine($"  Root GameObjects ({roots.Length}):");
            foreach (var root in roots.Take(10)) // Log first 10 roots
            {
                logWriter?.WriteLine($"    - {root.name} (active: {root.activeSelf})");

                // Check for specific components that might indicate scene type
                if (root.GetComponent<SceneManager>() != null)
                    logWriter?.WriteLine($"      ^ Has SceneManager component");
                if (root.name.Contains("_geo") || root.name.Contains("_map"))
                    logWriter?.WriteLine($"      ^ Likely geometry/map object");
                if (root.name.Contains("spawn") || root.name.Contains("entrance"))
                    logWriter?.WriteLine($"      ^ Likely spawn/entrance point");
            }
        }

        private void LogCoordinateSystem(Scene scene)
        {
            logWriter?.WriteLine($"  === Coordinate System Analysis ===");

            // Find bounds of the scene
            Bounds sceneBounds = new Bounds();
            bool boundsInitialized = false;

            GameObject[] roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    if (!boundsInitialized)
                    {
                        sceneBounds = renderer.bounds;
                        boundsInitialized = true;
                    }
                    else
                    {
                        sceneBounds.Encapsulate(renderer.bounds);
                    }
                }
            }

            if (boundsInitialized)
            {
                logWriter?.WriteLine($"  Scene Bounds Center: {sceneBounds.center}");
                logWriter?.WriteLine($"  Scene Bounds Size: {sceneBounds.size}");
                logWriter?.WriteLine($"  Scene Bounds Min: {sceneBounds.min}");
                logWriter?.WriteLine($"  Scene Bounds Max: {sceneBounds.max}");
            }

            // Check for camera
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                logWriter?.WriteLine($"  Main Camera Position: {mainCam.transform.position}");
                logWriter?.WriteLine($"  Main Camera Rotation: {mainCam.transform.rotation.eulerAngles}");
                logWriter?.WriteLine($"  Main Camera Orthographic: {mainCam.orthographic}");
                if (mainCam.orthographic)
                    logWriter?.WriteLine($"  Main Camera Orthographic Size: {mainCam.orthographicSize}");
            }
        }

        private void LogCurrentSceneState()
        {
            logWriter?.WriteLine($"\n=== Initial Scene State ===");

            Scene activeScene = SceneManager.GetActiveScene();
            logWriter?.WriteLine($"Active Scene: {activeScene.name}");
            LogSceneDetails(activeScene);

            int sceneCount = SceneManager.sceneCount;
            logWriter?.WriteLine($"Total Loaded Scenes: {sceneCount}");

            for (int i = 0; i < sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                logWriter?.WriteLine($"  Scene {i}: {scene.name} (loaded: {scene.isLoaded})");
            }

            logWriter?.Flush();
        }

        private List<string> GetAllActiveScenes()
        {
            List<string> scenes = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                    scenes.Add(scene.name);
            }
            return scenes;
        }

        private void WriteSummary()
        {
            logWriter?.WriteLine($"\n\n=== SCENE ANALYSIS SUMMARY ===");
            logWriter?.WriteLine($"Total Transitions: {transitions.Count}");

            // Find unique scenes
            var uniqueScenes = transitions.SelectMany(t => new[] { t.FromScene, t.ToScene })
                                         .Where(s => !string.IsNullOrEmpty(s))
                                         .Distinct()
                                         .OrderBy(s => s)
                                         .ToList();

            logWriter?.WriteLine($"Unique Scenes Visited ({uniqueScenes.Count}):");
            foreach (var scene in uniqueScenes)
            {
                logWriter?.WriteLine($"  - {scene}");
            }

            // Analyze transition patterns
            logWriter?.WriteLine($"\nTransition History:");
            foreach (var t in transitions)
            {
                logWriter?.WriteLine($"  {t.FromScene} -> {t.ToScene} at {t.Timestamp:HH:mm:ss} (duration: {t.SceneDuration:F2}s)");
                if (t.PlayerPosition != Vector3.zero)
                    logWriter?.WriteLine($"    Player at: {t.PlayerPosition}");
            }

            // Scene naming patterns
            logWriter?.WriteLine($"\nScene Naming Patterns Detected:");
            var patterns = new Dictionary<string, List<string>>();

            foreach (var scene in uniqueScenes)
            {
                // Check for common prefixes/suffixes
                if (scene.Contains("_"))
                {
                    string prefix = scene.Split('_')[0];
                    if (!patterns.ContainsKey(prefix))
                        patterns[prefix] = new List<string>();
                    patterns[prefix].Add(scene);
                }
            }

            foreach (var pattern in patterns)
            {
                if (pattern.Value.Count > 1)
                {
                    logWriter?.WriteLine($"  Pattern '{pattern.Key}_*': {string.Join(", ", pattern.Value)}");
                }
            }

            logWriter?.WriteLine($"\n=== END OF ANALYSIS ===");
        }
    }
}