using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace HKSS.BreadcrumbTrail
{
    public class TrailPoint
    {
        public Vector3 position;
        public float timestamp;
        public float speed;
        public bool inCombat;
        public Color color;
    }

    public class BreadcrumbTrail : MonoBehaviour
    {
        private float lastDropTime = 0f;
        private HeroController heroController;
        private Rigidbody2D heroRigidbody;
        private MultiSceneTrailManager trailManager;
        private TrailOptimizer optimizer;
        private float statsLogTimer = 0f;
        private const float STATS_LOG_INTERVAL = 10f; // Log stats every 10 seconds

        void Awake()
        {
            try
            {
                BreadcrumbPlugin.ModLogger?.LogInfo("[BreadcrumbTrail] Awake starting...");

                // Create or find the multi-scene trail manager
                GameObject managerObj = GameObject.Find("MultiSceneTrailManager");
                BreadcrumbPlugin.ModLogger?.LogInfo($"[BreadcrumbTrail] Search for MultiSceneTrailManager: {managerObj != null}");

                if (managerObj == null)
                {
                    BreadcrumbPlugin.ModLogger?.LogInfo("[BreadcrumbTrail] Creating new MultiSceneTrailManager GameObject...");
                    managerObj = new GameObject("MultiSceneTrailManager");
                    BreadcrumbPlugin.ModLogger?.LogInfo($"[BreadcrumbTrail] GameObject created: {managerObj != null}");

                    DontDestroyOnLoad(managerObj);
                    BreadcrumbPlugin.ModLogger?.LogInfo("[BreadcrumbTrail] DontDestroyOnLoad set");

                    trailManager = managerObj.AddComponent<MultiSceneTrailManager>();
                    BreadcrumbPlugin.ModLogger?.LogInfo($"[BreadcrumbTrail] MultiSceneTrailManager component added: {trailManager != null}");
                }
                else
                {
                    BreadcrumbPlugin.ModLogger?.LogInfo("[BreadcrumbTrail] Found existing MultiSceneTrailManager");
                    trailManager = managerObj.GetComponent<MultiSceneTrailManager>();
                    BreadcrumbPlugin.ModLogger?.LogInfo($"[BreadcrumbTrail] Got component: {trailManager != null}");
                }

                BreadcrumbPlugin.ModLogger?.LogInfo($"[BreadcrumbTrail] ✓ Awake complete - manager initialized: {trailManager != null}");

                // Initialize optimizer if optimizations are enabled
                if (BreadcrumbPlugin.Instance.EnableOptimizations.Value)
                {
                    BreadcrumbPlugin.ModLogger?.LogInfo("[BreadcrumbTrail] Initializing TrailOptimizer...");
                    optimizer = new TrailOptimizer(BreadcrumbPlugin.ModLogger);
                    UpdateOptimizerConfig();
                    BreadcrumbPlugin.ModLogger?.LogInfo("[BreadcrumbTrail] ✓ TrailOptimizer initialized");
                }
            }
            catch (Exception ex)
            {
                BreadcrumbPlugin.ModLogger?.LogError($"[BreadcrumbTrail] ✗ Awake failed: {ex.Message}");
                BreadcrumbPlugin.ModLogger?.LogError($"  Stack: {ex.StackTrace}");
            }
        }

        void UpdateOptimizerConfig()
        {
            if (optimizer != null)
            {
                optimizer.UpdateConfig(
                    BreadcrumbPlugin.Instance.AngleCullThreshold.Value,
                    BreadcrumbPlugin.Instance.MinPointDistance.Value,
                    BreadcrumbPlugin.Instance.UseAdaptiveSampling.Value
                );
            }
        }

        void Start()
        {
            BreadcrumbPlugin.ModLogger?.LogInfo("BreadcrumbTrail Start - trail manager ready");
        }

        // Removed InitializeLineRenderer and CreateColorGradient - now handled by MultiSceneTrailManager

        void Update()
        {
            if (!BreadcrumbPlugin.Instance.Enabled.Value)
                return;

            // Check for toggle key press
            if (Input.GetKeyDown(BreadcrumbPlugin.Instance.ToggleKey.Value))
            {
                BreadcrumbPlugin.Instance.TrailVisible = !BreadcrumbPlugin.Instance.TrailVisible;
                BreadcrumbPlugin.ModLogger?.LogInfo($"[BreadcrumbTrail] Trail visibility toggled to: {BreadcrumbPlugin.Instance.TrailVisible}");

                // Show the toggle message
                ShowToggleMessage();

                // Update trail visibility
                if (trailManager != null)
                {
                    var managerObj = GameObject.Find("MultiSceneTrailManager");
                    if (managerObj != null)
                    {
                        var multiSceneManager = managerObj.GetComponent<MultiSceneTrailManager>();
                        multiSceneManager?.SetTrailsVisible(BreadcrumbPlugin.Instance.TrailVisible);
                    }
                }
            }

            // Don't update trail collection if trail is hidden
            if (!BreadcrumbPlugin.Instance.TrailVisible)
                return;

            if (trailManager == null)
                return;

            if (heroController == null)
            {
                heroController = HeroController.instance;
                if (heroController != null)
                {
                    heroRigidbody = heroController.GetComponent<Rigidbody2D>();
                }
                if (heroController == null)
                    return;
            }

            // Use adaptive sampling rate if optimizer is active
            float dropFrequency = BreadcrumbPlugin.Instance.DropFrequency.Value;
            if (optimizer != null && BreadcrumbPlugin.Instance.UseAdaptiveSampling.Value)
            {
                dropFrequency = optimizer.GetCurrentSampleRate();
            }

            // Check if we should drop a new point
            if (Time.time - lastDropTime >= dropFrequency)
            {
                DropTrailPoint();
                lastDropTime = Time.time;
            }

            // Log optimizer statistics periodically
            if (optimizer != null && BreadcrumbPlugin.Instance.ShowOptimizationStats.Value)
            {
                statsLogTimer += Time.deltaTime;
                if (statsLogTimer >= STATS_LOG_INTERVAL)
                {
                    optimizer.LogStats();
                    statsLogTimer = 0f;
                }
            }
        }

        void DropTrailPoint()
        {
            if (heroController == null || trailManager == null)
                return;

            // Check combat state
            bool inCombat = IsInCombat();
            if (!BreadcrumbPlugin.Instance.ShowInCombat.Value && inCombat)
                return;

            Vector3 position = heroController.transform.position;
            float speed = heroRigidbody != null ? heroRigidbody.linearVelocity.magnitude : 0f;

            // Use optimizer to determine if we should add this point
            if (optimizer != null && BreadcrumbPlugin.Instance.EnableOptimizations.Value)
            {
                float timeSinceLastPoint = Time.time - lastDropTime;
                if (!optimizer.ShouldAddPoint(position, speed, inCombat, timeSinceLastPoint))
                {
                    return; // Skip this point based on optimization criteria
                }
            }

            // Add point to the multi-scene manager
            trailManager.AddTrailPoint(position, speed, inCombat);
        }

        // Removed RemoveOldPoints, UpdateLineRenderer, CalculatePointColor, and CalculateFade
        // These are now handled by MultiSceneTrailManager and SceneTrailRenderer

        bool IsInCombat()
        {
            // Check if there are enemies nearby
            Collider2D[] nearbyEnemies = Physics2D.OverlapCircleAll(
                heroController.transform.position,
                10f,
                LayerMask.GetMask("Enemies")
            );

            return nearbyEnemies.Length > 0;
        }

        void OnDestroy()
        {
            // Trail cleanup is now handled by MultiSceneTrailManager
        }

        private float toggleMessageTime = 0f;
        private const float TOGGLE_MESSAGE_DURATION = 2f;

        void OnGUI()
        {
            // Display toggle message
            if (Time.time - toggleMessageTime < TOGGLE_MESSAGE_DURATION)
            {
                GUIStyle messageStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 24,
                    fontStyle = FontStyle.Bold
                };

                // Add shadow effect
                GUI.color = new Color(0, 0, 0, 0.8f);
                GUI.Label(new Rect(Screen.width / 2 - 149, 49, 300, 50),
                    BreadcrumbPlugin.Instance.TrailVisible ? "Trail: ON" : "Trail: OFF", messageStyle);

                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width / 2 - 150, 50, 300, 50),
                    BreadcrumbPlugin.Instance.TrailVisible ? "Trail: ON" : "Trail: OFF", messageStyle);

                GUI.color = Color.white;
            }

            // Display optimization statistics overlay if enabled
            if (optimizer != null && BreadcrumbPlugin.Instance.ShowOptimizationStats.Value)
            {
                var stats = optimizer.GetStats();

                GUI.color = Color.white;
                GUI.backgroundColor = new Color(0, 0, 0, 0.7f);

                GUIStyle style = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 12,
                    normal = { textColor = Color.white }
                };

                string statsText = $"Trail Optimizer Stats\n" +
                                 $"Points Generated: {stats.TotalPointsGenerated}\n" +
                                 $"Points Accepted: {stats.PointsAccepted}\n" +
                                 $"Reduction: {stats.ReductionPercentage:F1}%\n" +
                                 $"Culled by Angle: {stats.PointsCulledByAngle}\n" +
                                 $"Culled by Distance: {stats.PointsCulledByDistance}\n" +
                                 $"Sample Rate: {stats.CurrentSampleRate:F2}s\n" +
                                 $"Complexity: {stats.MovementComplexity:F2}";

                GUI.Box(new Rect(10, 10, 200, 140), statsText, style);
            }
        }

        public void ShowToggleMessage()
        {
            toggleMessageTime = Time.time;
        }

        public void ResetOptimizer()
        {
            optimizer?.Reset();
        }
    }

    [HarmonyPatch]
    public static class TrailPatches
    {
        // FIXED: Parameter name must match exactly - it's 'enterGate' not 'transitionPoint'
        [HarmonyPatch(typeof(HeroController), "EnterScene")]
        [HarmonyPostfix]
        public static void OnSceneTransition(HeroController __instance, TransitionPoint enterGate, float delayBeforeEnter)
        {
            // Log detailed transition information
            BreadcrumbPlugin.ModLogger?.LogInfo($"[HERO_ENTER_SCENE] TransitionPoint: {enterGate?.name ?? "null"}");

            if (enterGate != null)
            {
                BreadcrumbPlugin.ModLogger?.LogInfo($"  TransitionPoint Position: {enterGate.transform.position}");
                BreadcrumbPlugin.ModLogger?.LogInfo($"  TransitionPoint Scene: {enterGate.gameObject.scene.name}");
                BreadcrumbPlugin.ModLogger?.LogInfo($"  Delay Before Enter: {delayBeforeEnter}");

                // Log transition point details
                var fields = enterGate.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                foreach (var field in fields)
                {
                    try
                    {
                        var value = field.GetValue(enterGate);
                        if (value != null && field.Name.ToLower().Contains("scene"))
                        {
                            BreadcrumbPlugin.ModLogger?.LogInfo($"  TransitionPoint.{field.Name}: {value}");
                        }
                    }
                    catch { }
                }
            }

            // Reset optimizer on scene transition
            var breadcrumb = GameObject.Find("BreadcrumbTrail")?.GetComponent<BreadcrumbTrail>();
            breadcrumb?.ResetOptimizer();

            // The trail will naturally clear due to the time-based removal
        }

        // Additional patches to understand scene loading
        [HarmonyPatch(typeof(GameManager), "LoadScene")]
        [HarmonyPrefix]
        public static void OnGameManagerLoadScene(string destScene)
        {
            BreadcrumbPlugin.ModLogger?.LogInfo($"[GameManager.LoadScene] Loading: {destScene}");
        }

        [HarmonyPatch(typeof(GameManager), "BeginSceneTransition")]
        [HarmonyPrefix]
        public static void OnBeginSceneTransition(GameManager __instance, GameManager.SceneLoadInfo info)
        {
            BreadcrumbPlugin.ModLogger?.LogInfo($"[GameManager.BeginSceneTransition] Starting transition");
            if (info != null)
            {
                BreadcrumbPlugin.ModLogger?.LogInfo($"  SceneName: {info.SceneName ?? "null"}");
                BreadcrumbPlugin.ModLogger?.LogInfo($"  EntryGateName: {info.EntryGateName ?? "null"}");
                BreadcrumbPlugin.ModLogger?.LogInfo($"  EntryDelay: {info.EntryDelay}");
                BreadcrumbPlugin.ModLogger?.LogInfo($"  PreventCameraFadeOut: {info.PreventCameraFadeOut}");
                BreadcrumbPlugin.ModLogger?.LogInfo($"  Visualization: {info.Visualization}");
                BreadcrumbPlugin.ModLogger?.LogInfo($"  AlwaysUnloadUnusedAssets: {info.AlwaysUnloadUnusedAssets}");

                // Log all fields of SceneLoadInfo using reflection
                var fields = info.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                foreach (var field in fields)
                {
                    try
                    {
                        BreadcrumbPlugin.ModLogger?.LogInfo($"  SceneLoadInfo.{field.Name}: {field.GetValue(info)}");
                    }
                    catch { }
                }
            }
        }

        [HarmonyPatch(typeof(HeroController), "SetHeroParent")]
        [HarmonyPostfix]
        public static void OnSetHeroParent(Transform newParent)
        {
            if (newParent != null)
            {
                BreadcrumbPlugin.ModLogger?.LogInfo($"[HeroController.SetHeroParent] New parent: {newParent.name} in scene: {newParent.gameObject.scene.name}");
            }
        }

        // Patch to understand coordinate changes
        // NOTE: SetPositionToRespawn doesn't exist in Silksong, commented out
        // [HarmonyPatch(typeof(HeroController), "SetPositionToRespawn")]
        // [HarmonyPostfix]
        // public static void OnSetPositionToRespawn(HeroController __instance)
        // {
        //     BreadcrumbPlugin.ModLogger?.LogInfo($"[HeroController.SetPositionToRespawn] Position: {__instance.transform.position}");
        // }
    }
}