using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace HKSS.DashCooldownRadial
{
    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.hkss.dashcooldownradial";
        public const string PLUGIN_NAME = "Dash Cooldown Radial";
        public const string PLUGIN_VERSION = "1.0.0";
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInProcess("Hollow Knight Silksong.exe")]
    public class DashCooldownPlugin : BaseUnityPlugin
    {
        public static ManualLogSource ModLogger { get; private set; }
        public static DashCooldownPlugin Instance { get; private set; }

        private Harmony harmony;
        private GameObject radialObject;

        public ConfigEntry<bool> Enabled { get; private set; }
        public ConfigEntry<float> RadialSize { get; private set; }
        public ConfigEntry<float> Opacity { get; private set; }
        public ConfigEntry<bool> PulseWhenReady { get; private set; }
        public ConfigEntry<bool> HideWhenAvailable { get; private set; }
        public ConfigEntry<float> HideDelay { get; private set; }
        public ConfigEntry<Color> CooldownColor { get; private set; }
        public ConfigEntry<Color> ReadyColor { get; private set; }
        public ConfigEntry<RadialPosition> Position { get; private set; }

        void Awake()
        {
            Instance = this;
            ModLogger = Logger;

            InitializeConfig();

            if (!Enabled.Value)
            {
                Logger.LogInfo("Dash Cooldown Radial is disabled in config");
                return;
            }

            harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();

            Logger.LogInfo($"Dash Cooldown Radial v{PluginInfo.PLUGIN_VERSION} loaded!");
        }

        void OnDestroy()
        {
            harmony?.UnpatchSelf();
            if (radialObject != null)
            {
                Destroy(radialObject);
            }
        }

        private void InitializeConfig()
        {
            Enabled = Config.Bind("General", "Enabled", true,
                "Enable or disable the dash cooldown radial");

            RadialSize = Config.Bind("Display", "RadialSize", 1.0f,
                new ConfigDescription("Size of the radial indicator relative to character",
                new AcceptableValueRange<float>(0.5f, 2.0f)));

            Opacity = Config.Bind("Display", "Opacity", 0.7f,
                new ConfigDescription("Opacity of the radial indicator",
                new AcceptableValueRange<float>(0.1f, 1.0f)));

            PulseWhenReady = Config.Bind("Display", "PulseWhenReady", true,
                "Pulse the indicator when dash is ready");

            HideWhenAvailable = Config.Bind("Display", "HideWhenAvailable", true,
                "Auto-hide the indicator after dash becomes available");

            HideDelay = Config.Bind("Display", "HideDelay", 0.5f,
                new ConfigDescription("Delay before hiding when dash is available",
                new AcceptableValueRange<float>(0f, 2f)));

            CooldownColor = Config.Bind("Display", "CooldownColor", new Color(1f, 0.3f, 0.3f, 1f),
                "Color when dash is on cooldown");

            ReadyColor = Config.Bind("Display", "ReadyColor", new Color(0.3f, 1f, 0.3f, 1f),
                "Color when dash is ready");

            Position = Config.Bind("Display", "Position", RadialPosition.AroundCharacter,
                "Position of the radial indicator");
        }

        public void CreateRadialIndicator()
        {
            if (radialObject != null)
                return;

            radialObject = new GameObject("DashCooldownRadial");
            radialObject.AddComponent<RadialIndicator>();
            radialObject.AddComponent<DashTracker>();
            DontDestroyOnLoad(radialObject);
        }

        public void DestroyRadialIndicator()
        {
            if (radialObject != null)
            {
                Destroy(radialObject);
                radialObject = null;
            }
        }
    }

    public enum RadialPosition
    {
        AroundCharacter,
        AboveCharacter,
        BelowCharacter
    }
}