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
        public ConfigEntry<float> TimelineWidth { get; private set; }
        public ConfigEntry<float> TimelineHeight { get; private set; }
        public ConfigEntry<TimelinePosition> Position { get; private set; }
        public ConfigEntry<float> TimeWindow { get; private set; }
        public ConfigEntry<bool> ShowCombos { get; private set; }
        public ConfigEntry<float> ButtonSpacing { get; private set; }
        public ConfigEntry<float> Opacity { get; private set; }
        public ConfigEntry<bool> ShowButtonLabels { get; private set; }
        public ConfigEntry<bool> ShowTimestamps { get; private set; }
        public ConfigEntry<bool> HighlightHolds { get; private set; }
        public ConfigEntry<float> HoldThreshold { get; private set; }
        public ConfigEntry<Color> ButtonPressColor { get; private set; }
        public ConfigEntry<Color> ButtonHoldColor { get; private set; }
        public ConfigEntry<Color> ComboColor { get; private set; }
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
        }

        void Start()
        {
            timelineObject = new GameObject("InputTimeline");
            timelineObject.AddComponent<InputRecorder>();
            timelineObject.AddComponent<TimelineRenderer>();
            DontDestroyOnLoad(timelineObject);
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

            TimelineWidth = Config.Bind("Display", "TimelineWidth", 800f,
                new ConfigDescription("Width of the timeline in pixels",
                    new AcceptableValueRange<float>(200f, 1920f)));

            TimelineHeight = Config.Bind("Display", "TimelineHeight", 60f,
                new ConfigDescription("Height of the timeline in pixels",
                    new AcceptableValueRange<float>(30f, 200f)));

            Position = Config.Bind("Display", "Position", TimelinePosition.Bottom,
                "Position of the timeline on screen");

            TimeWindow = Config.Bind("Display", "TimeWindow", 5f,
                new ConfigDescription("Time window to display in seconds",
                    new AcceptableValueRange<float>(1f, 20f)));

            ShowCombos = Config.Bind("Display", "ShowCombos", true,
                "Highlight combo sequences");

            ButtonSpacing = Config.Bind("Display", "ButtonSpacing", 5f,
                new ConfigDescription("Spacing between button indicators",
                    new AcceptableValueRange<float>(0f, 20f)));

            Opacity = Config.Bind("Display", "Opacity", 0.8f,
                new ConfigDescription("Opacity of the timeline",
                    new AcceptableValueRange<float>(0.1f, 1f)));

            ShowButtonLabels = Config.Bind("Display", "ShowButtonLabels", true,
                "Show button names on the timeline");

            ShowTimestamps = Config.Bind("Display", "ShowTimestamps", false,
                "Show timing information");

            HighlightHolds = Config.Bind("Display", "HighlightHolds", true,
                "Highlight held buttons differently");

            HoldThreshold = Config.Bind("Display", "HoldThreshold", 0.3f,
                new ConfigDescription("Time in seconds before a press becomes a hold",
                    new AcceptableValueRange<float>(0.1f, 2f)));

            ButtonPressColor = Config.Bind("Colors", "ButtonPressColor", new Color(0.4f, 0.8f, 1f, 1f),
                "Color for regular button presses");

            ButtonHoldColor = Config.Bind("Colors", "ButtonHoldColor", new Color(1f, 0.8f, 0.4f, 1f),
                "Color for held buttons");

            ComboColor = Config.Bind("Colors", "ComboColor", new Color(1f, 0.4f, 0.4f, 1f),
                "Color for combo indicators");

            BackgroundColor = Config.Bind("Colors", "BackgroundColor", new Color(0f, 0f, 0f, 0.5f),
                "Background color of the timeline");
        }
    }

    public enum TimelinePosition
    {
        Top,
        Bottom,
        Center
    }
}