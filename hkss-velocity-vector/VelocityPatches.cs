using HarmonyLib;
using UnityEngine;

namespace HKSS.VelocityVector
{
    [HarmonyPatch]
    public static class VelocityPatches
    {
        [HarmonyPatch(typeof(HeroController), "FixedUpdate")]
        [HarmonyPostfix]
        public static void TrackVelocity(HeroController __instance)
        {
            if (__instance == null || __instance.rb2d == null)
                return;

            Vector2 velocity = __instance.rb2d.linearVelocity;
            VelocityDisplay.UpdateVelocity(velocity);

            if (VelocityVectorPlugin.ModLogger != null && Time.frameCount % 120 == 0)
            {
                float speed = velocity.magnitude;
                if (speed > 0.1f)
                {
                    VelocityVectorPlugin.ModLogger.LogDebug($"Velocity: {velocity}, Speed: {speed:F2}");
                }
            }
        }

        [HarmonyPatch(typeof(HeroController), "Start")]
        [HarmonyPostfix]
        public static void OnHeroStart(HeroController __instance)
        {
            VelocityVectorPlugin.ModLogger?.LogInfo("HeroController started - velocity tracking active");
        }
    }
}