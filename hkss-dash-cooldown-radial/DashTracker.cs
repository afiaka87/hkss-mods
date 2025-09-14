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

        void Start()
        {
            DashCooldownPlugin.ModLogger?.LogInfo("DashTracker started");
            DashCooldownPlugin.Instance.CreateRadialIndicator();
        }

        void Update()
        {
            if (heroController == null)
            {
                heroController = HeroController.instance;
                if (heroController == null)
                    return;
            }

            // Track dash state
            bool isInDash = heroController.cState.dashing || heroController.cState.backDashing;

            if (isInDash && !wasInDash)
            {
                // Just started dashing
                OnDashStart();
            }
            else if (!isInDash && wasInDash)
            {
                // Just finished dashing
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
            DashCooldownPlugin.ModLogger?.LogDebug("Dash started");
            canDash = false;
            dashCooldownTimer = dashCooldownDuration;
        }

        private void OnDashEnd()
        {
            DashCooldownPlugin.ModLogger?.LogDebug("Dash ended");
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
            DashCooldownPlugin.ModLogger?.LogInfo("HeroController started - dash tracking active");
        }

        [HarmonyPatch(typeof(HeroController), "HeroDash")]
        [HarmonyPostfix]
        public static void OnHeroDash(HeroController __instance)
        {
            DashCooldownPlugin.ModLogger?.LogDebug("HeroDash triggered");
        }

        [HarmonyPatch(typeof(HeroController), "BackDash")]
        [HarmonyPostfix]
        public static void OnBackDash(HeroController __instance)
        {
            DashCooldownPlugin.ModLogger?.LogDebug("BackDash triggered");
        }

        // Try to capture the actual dash cooldown value
        [HarmonyPatch(typeof(HeroController), "CanDash")]
        [HarmonyPostfix]
        public static void OnCanDashCheck(HeroController __instance, ref bool __result)
        {
            if (DashCooldownPlugin.ModLogger != null && Time.frameCount % 60 == 0)
            {
                DashCooldownPlugin.ModLogger.LogDebug($"CanDash: {__result}");
            }
        }
    }
}