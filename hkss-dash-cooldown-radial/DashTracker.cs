using UnityEngine;
using HarmonyLib;

namespace HKSS.DashCooldownRadial
{
    public class DashTracker : MonoBehaviour
    {
        private HeroController heroController;
        private float dashCooldownTimer = 0f;
        private float dashCooldownDuration = 0.4f; // Default dash cooldown in Hollow Knight
        private bool canDash = true;
        private bool wasInDash = false;
        private int frameCount = 0;

        void Awake()
        {
            DashCooldownPlugin.ModLogger?.LogInfo("[DashTracker] Awake called");
        }

        void Start()
        {
            DashCooldownPlugin.ModLogger?.LogInfo("[DashTracker] Start called - tracker is active");
        }

        void Update()
        {
            frameCount++;

            if (heroController == null)
            {
                heroController = HeroController.instance;
                if (heroController == null)
                {
                    if (frameCount % 60 == 0)
                    {
                        DashCooldownPlugin.ModLogger?.LogDebug("[DashTracker] Waiting for HeroController.instance...");
                    }
                    return;
                }
                else
                {
                    DashCooldownPlugin.ModLogger?.LogInfo("[DashTracker] HeroController found!");
                }
            }

            // Track dash state
            bool isInDash = heroController.cState.dashing || heroController.cState.backDashing;

            if (isInDash && !wasInDash)
            {
                // Just started dashing
                DashCooldownPlugin.ModLogger?.LogInfo($"[DashTracker] Dash started! (dashing: {heroController.cState.dashing}, backDashing: {heroController.cState.backDashing})");
                OnDashStart();
            }
            else if (!isInDash && wasInDash)
            {
                // Just finished dashing
                DashCooldownPlugin.ModLogger?.LogInfo("[DashTracker] Dash ended!");
                OnDashEnd();
            }

            wasInDash = isInDash;

            // Update cooldown timer
            if (!canDash && dashCooldownTimer > 0f)
            {
                dashCooldownTimer -= Time.deltaTime;

                if (dashCooldownTimer <= 0f)
                {
                    canDash = true;
                    dashCooldownTimer = 0f;
                    DashCooldownPlugin.ModLogger?.LogInfo("[DashTracker] Dash cooldown complete - dash is ready!");
                }

                // Log cooldown progress every 10 frames
                if (frameCount % 10 == 0)
                {
                    float percent = dashCooldownTimer / dashCooldownDuration;
                    DashCooldownPlugin.ModLogger?.LogDebug($"[DashTracker] Cooldown: {percent:P1} ({dashCooldownTimer:F2}s remaining)");
                }
            }

            // Calculate cooldown percentage
            float cooldownPercent = 0f;
            if (!canDash && dashCooldownDuration > 0f)
            {
                cooldownPercent = dashCooldownTimer / dashCooldownDuration;
            }

            // Update radial display
            RadialIndicator.UpdateCooldown(cooldownPercent, canDash);
        }

        private void OnDashStart()
        {
            canDash = false;
            dashCooldownTimer = dashCooldownDuration;
            DashCooldownPlugin.ModLogger?.LogInfo($"[DashTracker] Cooldown started: {dashCooldownDuration}s");
        }

        private void OnDashEnd()
        {
            // Dash ended - cooldown continues running
        }

        void OnDestroy()
        {
            DashCooldownPlugin.Instance?.DestroyRadialIndicator();
        }
    }

    [HarmonyPatch]
    public static class DashPatches
    {
        [HarmonyPatch(typeof(HeroController), "Start")]
        [HarmonyPostfix]
        public static void OnHeroStart(HeroController __instance)
        {
            DashCooldownPlugin.ModLogger?.LogInfo("[DashPatches] HeroController.Start() called - hero initialized");
        }

        [HarmonyPatch(typeof(HeroController), "HeroDash")]
        [HarmonyPostfix]
        public static void OnHeroDash(HeroController __instance)
        {
            DashCooldownPlugin.ModLogger?.LogInfo("[DashPatches] HeroDash() method called");
        }

        [HarmonyPatch(typeof(HeroController), "BackDash")]
        [HarmonyPostfix]
        public static void OnBackDash(HeroController __instance)
        {
            DashCooldownPlugin.ModLogger?.LogInfo("[DashPatches] BackDash() method called");
        }

        // Try to capture the actual dash cooldown value
        [HarmonyPatch(typeof(HeroController), "CanDash")]
        [HarmonyPostfix]
        public static void OnCanDashCheck(HeroController __instance, ref bool __result)
        {
            if (DashCooldownPlugin.ModLogger != null && Time.frameCount % 60 == 0)
            {
                DashCooldownPlugin.ModLogger.LogDebug($"[DashPatches] CanDash() returns: {__result}");
            }
        }
    }
}