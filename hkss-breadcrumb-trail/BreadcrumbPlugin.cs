using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace HKSS.BreadcrumbTrail
{
    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.hkss.breadcrumbtrail";
        public const string PLUGIN_NAME = "Breadcrumb Trail";
        public const string PLUGIN_VERSION = "1.0.0";
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInProcess("Hollow Knight Silksong.exe")]
    public class BreadcrumbPlugin : BaseUnityPlugin
    {
        public static ManualLogSource ModLogger { get; private set; }
        public static BreadcrumbPlugin Instance { get; private set; }

        private Harmony harmony;
        private GameObject trailObject;

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

        void Awake()
        {
            Instance = this;
            ModLogger = Logger;

            InitializeConfig();

            if (!Enabled.Value)
            {
                Logger.LogInfo("Breadcrumb Trail is disabled in config");
                return;
            }

            harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();

            Logger.LogInfo($"Breadcrumb Trail v{PluginInfo.PLUGIN_VERSION} loaded!");
        }

        void OnDestroy()
        {
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

            TrailDuration = Config.Bind("Trail", "Duration", 10f,
                new ConfigDescription("How long trail points remain visible (seconds)",
                new AcceptableValueRange<float>(3f, 30f)));

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

            MaxPoints = Config.Bind("Performance", "MaxPoints", 300,
                new ConfigDescription("Maximum number of trail points",
                new AcceptableValueRange<int>(50, 1000)));
        }

        public void CreateTrailObject()
        {
            if (trailObject != null)
                return;

            trailObject = new GameObject("BreadcrumbTrail");
            trailObject.AddComponent<BreadcrumbTrail>();
            DontDestroyOnLoad(trailObject);
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
}