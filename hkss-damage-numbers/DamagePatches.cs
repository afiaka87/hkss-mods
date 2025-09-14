using HarmonyLib;
using UnityEngine;
using System;

namespace HKSS.DamageNumbers
{
    // Patch for enemy damage - shows ACTUAL damage dealt
    [HarmonyPatch(typeof(HealthManager), "TakeDamage")]
    internal static class HealthManager_TakeDamage_Patch
    {
        private static void Postfix(HealthManager __instance, HitInstance hitInstance)
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

                // Check if this was a critical/special hit based on ACTUAL game mechanics
                // We detect this from the game's actual multiplier or attack type
                bool isSpecialAttack = false;

                // If the game applied a significant multiplier, it's a special attack
                // Normal attacks typically have a multiplier of 1.0f
                if (hitInstance.Multiplier > 1.2f && DamageNumbersPlugin.ShowCriticalHits.Value)
                {
                    // This is a special/boosted attack as determined by the GAME
                    isSpecialAttack = true;
                    if (DamageNumbersPlugin.DebugLogging.Value)
                        DamageNumbersPlugin.Log.LogInfo($"Special attack detected - Multiplier: {hitInstance.Multiplier}");
                }

                // Get enemy position
                Vector3 position = __instance.transform.position;

                // Display the ACTUAL damage number - exactly what the game calculated
                DamageNumberDisplay.ShowDamage(position, actualDamage, isSpecialAttack);

                if (DamageNumbersPlugin.DebugLogging.Value)
                    DamageNumbersPlugin.Log.LogInfo($"Actual damage: {actualDamage} | Multiplier: {hitInstance.Multiplier} | AttackType: {hitInstance.AttackType}");
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
        private static void Postfix(HeroController __instance, GameObject go, GlobalEnums.HazardType hazardType, int damageAmount)
        {
            try
            {
                if (!DamageNumbersPlugin.Enabled.Value || !DamageNumbersPlugin.ShowPlayerDamage.Value)
                    return;

                if (damageAmount <= 0)
                    return;

                // Get player position
                Vector3 position = __instance.transform.position;

                // Display damage number (player damage is shown in red)
                DamageNumberDisplay.ShowDamage(position, damageAmount, false);

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