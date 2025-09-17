using HarmonyLib;
using UnityEngine;
using System;

namespace HKSS.DamageNumbers
{
    // Patch for enemy damage - shows ACTUAL damage dealt
    [HarmonyPatch(typeof(HealthManager), "TakeDamage")]
    internal static class HealthManager_TakeDamage_Patch
    {
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