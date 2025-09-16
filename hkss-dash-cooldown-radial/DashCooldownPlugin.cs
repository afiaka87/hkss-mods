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

            Logger.LogInfo("[DashCooldownRadial] Initializing plugin...");

            InitializeConfig();

            if (!Enabled.Value)
            {
                Logger.LogInfo("[DashCooldownRadial] Plugin is disabled in config");
                return;
            }

            Logger.LogInfo("[DashCooldownRadial] Creating GameObject and components...");
            CreateRadialIndicator();

            Logger.LogInfo("[DashCooldownRadial] Applying Harmony patches...");
            harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();

            Logger.LogInfo($"[DashCooldownRadial] v{PluginInfo.PLUGIN_VERSION} loaded successfully!");
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

            Opacity = Config.Bind("Display", "Opacity", 0.85f,
                new ConfigDescription("Opacity of the radial indicator",
                new AcceptableValueRange<float>(0.1f, 1.0f)));

            PulseWhenReady = Config.Bind("Display", "PulseWhenReady", true,
                "Pulse the indicator when dash is ready");

            HideWhenAvailable = Config.Bind("Display", "HideWhenAvailable", true,
                "Auto-hide the indicator after dash becomes available");

            HideDelay = Config.Bind("Display", "HideDelay", 0.5f,
                new ConfigDescription("Delay before hiding when dash is available",
                new AcceptableValueRange<float>(0f, 2f)));

            CooldownColor = Config.Bind("Display", "CooldownColor", new Color(180f/255f, 61f/255f, 62f/255f, 1f),
                "Color when dash is on cooldown (HK Red)");

            ReadyColor = Config.Bind("Display", "ReadyColor", new Color(120f/255f, 180f/255f, 120f/255f, 1f),
                "Color when dash is ready (Pastel Green)");

            Position = Config.Bind("Display", "Position", RadialPosition.AroundCharacter,
                "Position of the radial indicator");
        }

        public void CreateRadialIndicator()
        {
            if (radialObject != null)
            {
                Logger.LogWarning("[DashCooldownRadial] RadialIndicator already exists, skipping creation");
                return;
            }

            Logger.LogInfo("[DashCooldownRadial] Creating DashCooldownRadial GameObject");
            radialObject = new GameObject("DashCooldownRadial");

            Logger.LogInfo("[DashCooldownRadial] Adding RadialIndicator component");
            var radialIndicator = radialObject.AddComponent<RadialIndicator>();

            Logger.LogInfo("[DashCooldownRadial] Adding DashTracker component");
            var dashTracker = radialObject.AddComponent<DashTracker>();

            DontDestroyOnLoad(radialObject);
            Logger.LogInfo("[DashCooldownRadial] GameObject created and marked as DontDestroyOnLoad");
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