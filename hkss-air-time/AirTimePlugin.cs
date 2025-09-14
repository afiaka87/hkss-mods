using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace HKSS.AirTime
{
    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.hkss.airtime";
        public const string PLUGIN_NAME = "Air Time Counter";
        public const string PLUGIN_VERSION = "1.0.0";
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInProcess("Hollow Knight Silksong.exe")]
    public class AirTimePlugin : BaseUnityPlugin
    {
        public static ManualLogSource ModLogger { get; private set; }
        public static AirTimePlugin Instance { get; private set; }

        private Harmony harmony;
        private GameObject displayObject;

        public ConfigEntry<bool> Enabled { get; private set; }
        public ConfigEntry<DisplayPosition> Position { get; private set; }
        public ConfigEntry<bool> ShowCurrentJump { get; private set; }
        public ConfigEntry<bool> ShowSessionTotal { get; private set; }
        public ConfigEntry<bool> ShowJumpHistory { get; private set; }
        public ConfigEntry<int> HistorySize { get; private set; }
        public ConfigEntry<int> DecimalPlaces { get; private set; }
        public ConfigEntry<Color> TextColor { get; private set; }
        public ConfigEntry<Color> HistoryColor { get; private set; }
        public ConfigEntry<int> FontSize { get; private set; }

        void Awake()
        {
            Instance = this;
            ModLogger = Logger;

            InitializeConfig();

            if (!Enabled.Value)
            {
                Logger.LogInfo("Air Time Counter is disabled in config");
                return;
            }

            harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();

            displayObject = new GameObject("AirTimeDisplay");
            displayObject.AddComponent<AirTimeDisplay>();
            displayObject.AddComponent<AirTimeTracker>();
            DontDestroyOnLoad(displayObject);

            Logger.LogInfo($"Air Time Counter v{PluginInfo.PLUGIN_VERSION} loaded!");
        }

        void OnDestroy()
        {
            harmony?.UnpatchSelf();
            if (displayObject != null)
            {
                Destroy(displayObject);
            }
        }

        private void InitializeConfig()
        {
            Enabled = Config.Bind("General", "Enabled", true,
                "Enable or disable the air time counter");

            Position = Config.Bind("Display", "Position", DisplayPosition.TopRight,
                "Screen position for the air time display");

            ShowCurrentJump = Config.Bind("Display", "ShowCurrentJump", true,
                "Show the current jump duration");

            ShowSessionTotal = Config.Bind("Display", "ShowSessionTotal", true,
                "Show total air time for the session");

            ShowJumpHistory = Config.Bind("Display", "ShowJumpHistory", true,
                "Show histogram of recent jumps");

            HistorySize = Config.Bind("Display", "HistorySize", 10,
                new ConfigDescription("Number of recent jumps to display",
                new AcceptableValueRange<int>(3, 20)));

            DecimalPlaces = Config.Bind("Display", "DecimalPlaces", 2,
                new ConfigDescription("Decimal places for time display",
                new AcceptableValueRange<int>(1, 3)));

            TextColor = Config.Bind("Display", "TextColor", Color.white,
                "Color of the text display");

            HistoryColor = Config.Bind("Display", "HistoryColor", Color.cyan,
                "Color of the jump history bars");

            FontSize = Config.Bind("Display", "FontSize", 18,
                new ConfigDescription("Font size for text display",
                new AcceptableValueRange<int>(12, 36)));
        }
    }

    public enum DisplayPosition
    {
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        MiddleCenter,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }
}