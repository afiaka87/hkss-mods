using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace HKSS.SecretProximity
{
    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.hkss.secretproximity";
        public const string PLUGIN_NAME = "Secret Proximity Sensor";
        public const string PLUGIN_VERSION = "1.0.0";
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInProcess("Hollow Knight Silksong.exe")]
    public class SecretProximityPlugin : BaseUnityPlugin
    {
        public static ManualLogSource ModLogger { get; private set; }
        public static SecretProximityPlugin Instance { get; private set; }

        private Harmony harmony;
        private GameObject sensorObject;

        public ConfigEntry<bool> Enabled { get; private set; }
        public ConfigEntry<bool> AudioFeedback { get; private set; }
        public ConfigEntry<float> AudioVolume { get; private set; }
        public ConfigEntry<bool> VisualPulse { get; private set; }
        public ConfigEntry<Color> ProximityColor { get; private set; }
        public ConfigEntry<float> DetectionRange { get; private set; }
        public ConfigEntry<float> PulseSpeed { get; private set; }
        public ConfigEntry<bool> ShowDistance { get; private set; }
        public ConfigEntry<bool> DirectionalIndicator { get; private set; }
        public ConfigEntry<float> IndicatorSize { get; private set; }
        public ConfigEntry<HUDPosition> IndicatorPosition { get; private set; }
        public ConfigEntry<bool> DetectGrubs { get; private set; }
        public ConfigEntry<bool> DetectCharms { get; private set; }
        public ConfigEntry<bool> DetectMasks { get; private set; }
        public ConfigEntry<bool> DetectVessels { get; private set; }
        public ConfigEntry<bool> DetectKeys { get; private set; }
        public ConfigEntry<bool> DetectEssence { get; private set; }

        void Awake()
        {
            Instance = this;
            ModLogger = Logger;

            InitializeConfig();

            if (!Enabled.Value)
            {
                Logger.LogInfo("Secret Proximity Sensor is disabled in config");
                return;
            }

            harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();

            CreateSensorObject();

            Logger.LogInfo($"Secret Proximity Sensor v{PluginInfo.PLUGIN_VERSION} loaded!");
        }

        void OnDestroy()
        {
            harmony?.UnpatchSelf();
            if (sensorObject != null)
            {
                Destroy(sensorObject);
            }
        }

        private void InitializeConfig()
        {
            Enabled = Config.Bind("General", "Enabled", true,
                "Enable or disable the proximity sensor");

            AudioFeedback = Config.Bind("Audio", "AudioFeedback", true,
                "Play audio feedback when near secrets");

            AudioVolume = Config.Bind("Audio", "AudioVolume", 0.5f,
                new ConfigDescription("Volume of audio feedback",
                new AcceptableValueRange<float>(0f, 1f)));

            VisualPulse = Config.Bind("Display", "VisualPulse", true,
                "Show visual pulse indicator");

            ProximityColor = Config.Bind("Display", "ProximityColor", new Color(1f, 0.8f, 0f),
                "Color of proximity indicator");

            DetectionRange = Config.Bind("Detection", "DetectionRange", 10f,
                new ConfigDescription("Range to detect secrets",
                new AcceptableValueRange<float>(5f, 30f)));

            PulseSpeed = Config.Bind("Display", "PulseSpeed", 2f,
                new ConfigDescription("Speed of pulse effect",
                new AcceptableValueRange<float>(0.5f, 5f)));

            ShowDistance = Config.Bind("Display", "ShowDistance", true,
                "Show distance to nearest secret");

            DirectionalIndicator = Config.Bind("Display", "DirectionalIndicator", true,
                "Show direction to nearest secret");

            IndicatorSize = Config.Bind("Display", "IndicatorSize", 50f,
                new ConfigDescription("Size of the indicator",
                new AcceptableValueRange<float>(30f, 100f)));

            IndicatorPosition = Config.Bind("Display", "IndicatorPosition", HUDPosition.TopCenter,
                "Position of the proximity indicator");

            // Detection filters
            DetectGrubs = Config.Bind("Detection", "DetectGrubs", true,
                "Detect grubs");

            DetectCharms = Config.Bind("Detection", "DetectCharms", true,
                "Detect charms");

            DetectMasks = Config.Bind("Detection", "DetectMasks", true,
                "Detect mask shards");

            DetectVessels = Config.Bind("Detection", "DetectVessels", true,
                "Detect vessel fragments");

            DetectKeys = Config.Bind("Detection", "DetectKeys", true,
                "Detect keys and special items");

            DetectEssence = Config.Bind("Detection", "DetectEssence", true,
                "Detect essence sources");
        }

        public void CreateSensorObject()
        {
            if (sensorObject == null)
            {
                sensorObject = new GameObject("SecretProximitySensor");
                sensorObject.AddComponent<ProximitySensor>();
                DontDestroyOnLoad(sensorObject);
            }
        }

        public void DestroySensorObject()
        {
            if (sensorObject != null)
            {
                Destroy(sensorObject);
                sensorObject = null;
            }
        }
    }

    public enum HUDPosition
    {
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }
}