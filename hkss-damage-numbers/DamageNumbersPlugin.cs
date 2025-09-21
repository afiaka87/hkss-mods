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
        internal static ConfigEntry<float> BaseFontSize;
        internal static ConfigEntry<bool> AutoScaleResolution;
        internal static ConfigEntry<string> EnemyDamageColor;
        internal static ConfigEntry<string> PlayerDamageColor;
        internal static ConfigEntry<bool> ShowPlayerDamage;
        internal static ConfigEntry<bool> DebugLogging;
        internal static ConfigEntry<string> FontName;
        internal static ConfigEntry<string> CustomFontPath;
        internal static ConfigEntry<bool> UseOutline;
        internal static ConfigEntry<float> OutlineWidth;
        internal static ConfigEntry<float> DamageCooldown;

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

            BaseFontSize = Config.Bind(
                "Display",
                "BaseFontSize",
                36f,
                new ConfigDescription(
                    "Base size of damage number text (scales with resolution)",
                    new AcceptableValueRange<float>(12f, 100f)
                )
            );

            AutoScaleResolution = Config.Bind(
                "Display",
                "AutoScaleResolution",
                true,
                "Automatically scale font size based on screen resolution"
            );

            EnemyDamageColor = Config.Bind(
                "Colors",
                "EnemyDamageColor",
                "#3A3A3A",
                "Color for enemy damage numbers (hex format) - Dark grey for subtle display"
            );

            PlayerDamageColor = Config.Bind(
                "Colors",
                "PlayerDamageColor",
                "#4A4A4A",
                "Color for player damage numbers (hex format) - Slightly lighter dark grey"
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

            FontName = Config.Bind(
                "Font",
                "FontName",
                "Georgia",
                new ConfigDescription(
                    "Font to use. Unity=game's font. Basic: Arial, Helvetica, Verdana, Tahoma, Trebuchet, Calibri, Segoe, Futura, Century, Franklin. Gothic/Fantasy: Trajan, Georgia, Times, Garamond, Baskerville, Palatino, Bookman, Perpetua, Copperplate, Didot",
                    new AcceptableValueList<string>(
                        "Default", "Unity", "Custom",
                        // Basic fonts
                        "Arial", "Helvetica", "Verdana", "Tahoma", "Trebuchet",
                        "Calibri", "Segoe", "Futura", "Century", "Franklin",
                        // Gothic/Fantasy fonts
                        "Trajan", "Georgia", "Times", "Garamond", "Baskerville",
                        "Palatino", "Bookman", "Perpetua", "Copperplate", "Didot"
                    )
                )
            );

            CustomFontPath = Config.Bind(
                "Font",
                "CustomFontPath",
                "",
                "Path to custom .ttf or .otf font file (only used when FontName is set to Custom)"
            );

            UseOutline = Config.Bind(
                "Font",
                "UseOutline",
                true,
                "Add an outline/shadow effect to damage numbers for better visibility"
            );

            OutlineWidth = Config.Bind(
                "Font",
                "OutlineWidth",
                2f,
                new ConfigDescription(
                    "Width of the outline effect in pixels",
                    new AcceptableValueRange<float>(1f, 5f)
                )
            );

            DamageCooldown = Config.Bind(
                "Gameplay",
                "DamageCooldown",
                0.5f,
                new ConfigDescription(
                    "Minimum time between damage numbers for the same enemy (prevents spam from overlapping hitboxes)",
                    new AcceptableValueRange<float>(0f, 2f)
                )
            );
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }
}