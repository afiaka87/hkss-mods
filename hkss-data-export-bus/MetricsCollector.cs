using UnityEngine;
using System;
using System.Collections.Generic;
using HarmonyLib;

namespace HKSS.DataExportBus
{
    public class GameMetric
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; }
        public Dictionary<string, object> Data { get; set; }

        public GameMetric(string eventType)
        {
            Timestamp = DateTime.UtcNow;
            EventType = eventType;
            Data = new Dictionary<string, object>();
        }
    }

    public class MetricsCollector : MonoBehaviour
    {
        private HeroController heroController;
        private float lastUpdateTime = 0f;
        private float updateInterval;
        private Vector3 lastPosition;
        private float totalDistance = 0f;
        private float sessionStartTime;
        private int totalDamageTaken = 0;
        private int totalDamageDealt = 0;
        private int enemiesKilled = 0;
        private string currentScene = "";
        private float sceneEnterTime = 0f;
        private List<string> recentEvents = new List<string>();

        void Start()
        {
            DataExportBusPlugin.ModLogger?.LogInfo("MetricsCollector started");
            updateInterval = 1f / DataExportBusPlugin.Instance.UpdateFrequencyHz.Value;
            sessionStartTime = Time.time;
        }

        void Update()
        {
            if (heroController == null)
            {
                heroController = HeroController.instance;
                if (heroController == null)
                    return;

                lastPosition = heroController.transform.position;
            }

            // Regular update based on configured frequency
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                CollectAndBroadcastMetrics();
                lastUpdateTime = Time.time;
            }

            // Track distance traveled
            float distanceThisFrame = Vector3.Distance(heroController.transform.position, lastPosition);
            totalDistance += distanceThisFrame;
            lastPosition = heroController.transform.position;
        }

        private void CollectAndBroadcastMetrics()
        {
            if (heroController == null)
                return;

            var plugin = DataExportBusPlugin.Instance;

            // Player data
            if (plugin.ExportPlayerData.Value)
            {
                var playerMetric = new GameMetric("player_update");
                playerMetric.Data["position_x"] = heroController.transform.position.x;
                playerMetric.Data["position_y"] = heroController.transform.position.y;
                playerMetric.Data["velocity_x"] = heroController.GetComponent<Rigidbody2D>()?.linearVelocity.x ?? 0f;
                playerMetric.Data["velocity_y"] = heroController.GetComponent<Rigidbody2D>()?.linearVelocity.y ?? 0f;
                playerMetric.Data["health_current"] = PlayerData.instance?.health ?? 0;
                playerMetric.Data["health_max"] = PlayerData.instance?.maxHealth ?? 0;
                playerMetric.Data["soul_current"] = 0; // Soul/MP system may differ in Silksong
                playerMetric.Data["soul_max"] = 0;
                playerMetric.Data["grounded"] = heroController.cState.onGround;
                playerMetric.Data["dashing"] = heroController.cState.dashing;
                playerMetric.Data["attacking"] = heroController.cState.attacking;
                playerMetric.Data["total_distance"] = totalDistance;

                plugin.BroadcastMetric(playerMetric);
            }

            // Scene data
            if (plugin.ExportSceneData.Value && !string.IsNullOrEmpty(currentScene))
            {
                var sceneMetric = new GameMetric("scene_update");
                sceneMetric.Data["scene_name"] = currentScene;
                sceneMetric.Data["time_in_scene"] = Time.time - sceneEnterTime;
                sceneMetric.Data["room_position_x"] = heroController.transform.position.x;
                sceneMetric.Data["room_position_y"] = heroController.transform.position.y;

                plugin.BroadcastMetric(sceneMetric);
            }

            // Timing data
            if (plugin.ExportTimingData.Value)
            {
                var timingMetric = new GameMetric("timing_update");
                timingMetric.Data["session_time"] = Time.time - sessionStartTime;
                timingMetric.Data["real_time"] = DateTime.UtcNow.ToString("o");
                timingMetric.Data["frame_count"] = Time.frameCount;
                timingMetric.Data["fps"] = 1f / Time.deltaTime;

                plugin.BroadcastMetric(timingMetric);
            }

            // Combat stats
            if (plugin.ExportCombatData.Value)
            {
                var combatMetric = new GameMetric("combat_stats");
                combatMetric.Data["total_damage_taken"] = totalDamageTaken;
                combatMetric.Data["total_damage_dealt"] = totalDamageDealt;
                combatMetric.Data["enemies_killed"] = enemiesKilled;

                plugin.BroadcastMetric(combatMetric);
            }
        }

        public void OnPlayerDamaged(int damage)
        {
            totalDamageTaken += damage;

            if (DataExportBusPlugin.Instance.ExportCombatData.Value)
            {
                var metric = new GameMetric("player_damaged");
                metric.Data["damage"] = damage;
                metric.Data["health_remaining"] = PlayerData.instance?.health ?? 0;
                metric.Data["position_x"] = heroController?.transform.position.x ?? 0;
                metric.Data["position_y"] = heroController?.transform.position.y ?? 0;

                DataExportBusPlugin.Instance.BroadcastMetric(metric);
            }

            recentEvents.Add($"Damaged: {damage}");
            TrimRecentEvents();
        }

        public void OnEnemyDamaged(int damage, GameObject enemy)
        {
            totalDamageDealt += damage;

            if (DataExportBusPlugin.Instance.ExportCombatData.Value)
            {
                var metric = new GameMetric("enemy_damaged");
                metric.Data["damage"] = damage;
                metric.Data["enemy_name"] = enemy?.name ?? "Unknown";
                metric.Data["enemy_position_x"] = enemy?.transform.position.x ?? 0;
                metric.Data["enemy_position_y"] = enemy?.transform.position.y ?? 0;

                DataExportBusPlugin.Instance.BroadcastMetric(metric);
            }
        }

        public void OnEnemyKilled(GameObject enemy)
        {
            enemiesKilled++;

            if (DataExportBusPlugin.Instance.ExportCombatData.Value)
            {
                var metric = new GameMetric("enemy_killed");
                metric.Data["enemy_name"] = enemy?.name ?? "Unknown";
                metric.Data["total_kills"] = enemiesKilled;
                metric.Data["position_x"] = enemy?.transform.position.x ?? 0;
                metric.Data["position_y"] = enemy?.transform.position.y ?? 0;

                DataExportBusPlugin.Instance.BroadcastMetric(metric);
            }

            recentEvents.Add($"Killed: {enemy?.name}");
            TrimRecentEvents();
        }

        public void OnSceneChange(string sceneName)
        {
            currentScene = sceneName;
            sceneEnterTime = Time.time;

            if (DataExportBusPlugin.Instance.ExportSceneData.Value)
            {
                var metric = new GameMetric("scene_transition");
                metric.Data["scene_name"] = sceneName;
                metric.Data["session_time"] = Time.time - sessionStartTime;

                DataExportBusPlugin.Instance.BroadcastMetric(metric);
            }

            recentEvents.Add($"Scene: {sceneName}");
            TrimRecentEvents();
        }

        public void OnItemCollected(string itemName, int quantity = 1)
        {
            if (DataExportBusPlugin.Instance.ExportInventoryData.Value)
            {
                var metric = new GameMetric("item_collected");
                metric.Data["item_name"] = itemName;
                metric.Data["quantity"] = quantity;
                metric.Data["position_x"] = heroController?.transform.position.x ?? 0;
                metric.Data["position_y"] = heroController?.transform.position.y ?? 0;

                DataExportBusPlugin.Instance.BroadcastMetric(metric);
            }

            recentEvents.Add($"Item: {itemName} x{quantity}");
            TrimRecentEvents();
        }

        public void OnAbilityUnlocked(string abilityName)
        {
            if (DataExportBusPlugin.Instance.ExportInventoryData.Value)
            {
                var metric = new GameMetric("ability_unlocked");
                metric.Data["ability_name"] = abilityName;
                metric.Data["session_time"] = Time.time - sessionStartTime;

                DataExportBusPlugin.Instance.BroadcastMetric(metric);
            }

            recentEvents.Add($"Ability: {abilityName}");
            TrimRecentEvents();
        }

        public void OnBossEncounter(string bossName, string eventType)
        {
            if (DataExportBusPlugin.Instance.ExportCombatData.Value)
            {
                var metric = new GameMetric("boss_event");
                metric.Data["boss_name"] = bossName;
                metric.Data["event_type"] = eventType; // "start", "phase_change", "defeat"
                metric.Data["session_time"] = Time.time - sessionStartTime;

                DataExportBusPlugin.Instance.BroadcastMetric(metric);
            }

            recentEvents.Add($"Boss {eventType}: {bossName}");
            TrimRecentEvents();
        }

        public List<string> GetRecentEvents()
        {
            return new List<string>(recentEvents);
        }

        private void TrimRecentEvents()
        {
            while (recentEvents.Count > 20)
            {
                recentEvents.RemoveAt(0);
            }
        }

        public Dictionary<string, object> GetCurrentState()
        {
            var state = new Dictionary<string, object>();

            if (heroController != null)
            {
                state["player_position"] = new { x = heroController.transform.position.x, y = heroController.transform.position.y };
                state["player_health"] = new { current = PlayerData.instance?.health ?? 0, max = PlayerData.instance?.maxHealth ?? 0 };
                state["player_soul"] = new { current = 0, max = 0 }; // Soul/MP system may differ in Silksong
            }

            state["current_scene"] = currentScene;
            state["session_time"] = Time.time - sessionStartTime;
            state["total_distance"] = totalDistance;
            state["enemies_killed"] = enemiesKilled;
            state["damage_taken"] = totalDamageTaken;
            state["damage_dealt"] = totalDamageDealt;
            state["recent_events"] = recentEvents;

            return state;
        }
    }

    // Harmony patches to collect game events
    [HarmonyPatch]
    public static class MetricsPatches
    {
        [HarmonyPatch(typeof(HeroController), "TakeDamage")]
        [HarmonyPostfix]
        public static void OnPlayerDamage(HeroController __instance, GameObject go, int damageAmount)
        {
            DataExportBusPlugin.Instance?.GetMetricsCollector()?.OnPlayerDamaged(damageAmount);
        }

        [HarmonyPatch(typeof(HealthManager), "TakeDamage")]
        [HarmonyPostfix]
        public static void OnEnemyDamage(HealthManager __instance, in HitInstance hitInstance)
        {
            if (__instance.gameObject != HeroController.instance?.gameObject)
            {
                DataExportBusPlugin.Instance?.GetMetricsCollector()?.OnEnemyDamaged(hitInstance.DamageDealt, __instance.gameObject);
            }
        }

        [HarmonyPatch(typeof(HealthManager), "Die")]
        [HarmonyPostfix]
        public static void OnEnemyDeath(HealthManager __instance)
        {
            if (__instance.gameObject != HeroController.instance?.gameObject)
            {
                DataExportBusPlugin.Instance?.GetMetricsCollector()?.OnEnemyKilled(__instance.gameObject);
            }
        }

        [HarmonyPatch(typeof(HeroController), "EnterScene")]
        [HarmonyPostfix]
        public static void OnSceneEnter(HeroController __instance, string enterGate)
        {
            var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            DataExportBusPlugin.Instance?.GetMetricsCollector()?.OnSceneChange(sceneName);
        }
    }
}