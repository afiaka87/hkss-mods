using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

namespace HKSS.ParryWindowFlash
{
    [HarmonyPatch]
    public static class ParryPatches
    {
        private static HashSet<int> trackedAttacks = new HashSet<int>();

        [HarmonyPatch(typeof(HeroController), "Start")]
        [HarmonyPostfix]
        public static void OnHeroStart(HeroController __instance)
        {
            ParryWindowPlugin.ModLogger?.LogInfo("HeroController started - parry detection active");
        }

        // Patch enemy attack windup completion
        [HarmonyPatch(typeof(HealthManager), "Hit")]
        [HarmonyPrefix]
        public static void OnEnemyAttackHit(HealthManager __instance, HitInstance hitInstance)
        {
            if (__instance == null)
                return;

            // Check if this is an attack heading toward the player
            if (HeroController.instance != null && hitInstance.Source != null)
            {
                GameObject attacker = hitInstance.Source.gameObject;
                if (attacker != null)
                {
                    CheckForParryOpportunity(attacker, hitInstance);
                }
            }
        }

        // Patch when enemies begin their attack animations
        [HarmonyPatch(typeof(HealthManager), "TakeDamage")]
        [HarmonyPrefix]
        public static void OnPlayerAboutToTakeDamage(HealthManager __instance, HitInstance hitInstance)
        {
            // This is called when the player is about to take damage
            // Perfect time to show parry window
            if (__instance.gameObject == HeroController.instance?.gameObject)
            {
                ParryWindowPlugin.ModLogger?.LogDebug("Player about to take damage - parry window!");

                // Trigger immediate flash since damage is imminent
                ParryIndicator.TriggerParryFlash(0f, 0f);
            }
        }

        private static void CheckForParryOpportunity(GameObject attacker, HitInstance hitInstance)
        {
            if (HeroController.instance == null)
                return;

            Vector3 playerPos = HeroController.instance.transform.position;
            Vector3 attackerPos = attacker.transform.position;
            float distance = Vector2.Distance(playerPos, attackerPos);

            float parryRange = ParryWindowPlugin.Instance.ParryRange.Value;

            if (distance <= parryRange)
            {
                // Calculate time to impact based on attack properties
                float attackSpeed = EstimateAttackSpeed(hitInstance);
                float timeToImpact = distance / attackSpeed;

                // Apply configured offset
                float adjustedTime = timeToImpact - ParryWindowPlugin.Instance.ParryWindowOffset.Value;

                if (adjustedTime > 0f)
                {
                    ParryWindowPlugin.ModLogger?.LogDebug($"Parry opportunity detected! Distance: {distance:F2}, Time: {adjustedTime:F2}");
                    ParryIndicator.TriggerParryFlash(adjustedTime, distance);
                }
            }
        }

        private static float EstimateAttackSpeed(HitInstance hitInstance)
        {
            // Try to estimate attack speed based on attack type
            // In a real implementation, you would have specific values for different enemy types

            if (hitInstance.AttackType == AttackTypes.Nail)
            {
                return 15f; // Fast melee attack
            }
            else if (hitInstance.AttackType == AttackTypes.Spell)
            {
                return 10f; // Projectile speed
            }
            else
            {
                return 12f; // Default speed
            }
        }

        // Track enemy state changes that might indicate attacks
        [HarmonyPatch(typeof(HealthManager), "Invincible")]
        [HarmonyPostfix]
        public static void OnEnemyInvincible(HealthManager __instance, bool __result)
        {
            // Some enemies become invincible during attack windups
            if (__result && __instance.gameObject != HeroController.instance?.gameObject)
            {
                CheckProximityForParry(__instance.gameObject);
            }
        }

        private static void CheckProximityForParry(GameObject enemy)
        {
            if (enemy == null || HeroController.instance == null)
                return;

            float distance = Vector2.Distance(enemy.transform.position, HeroController.instance.transform.position);

            if (distance < ParryWindowPlugin.Instance.ParryRange.Value)
            {
                ParryWindowPlugin.ModLogger?.LogDebug($"Enemy {enemy.name} in parry range during invincible state");

                // Show early warning since enemy is preparing attack
                if (ParryWindowPlugin.Instance.EarlyWarning.Value)
                {
                    float warningTime = ParryWindowPlugin.Instance.EarlyWarningTime.Value;
                    ParryIndicator.TriggerParryFlash(warningTime, distance);
                }
            }
        }

        // Monitor player parry attempts
        [HarmonyPatch(typeof(HeroController), "NailParry")]
        [HarmonyPostfix]
        public static void OnPlayerParry(HeroController __instance)
        {
            ParryWindowPlugin.ModLogger?.LogInfo("Player attempted parry!");
        }

        // Track successful parries
        [HarmonyPatch(typeof(HeroController), "NailParryRecover")]
        [HarmonyPostfix]
        public static void OnSuccessfulParry(HeroController __instance)
        {
            ParryWindowPlugin.ModLogger?.LogInfo("Successful parry!");
        }
    }
}