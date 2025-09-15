using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace HKSS.InputTimeline
{
    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.hkss.inputtimeline";
        public const string PLUGIN_NAME = "Input Timeline Strip";
        public const string PLUGIN_VERSION = "1.0.0";
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInProcess("Hollow Knight Silksong.exe")]
    public class InputTimelinePlugin : BaseUnityPlugin
    {
        internal static ManualLogSource ModLogger;
        internal static InputTimelinePlugin Instance { get; private set; }

        // Configuration
        public ConfigEntry<bool> Enabled { get; private set; }
        public ConfigEntry<int> MaxRecentActions { get; private set; }
        public ConfigEntry<TimelinePosition> Position { get; private set; }
        public ConfigEntry<float> TimeWindow { get; private set; }
        public ConfigEntry<float> Opacity { get; private set; }
        public ConfigEntry<bool> ShowTimestamps { get; private set; }
        public ConfigEntry<bool> ShowBackground { get; private set; }
        public ConfigEntry<Color> ActionBoxColor { get; private set; }
        public ConfigEntry<Color> HighlightColor { get; private set; }
        public ConfigEntry<Color> BackgroundColor { get; private set; }

        private Harmony harmony;
        private GameObject timelineObject;

        void Awake()
        {
            Instance = this;
            ModLogger = Logger;

            LoadConfiguration();

            harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();

            Logger.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} loaded!");

            // Create the timeline GameObject immediately
            Logger.LogInfo("Creating InputTimeline GameObject...");
            timelineObject = new GameObject("InputTimeline");
            var recorder = timelineObject.AddComponent<InputRecorder>();
            var renderer = timelineObject.AddComponent<TimelineRenderer>();
            DontDestroyOnLoad(timelineObject);
            Logger.LogInfo($"InputTimeline GameObject created with recorder={recorder != null} and renderer={renderer != null}");
        }

        void Start()
        {
            // No longer needed here
        }

        void OnDestroy()
        {
            harmony?.UnpatchSelf();
            if (timelineObject != null)
                Destroy(timelineObject);
        }

        private void LoadConfiguration()
        {
            Enabled = Config.Bind("General", "Enabled", true,
                "Enable the input timeline display");

            MaxRecentActions = Config.Bind("Display", "MaxRecentActions", 5,
                new ConfigDescription("Maximum number of recent actions to display (3-10)",
                    new AcceptableValueRange<int>(3, 10)));

            Position = Config.Bind("Display", "Position", TimelinePosition.Bottom,
                "Position of the timeline on screen");

            TimeWindow = Config.Bind("Display", "TimeWindow", 5f,
                new ConfigDescription("How long actions remain visible in seconds",
                    new AcceptableValueRange<float>(2f, 10f)));

            Opacity = Config.Bind("Display", "Opacity", 0.9f,
                new ConfigDescription("Opacity of the timeline",
                    new AcceptableValueRange<float>(0.1f, 1f)));

            ShowTimestamps = Config.Bind("Display", "ShowTimestamps", true,
                "Show time since action occurred");

            ShowBackground = Config.Bind("Display", "ShowBackground", true,
                "Show background strip behind actions");

            ActionBoxColor = Config.Bind("Colors", "ActionBoxColor", new Color(0.2f, 0.3f, 0.4f, 1f),
                "Color for action boxes");

            HighlightColor = Config.Bind("Colors", "HighlightColor", new Color(0.4f, 0.6f, 0.8f, 1f),
                "Color for most recent action");

            BackgroundColor = Config.Bind("Colors", "BackgroundColor", new Color(0f, 0f, 0f, 0.5f),
                "Background color of the timeline strip");
        }
    }

    public enum TimelinePosition
    {
        Top,
        Bottom,
        Center
    }
}