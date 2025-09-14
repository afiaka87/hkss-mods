using UnityEngine;
using HarmonyLib;

namespace HKSS.AirTime
{
    public class AirTimeTracker : MonoBehaviour
    {
        private float currentAirTime = 0f;
        private float sessionTotalAirTime = 0f;
        private bool wasGrounded = true;
        private HeroController heroController;

        void Start()
        {
            AirTimePlugin.ModLogger?.LogInfo("AirTimeTracker started");
        }

        void Update()
        {
            if (heroController == null)
            {
                heroController = HeroController.instance;
                if (heroController == null)
                    return;
            }

            bool grounded = heroController.cState.onGround;

            if (!grounded)
            {
                currentAirTime += Time.deltaTime;
                sessionTotalAirTime += Time.deltaTime;
            }
            else if (!wasGrounded && grounded)
            {
                // Just landed - record the jump
                if (currentAirTime > 0.1f)
                {
                    AirTimeDisplay.RecordJump(currentAirTime);
                    AirTimePlugin.ModLogger?.LogInfo($"Jump completed: {currentAirTime:F2}s");
                }
                currentAirTime = 0f;
            }

            AirTimeDisplay.UpdateAirTime(currentAirTime, sessionTotalAirTime);
            wasGrounded = grounded;
        }
    }

    [HarmonyPatch]
    public static class AirTimePatches
    {
        [HarmonyPatch(typeof(HeroController), "Start")]
        [HarmonyPostfix]
        public static void OnHeroStart(HeroController __instance)
        {
            AirTimePlugin.ModLogger?.LogInfo("HeroController started - air time tracking active");
        }

        [HarmonyPatch(typeof(HeroController), "Jump")]
        [HarmonyPostfix]
        public static void OnJump(HeroController __instance)
        {
            AirTimePlugin.ModLogger?.LogDebug("Jump detected");
        }

        [HarmonyPatch(typeof(HeroController), "DoubleJump")]
        [HarmonyPostfix]
        public static void OnDoubleJump(HeroController __instance)
        {
            AirTimePlugin.ModLogger?.LogDebug("Double jump detected");
        }
    }
}