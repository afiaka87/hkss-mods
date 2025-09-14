using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace HKSS.ParryWindowFlash
{
    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.hkss.parrywindowflash";
        public const string PLUGIN_NAME = "Perfect Parry Window Flash";
        public const string PLUGIN_VERSION = "1.0.0";
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInProcess("Hollow Knight Silksong.exe")]
    public class ParryWindowPlugin : BaseUnityPlugin
    {
        public static ManualLogSource ModLogger { get; private set; }
        public static ParryWindowPlugin Instance { get; private set; }

        private Harmony harmony;
        private GameObject indicatorObject;

        public ConfigEntry<bool> Enabled { get; private set; }
        public ConfigEntry<Color> FlashColor { get; private set; }
        public ConfigEntry<float> FlashDuration { get; private set; }
        public ConfigEntry<bool> EarlyWarning { get; private set; }
        public ConfigEntry<float> EarlyWarningTime { get; private set; }
        public ConfigEntry<bool> AudioCue { get; private set; }
        public ConfigEntry<float> AudioVolume { get; private set; }
        public ConfigEntry<FlashType> FlashStyle { get; private set; }
        public ConfigEntry<float> FlashIntensity { get; private set; }
        public ConfigEntry<float> ParryRange { get; private set; }
        public ConfigEntry<float> ParryWindowOffset { get; private set; }
        public ConfigEntry<bool> ShowCharacterGlow { get; private set; }

        void Awake()
        {
            Instance = this;
            ModLogger = Logger;

            InitializeConfig();

            if (!Enabled.Value)
            {
                Logger.LogInfo("Perfect Parry Window Flash is disabled in config");
                return;
            }

            harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();

            CreateIndicatorObject();

            Logger.LogInfo($"Perfect Parry Window Flash v{PluginInfo.PLUGIN_VERSION} loaded!");
        }

        void OnDestroy()
        {
            harmony?.UnpatchSelf();
            if (indicatorObject != null)
            {
                Destroy(indicatorObject);
            }
        }

        private void InitializeConfig()
        {
            Enabled = Config.Bind("General", "Enabled", true,
                "Enable or disable the parry window flash");

            FlashColor = Config.Bind("Display", "FlashColor", Color.white,
                "Color of the parry window flash");

            FlashDuration = Config.Bind("Display", "FlashDuration", 0.1f,
                new ConfigDescription("Duration of the flash in seconds",
                new AcceptableValueRange<float>(0.05f, 0.2f)));

            EarlyWarning = Config.Bind("Display", "EarlyWarning", true,
                "Show an early warning indicator before the parry window");

            EarlyWarningTime = Config.Bind("Display", "EarlyWarningTime", 0.3f,
                new ConfigDescription("How early to show the warning (seconds)",
                new AcceptableValueRange<float>(0.1f, 0.5f)));

            AudioCue = Config.Bind("Audio", "AudioCue", false,
                "Play a sound effect during the parry window");

            AudioVolume = Config.Bind("Audio", "AudioVolume", 0.5f,
                new ConfigDescription("Volume of the audio cue",
                new AcceptableValueRange<float>(0f, 1f)));

            FlashStyle = Config.Bind("Display", "FlashStyle", FlashType.ScreenEdge,
                "Style of the flash indicator");

            FlashIntensity = Config.Bind("Display", "FlashIntensity", 0.8f,
                new ConfigDescription("Intensity of the flash effect",
                new AcceptableValueRange<float>(0.3f, 1f)));

            ParryRange = Config.Bind("Gameplay", "ParryRange", 5f,
                new ConfigDescription("Range at which parry detection activates",
                new AcceptableValueRange<float>(2f, 10f)));

            ParryWindowOffset = Config.Bind("Gameplay", "ParryWindowOffset", 0.1f,
                new ConfigDescription("Timing offset for the parry window",
                new AcceptableValueRange<float>(0f, 0.3f)));

            ShowCharacterGlow = Config.Bind("Display", "ShowCharacterGlow", true,
                "Show a glow effect on the character during parry window");
        }

        private void CreateIndicatorObject()
        {
            indicatorObject = new GameObject("ParryIndicator");
            indicatorObject.AddComponent<ParryIndicator>();
            DontDestroyOnLoad(indicatorObject);
        }
    }

    public enum FlashType
    {
        ScreenEdge,
        FullScreen,
        CharacterOnly,
        Corner
    }
}