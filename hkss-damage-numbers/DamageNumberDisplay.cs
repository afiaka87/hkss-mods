using System.Collections.Generic;
using UnityEngine;

namespace HKSS.DamageNumbers
{
    public class DamageNumberDisplay : MonoBehaviour
    {
        private static DamageNumberDisplay instance;
        private readonly List<DamageNumber> activeNumbers = new List<DamageNumber>();
        private GUIStyle normalStyle;
        private GUIStyle criticalStyle;

        private class DamageNumber
        {
            public string text;
            public float lifetime;
            public Vector3 worldPosition;
            public Vector3 velocity;
            public bool isCritical;
            public float scale;
            public Color color;
        }

        private void Awake()
        {
            instance = this;
            InitializeStyles();
        }

        private void InitializeStyles()
        {
            normalStyle = new GUIStyle();
            normalStyle.fontSize = (int)DamageNumbersPlugin.FontSize.Value;
            normalStyle.normal.textColor = ParseColor(DamageNumbersPlugin.NormalColor.Value);
            normalStyle.alignment = TextAnchor.MiddleCenter;
            normalStyle.fontStyle = FontStyle.Bold;

            criticalStyle = new GUIStyle();
            criticalStyle.fontSize = (int)(DamageNumbersPlugin.FontSize.Value * 1.3f);
            criticalStyle.normal.textColor = ParseColor(DamageNumbersPlugin.SpecialAttackColor.Value);
            criticalStyle.alignment = TextAnchor.MiddleCenter;
            criticalStyle.fontStyle = FontStyle.Bold;
        }

        public static void ShowDamage(Vector3 worldPosition, int damage, bool isCritical = false)
        {
            if (instance == null || !DamageNumbersPlugin.Enabled.Value)
                return;

            instance.SpawnDamageNumber(worldPosition, damage, isCritical);
        }

        private void SpawnDamageNumber(Vector3 worldPosition, int damage, bool isCritical)
        {
            var number = new DamageNumber
            {
                worldPosition = worldPosition + Vector3.up * 0.5f,
                lifetime = DamageNumbersPlugin.DisplayDuration.Value,
                isCritical = isCritical
            };

            // Set initial velocity with some randomness
            float randomX = Random.Range(-0.5f, 0.5f);
            number.velocity = new Vector3(randomX, DamageNumbersPlugin.FloatSpeed.Value, 0);

            // Configure text - show ACTUAL damage value
            number.text = damage.ToString();

            if (isCritical && DamageNumbersPlugin.ShowCriticalHits.Value)
            {
                // Special attack (nail art, ability, etc) - slightly larger and different color
                number.color = ParseColor(DamageNumbersPlugin.SpecialAttackColor.Value);
                number.scale = 1.3f;
            }
            else
            {
                // Normal attack
                number.color = ParseColor(DamageNumbersPlugin.NormalColor.Value);
                number.scale = 1f;
            }

            activeNumbers.Add(number);
        }

        private void Update()
        {
            if (!DamageNumbersPlugin.Enabled.Value)
            {
                activeNumbers.Clear();
                return;
            }

            // Update active damage numbers
            for (int i = activeNumbers.Count - 1; i >= 0; i--)
            {
                var number = activeNumbers[i];

                // Update lifetime
                number.lifetime -= Time.deltaTime;

                if (number.lifetime <= 0)
                {
                    activeNumbers.RemoveAt(i);
                    continue;
                }

                // Update position
                number.worldPosition += number.velocity * Time.deltaTime;

                // Apply gravity to velocity
                number.velocity.y -= 2f * Time.deltaTime;

                // Update color alpha for fade out
                float alpha = Mathf.Clamp01(number.lifetime / 0.5f);
                number.color.a = alpha;
            }
        }

        private void OnGUI()
        {
            if (!DamageNumbersPlugin.Enabled.Value || Camera.main == null)
                return;

            foreach (var number in activeNumbers)
            {
                // Convert world position to screen position
                Vector3 screenPos = Camera.main.WorldToScreenPoint(number.worldPosition);

                // Skip if behind camera
                if (screenPos.z < 0)
                    continue;

                // Flip Y coordinate (GUI uses top-left origin)
                screenPos.y = Screen.height - screenPos.y;

                // Set up style based on type
                GUIStyle style = number.isCritical ? criticalStyle : normalStyle;

                // Create a copy to modify color
                var tempStyle = new GUIStyle(style);
                tempStyle.normal.textColor = number.color;

                // Scale font size
                tempStyle.fontSize = (int)(style.fontSize * number.scale);

                // Draw shadow
                var shadowColor = new Color(0, 0, 0, number.color.a * 0.5f);
                var shadowStyle = new GUIStyle(tempStyle);
                shadowStyle.normal.textColor = shadowColor;

                Rect shadowRect = new Rect(screenPos.x - 50 + 2, screenPos.y - 20 + 2, 100, 40);
                GUI.Label(shadowRect, number.text, shadowStyle);

                // Draw main text
                Rect rect = new Rect(screenPos.x - 50, screenPos.y - 20, 100, 40);
                GUI.Label(rect, number.text, tempStyle);
            }
        }

        private Color ParseColor(string hexColor)
        {
            if (ColorUtility.TryParseHtmlString(hexColor, out Color color))
            {
                return color;
            }
            return Color.white;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}