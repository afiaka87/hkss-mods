using HarmonyLib;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace HKSS.DamageNumbers
{
    // Patch for enemy damage - shows ACTUAL damage dealt
    [HarmonyPatch(typeof(HealthManager), "TakeDamage")]
    internal static class HealthManager_TakeDamage_Patch
    {
        // Track last damage time per enemy to prevent spam
        private static readonly Dictionary<HealthManager, float> lastDamageTime = new Dictionary<HealthManager, float>();
        private static void Postfix(HealthManager __instance, in HitInstance hitInstance)
        {
            try
            {
                if (!DamageNumbersPlugin.Enabled.Value)
                    return;

                // Only show damage from player attacks
                if (!hitInstance.IsHeroDamage)
                    return;

                // Get the ACTUAL damage dealt - no bullshit modifications
                int actualDamage = hitInstance.DamageDealt;
                if (actualDamage <= 0)
                    return;

                // Check cooldown to prevent spam from overlapping hitboxes
                float currentTime = Time.time;
                float cooldown = DamageNumbersPlugin.DamageCooldown.Value;

                if (cooldown > 0f && lastDamageTime.TryGetValue(__instance, out float lastTime))
                {
                    if (currentTime - lastTime < cooldown)
                    {
                        // Still in cooldown, skip this damage number
                        if (DamageNumbersPlugin.DebugLogging.Value)
                            DamageNumbersPlugin.Log.LogInfo($"Skipping damage number due to cooldown (last: {lastTime}, current: {currentTime}, cooldown: {cooldown})");
                        return;
                    }
                }

                // Update last damage time
                lastDamageTime[__instance] = currentTime;

                // Clean up old entries periodically to prevent memory leak
                if (lastDamageTime.Count > 100)
                {
                    var toRemove = new List<HealthManager>();
                    foreach (var kvp in lastDamageTime)
                    {
                        if (kvp.Key == null || currentTime - kvp.Value > 10f)
                        {
                            toRemove.Add(kvp.Key);
                        }
                    }
                    foreach (var key in toRemove)
                    {
                        lastDamageTime.Remove(key);
                    }
                }

                // Get enemy position
                Vector3 position = __instance.transform.position;

                // Display the ACTUAL damage number - exactly what the game calculated
                DamageNumberDisplay.ShowDamage(position, actualDamage, DamageType.Enemy);

                if (DamageNumbersPlugin.DebugLogging.Value)
                    DamageNumbersPlugin.Log.LogInfo($"Actual damage: {actualDamage}");
            }
            catch (Exception e)
            {
                DamageNumbersPlugin.Log.LogError($"Error in damage display: {e}");
            }
        }
    }

    // Patch for player damage (optional)
    [HarmonyPatch(typeof(HeroController), "TakeDamage")]
    internal static class HeroController_TakeDamage_Patch
    {
        // Track last player damage time to prevent spam
        private static float lastPlayerDamageTime = 0f;
        private static void Postfix(HeroController __instance, GameObject go, GlobalEnums.HazardType hazardType, int damageAmount)
        {
            try
            {
                if (!DamageNumbersPlugin.Enabled.Value || !DamageNumbersPlugin.ShowPlayerDamage.Value)
                    return;

                if (damageAmount <= 0)
                    return;

                // Check cooldown to prevent spam
                float currentTime = Time.time;
                float cooldown = DamageNumbersPlugin.DamageCooldown.Value;

                if (cooldown > 0f && currentTime - lastPlayerDamageTime < cooldown)
                {
                    if (DamageNumbersPlugin.DebugLogging.Value)
                        DamageNumbersPlugin.Log.LogInfo($"Skipping player damage number due to cooldown");
                    return;
                }

                lastPlayerDamageTime = currentTime;

                // Get player position
                Vector3 position = __instance.transform.position;

                // Display damage number with player damage type for distinct animation
                DamageNumberDisplay.ShowDamage(position, damageAmount, DamageType.Player);

                if (DamageNumbersPlugin.DebugLogging.Value)
                    DamageNumbersPlugin.Log.LogInfo($"Player took {damageAmount} damage from {hazardType}");
            }
            catch (Exception e)
            {
                DamageNumbersPlugin.Log.LogError($"Error in player damage display: {e}");
            }
        }
    }
}