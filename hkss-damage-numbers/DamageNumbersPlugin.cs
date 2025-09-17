using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace HKSS.DamageNumbers
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("Hollow Knight Silksong.exe")]
    public class DamageNumbersPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.hkss.damagenumbers";
        public const string PluginName = "Damage Numbers";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log;
        internal static DamageNumbersPlugin Instance;

        // Configuration
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> DisplayDuration;
        internal static ConfigEntry<float> FloatSpeed;
        internal static ConfigEntry<float> FontSize;
        internal static ConfigEntry<string> EnemyDamageColor;
        internal static ConfigEntry<string> PlayerDamageColor;
        internal static ConfigEntry<bool> ShowPlayerDamage;
        internal static ConfigEntry<bool> DebugLogging;

        private Harmony harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            InitializeConfiguration();

            // Initialize the damage number display system
            var displayGO = new GameObject("DamageNumberDisplay");
            DontDestroyOnLoad(displayGO);
            displayGO.AddComponent<DamageNumberDisplay>();

            // Apply Harmony patches
            harmony = new Harmony(PluginGuid);
            harmony.PatchAll();

            Log.LogInfo($"{PluginName} v{PluginVersion} loaded!");
        }

        private void InitializeConfiguration()
        {
            Enabled = Config.Bind(
                "General",
                "Enabled",
                true,
                "Enable or disable damage numbers display"
            );

            DisplayDuration = Config.Bind(
                "Display",
                "Duration",
                1.5f,
                new ConfigDescription(
                    "How long damage numbers stay visible (seconds)",
                    new AcceptableValueRange<float>(0.5f, 5f)
                )
            );

            FloatSpeed = Config.Bind(
                "Display",
                "FloatSpeed",
                2f,
                new ConfigDescription(
                    "Speed at which damage numbers float upward",
                    new AcceptableValueRange<float>(0.5f, 10f)
                )
            );

            FontSize = Config.Bind(
                "Display",
                "FontSize",
                24f,
                new ConfigDescription(
                    "Size of damage number text",
                    new AcceptableValueRange<float>(12f, 72f)
                )
            );

            EnemyDamageColor = Config.Bind(
                "Colors",
                "EnemyDamageColor",
                "#FFD700",
                "Color for enemy damage numbers (hex format) - Golden color for positive feedback"
            );

            PlayerDamageColor = Config.Bind(
                "Colors",
                "PlayerDamageColor",
                "#DC143C",
                "Color for player damage numbers (hex format) - Crimson color for negative feedback"
            );

            ShowPlayerDamage = Config.Bind(
                "Gameplay",
                "ShowPlayerDamage",
                true,
                "Also show damage numbers when the player takes damage"
            );

            DebugLogging = Config.Bind(
                "Debug",
                "EnableDebugLogging",
                false,
                "Enable detailed logging of damage calculations (useful for understanding game mechanics)"
            );
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }
}