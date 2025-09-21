using System.Collections.Generic;
using UnityEngine;

namespace HKSS.DamageNumbers
{
    public enum DamageType
    {
        Enemy,
        Player
    }

    public class DamageNumberDisplay : MonoBehaviour
    {
        private static DamageNumberDisplay instance;
        private readonly List<DamageNumber> activeNumbers = new List<DamageNumber>();
        private GUIStyle normalStyle;
        private Font currentFont;
        private string lastFontName;
        private int lastFontSize = -1;

        // Performance: Pre-allocate styles to avoid GC pressure
        private readonly Dictionary<int, GUIStyle> styleCache = new Dictionary<int, GUIStyle>();
        private readonly Dictionary<int, GUIStyle> outlineStyleCache = new Dictionary<int, GUIStyle>();

        private class DamageNumber
        {
            public string text;
            public float lifetime;
            public Vector3 worldPosition;
            public Vector3 velocity;
            public float scale;
            public Color color;
            public DamageType type;
            public float pulseTimer;
        }

        private void Awake()
        {
            instance = this;
            // Don't initialize styles in Awake - GUI context might not be ready
            // We'll do it lazily in OnGUI

            // Performance: Trigger font discovery early to avoid first-attack stutter
            StartCoroutine(PreloadFontsCoroutine());
        }

        private System.Collections.IEnumerator PreloadFontsCoroutine()
        {
            // Wait a frame to ensure everything is initialized
            yield return null;

            // Preload the font to avoid stutter on first damage
            string fontName = DamageNumbersPlugin.FontName.Value;
            FontLoader.PreloadFont(fontName);
        }

        private void InitializeStyles()
        {
            // Clear style caches when reinitializing
            styleCache.Clear();
            outlineStyleCache.Clear();

            // Load the configured font
            string fontName = DamageNumbersPlugin.FontName.Value;
            currentFont = FontLoader.GetFont(fontName);
            lastFontName = fontName;
            lastFontSize = CalculateScaledFontSize();

            // Log font loading for debugging
            if (currentFont != null)
            {
                DamageNumbersPlugin.Log.LogInfo($"Font loaded successfully: {fontName} -> {currentFont.name}");
            }
            else
            {
                DamageNumbersPlugin.Log.LogInfo($"Font not loaded, using default for: {fontName}");
            }

            normalStyle = new GUIStyle();
            if (currentFont != null)
            {
                normalStyle.font = currentFont;
                DamageNumbersPlugin.Log.LogInfo($"Font applied to style: {normalStyle.font?.name ?? "null"}");
            }
            // If font is null, GUIStyle will use the default font automatically
            normalStyle.fontSize = lastFontSize;
            normalStyle.alignment = TextAnchor.MiddleCenter;
            normalStyle.fontStyle = FontStyle.Bold;
        }

        private int CalculateScaledFontSize()
        {
            float baseFontSize = DamageNumbersPlugin.BaseFontSize.Value;

            if (!DamageNumbersPlugin.AutoScaleResolution.Value)
            {
                return (int)baseFontSize;
            }

            // Base resolution is 1280x720 (Steam Deck/720p)
            // Use the height for scaling as it's more consistent across aspect ratios
            float baseHeight = 720f;
            float currentHeight = Screen.height;

            // Linear scaling with adjustments for common resolutions
            float scaleFactor = currentHeight / baseHeight;

            // Add extra boost for high resolutions where linear isn't enough
            if (currentHeight >= 1440) // 1440p and above
            {
                scaleFactor *= 1.2f; // 20% extra boost
            }
            if (currentHeight >= 2160) // 4K
            {
                scaleFactor *= 1.15f; // Additional 15% for 4K (35% total boost)
            }

            // Apply minimum scale
            scaleFactor = Mathf.Max(scaleFactor, 1.0f);

            // Apply maximum scale to prevent text from being too large
            scaleFactor = Mathf.Min(scaleFactor, 4f);

            int scaledFontSize = Mathf.RoundToInt(baseFontSize * scaleFactor);

            // Log the scaling for debugging
            DamageNumbersPlugin.Log.LogInfo($"Resolution scaling: {Screen.width}x{Screen.height} -> Scale: {scaleFactor:F2} -> Font: {scaledFontSize}");

            return scaledFontSize;
        }

        public static void ShowDamage(Vector3 worldPosition, int damage, DamageType type = DamageType.Enemy)
        {
            if (instance == null || !DamageNumbersPlugin.Enabled.Value)
                return;

            instance.SpawnDamageNumber(worldPosition, damage, type);
        }

        private void SpawnDamageNumber(Vector3 worldPosition, int damage, DamageType type)
        {
            var number = new DamageNumber
            {
                worldPosition = worldPosition + Vector3.up * 0.5f,
                lifetime = DamageNumbersPlugin.DisplayDuration.Value,
                type = type,
                pulseTimer = 0f
            };

            // Set initial velocity with some randomness
            float randomX = Random.Range(-0.5f, 0.5f);

            if (type == DamageType.Enemy)
            {
                // Enemy damage floats up energetically (positive feedback)
                number.velocity = new Vector3(randomX, DamageNumbersPlugin.FloatSpeed.Value * 1.5f, 0);
                number.color = ParseColor(DamageNumbersPlugin.EnemyDamageColor.Value);
                number.scale = 1.1f; // Slightly larger for positive emphasis
            }
            else // Player damage
            {
                // Player damage sinks down (negative feedback)
                number.velocity = new Vector3(randomX * 1.5f, -DamageNumbersPlugin.FloatSpeed.Value * 0.5f, 0);
                number.color = ParseColor(DamageNumbersPlugin.PlayerDamageColor.Value);
                number.scale = 1f;
            }

            // Configure text - show ACTUAL damage value
            number.text = damage.ToString();

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

                if (number.type == DamageType.Enemy)
                {
                    // Enemy damage: floats up with slight deceleration
                    number.velocity.y -= 2f * Time.deltaTime;

                    // Slight bounce effect for positive feedback
                    number.scale = 1.1f + Mathf.Sin(Time.time * 10f) * 0.05f;
                }
                else // Player damage
                {
                    // Player damage: sinks down with acceleration
                    number.velocity.y -= 4f * Time.deltaTime;

                    // Pulsing effect for warning emphasis
                    number.pulseTimer += Time.deltaTime;
                    number.scale = 1f + Mathf.Sin(number.pulseTimer * 15f) * 0.15f;

                    // Add horizontal shake
                    number.worldPosition.x += Mathf.Sin(number.pulseTimer * 30f) * 0.02f;
                }

                // Update color alpha for fade out
                float alpha = Mathf.Clamp01(number.lifetime / 0.5f);
                number.color.a = alpha;
            }
        }

        private void OnGUI()
        {
            if (!DamageNumbersPlugin.Enabled.Value || Camera.main == null)
                return;

            // Initialize styles lazily in OnGUI context if needed
            if (normalStyle == null)
            {
                InitializeStyles();
            }

            // Only recalculate if actually changed (cache the font size)
            int currentFontSize = CalculateScaledFontSize();
            if (normalStyle != null && (lastFontSize != currentFontSize ||
                lastFontName != DamageNumbersPlugin.FontName.Value))
            {
                InitializeStyles();
            }

            foreach (var number in activeNumbers)
            {
                // Convert world position to screen position
                Vector3 screenPos = Camera.main.WorldToScreenPoint(number.worldPosition);

                // Skip if behind camera
                if (screenPos.z < 0)
                    continue;

                // Flip Y coordinate (GUI uses top-left origin)
                screenPos.y = Screen.height - screenPos.y;

                // Performance: Use cached styles instead of creating new ones
                int scaledFontSize = (int)(normalStyle.fontSize * number.scale);
                GUIStyle tempStyle = GetCachedStyle(scaledFontSize, number.color);

                // Draw outline/shadow if enabled
                if (DamageNumbersPlugin.UseOutline.Value)
                {
                    // Use nearly white outline for dark grey text
                    var outlineColor = new Color(0.95f, 0.95f, 0.95f, number.color.a * 0.9f);
                    GUIStyle outlineStyle = GetCachedOutlineStyle(scaledFontSize, outlineColor);

                    float outlineWidth = DamageNumbersPlugin.OutlineWidth.Value;

                    // Performance: Only draw 4-direction outline for better performance
                    // This still looks good but reduces draw calls from 8 to 4
                    DrawOutline(screenPos, number.text, outlineStyle, outlineWidth);
                }

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

        private GUIStyle GetCachedStyle(int fontSize, Color color)
        {
            // Use a simple hash for the cache key
            int key = fontSize.GetHashCode() ^ color.GetHashCode();

            if (!styleCache.TryGetValue(key, out GUIStyle cachedStyle))
            {
                cachedStyle = new GUIStyle(normalStyle);
                cachedStyle.fontSize = fontSize;
                if (currentFont != null)
                {
                    cachedStyle.font = currentFont;
                }
                styleCache[key] = cachedStyle;

                // Limit cache size to prevent memory issues
                if (styleCache.Count > 50)
                {
                    styleCache.Clear();
                    styleCache[key] = cachedStyle;
                }
            }

            // Update color (this is cheap and doesn't allocate)
            cachedStyle.normal.textColor = color;
            return cachedStyle;
        }

        private GUIStyle GetCachedOutlineStyle(int fontSize, Color color)
        {
            int key = fontSize.GetHashCode() ^ color.GetHashCode();

            if (!outlineStyleCache.TryGetValue(key, out GUIStyle cachedStyle))
            {
                cachedStyle = new GUIStyle(normalStyle);
                cachedStyle.fontSize = fontSize;
                if (currentFont != null)
                {
                    cachedStyle.font = currentFont;
                }
                outlineStyleCache[key] = cachedStyle;

                // Limit cache size
                if (outlineStyleCache.Count > 50)
                {
                    outlineStyleCache.Clear();
                    outlineStyleCache[key] = cachedStyle;
                }
            }

            cachedStyle.normal.textColor = color;
            return cachedStyle;
        }

        private void DrawOutline(Vector3 screenPos, string text, GUIStyle outlineStyle, float outlineWidth)
        {
            // 4-direction outline (top, bottom, left, right) instead of 8
            // This reduces draw calls by 50% while still looking good
            Rect rect;

            // Top
            rect = new Rect(screenPos.x - 50, screenPos.y - 20 - outlineWidth, 100, 40);
            GUI.Label(rect, text, outlineStyle);

            // Bottom
            rect = new Rect(screenPos.x - 50, screenPos.y - 20 + outlineWidth, 100, 40);
            GUI.Label(rect, text, outlineStyle);

            // Left
            rect = new Rect(screenPos.x - 50 - outlineWidth, screenPos.y - 20, 100, 40);
            GUI.Label(rect, text, outlineStyle);

            // Right
            rect = new Rect(screenPos.x - 50 + outlineWidth, screenPos.y - 20, 100, 40);
            GUI.Label(rect, text, outlineStyle);
        }
    }
}