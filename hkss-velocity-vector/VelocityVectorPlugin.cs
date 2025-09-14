using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace HKSS.VelocityVector
{
    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.hkss.velocityvector";
        public const string PLUGIN_NAME = "Velocity Vector HUD";
        public const string PLUGIN_VERSION = "1.0.0";
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInProcess("Hollow Knight Silksong.exe")]
    public class VelocityVectorPlugin : BaseUnityPlugin
    {
        public static ManualLogSource ModLogger { get; private set; }
        public static VelocityVectorPlugin Instance { get; private set; }

        private Harmony harmony;
        private GameObject displayObject;

        public ConfigEntry<bool> Enabled { get; private set; }
        public ConfigEntry<DisplayPosition> Position { get; private set; }
        public ConfigEntry<DisplayUnits> Units { get; private set; }
        public ConfigEntry<float> ArrowScale { get; private set; }
        public ConfigEntry<bool> ShowPeakSpeed { get; private set; }
        public ConfigEntry<bool> ShowVector { get; private set; }
        public ConfigEntry<bool> ShowNumeric { get; private set; }
        public ConfigEntry<Color> ArrowColor { get; private set; }
        public ConfigEntry<Color> TextColor { get; private set; }
        public ConfigEntry<int> FontSize { get; private set; }

        void Awake()
        {
            Instance = this;
            ModLogger = Logger;

            InitializeConfig();

            if (!Enabled.Value)
            {
                Logger.LogInfo("Velocity Vector HUD is disabled in config");
                return;
            }

            harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();

            displayObject = new GameObject("VelocityVectorDisplay");
            displayObject.AddComponent<VelocityDisplay>();
            DontDestroyOnLoad(displayObject);

            Logger.LogInfo($"Velocity Vector HUD v{PluginInfo.PLUGIN_VERSION} loaded!");
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
                "Enable or disable the velocity vector display");

            Position = Config.Bind("Display", "Position", DisplayPosition.TopLeft,
                "Screen position for the velocity display");

            Units = Config.Bind("Display", "Units", DisplayUnits.UnitsPerSecond,
                "Units to display velocity in");

            ArrowScale = Config.Bind("Display", "ArrowScale", 1.0f,
                new ConfigDescription("Scale of the direction arrow",
                new AcceptableValueRange<float>(0.5f, 3.0f)));

            ShowPeakSpeed = Config.Bind("Display", "ShowPeakSpeed", false,
                "Track and display session maximum speed");

            ShowVector = Config.Bind("Display", "ShowVector", true,
                "Show the directional arrow");

            ShowNumeric = Config.Bind("Display", "ShowNumeric", true,
                "Show numeric speed value");

            ArrowColor = Config.Bind("Display", "ArrowColor", Color.cyan,
                "Color of the direction arrow");

            TextColor = Config.Bind("Display", "TextColor", Color.white,
                "Color of the text display");

            FontSize = Config.Bind("Display", "FontSize", 20,
                new ConfigDescription("Font size for text display",
                new AcceptableValueRange<int>(12, 48)));
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

    public enum DisplayUnits
    {
        UnitsPerSecond,
        MetersPerSecond,
        PixelsPerFrame
    }
}