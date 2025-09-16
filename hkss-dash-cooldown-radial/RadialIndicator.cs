using UnityEngine;
using System.Collections;

namespace HKSS.DashCooldownRadial
{
    public class RadialIndicator : MonoBehaviour
    {
        private static RadialIndicator instance;

        private float currentCooldownPercent = 0f;
        private bool isDashReady = true;
        private float hideTimer = 0f;
        private float pulseTimer = 0f;

        private Texture2D radialTexture;
        private Texture2D[] radialSegments; // Pre-rendered segments for animation
        private bool texturesInitialized = false;

        // Cache references for performance
        private SpriteRenderer cachedSpriteRenderer;
        private Collider2D cachedCollider;
        private Transform cachedTransform;

        private const int TEXTURE_SIZE = 64; // Reduced for performance
        private const float PULSE_SPEED = 3f;
        private const int SEGMENT_COUNT = 16; // Number of segments for smooth fill

        void Awake()
        {
            instance = this;
            DashCooldownPlugin.ModLogger?.LogInfo("[RadialIndicator] Awake called - creating textures");
            CreateRadialTextures();
        }

        void Start()
        {
            // Cache references when HeroController becomes available
            StartCoroutine(CacheReferences());
        }

        System.Collections.IEnumerator CacheReferences()
        {
            while (HeroController.instance == null)
                yield return new WaitForSeconds(0.1f);

            cachedTransform = HeroController.instance.transform;
            cachedSpriteRenderer = HeroController.instance.GetComponent<SpriteRenderer>();
            cachedCollider = HeroController.instance.GetComponent<Collider2D>();

            DashCooldownPlugin.ModLogger?.LogInfo($"[RadialIndicator] Cached references - SpriteRenderer: {cachedSpriteRenderer != null}, Collider: {cachedCollider != null}");
        }

        void OnDestroy()
        {
            if (radialTexture != null)
                Destroy(radialTexture);
            if (radialSegments != null)
            {
                foreach (var segment in radialSegments)
                {
                    if (segment != null)
                        Destroy(segment);
                }
            }
        }

        public static void UpdateCooldown(float percent, bool ready)
        {
            if (instance != null)
            {
                instance.SetCooldown(percent, ready);
            }
            else
            {
                DashCooldownPlugin.ModLogger?.LogWarning("[RadialIndicator] UpdateCooldown called but instance is null!");
            }
        }

        private void SetCooldown(float percent, bool ready)
        {
            currentCooldownPercent = Mathf.Clamp01(percent);

            if (ready && !isDashReady)
            {
                // Dash just became ready
                hideTimer = DashCooldownPlugin.Instance.HideDelay.Value;
                DashCooldownPlugin.ModLogger?.LogDebug($"[RadialIndicator] Dash ready! Starting hide timer: {hideTimer}s");
            }

            isDashReady = ready;
        }

        void Update()
        {
            if (isDashReady && DashCooldownPlugin.Instance.HideWhenAvailable.Value)
            {
                hideTimer -= Time.deltaTime;
            }
            else
            {
                hideTimer = 0f;
            }

            if (isDashReady && DashCooldownPlugin.Instance.PulseWhenReady.Value)
            {
                pulseTimer += Time.deltaTime * PULSE_SPEED;
            }
            else
            {
                pulseTimer = 0f;
            }
        }

        void OnGUI()
        {
            if (!DashCooldownPlugin.Instance.Enabled.Value)
                return;

            if (hideTimer > 0f && DashCooldownPlugin.Instance.HideWhenAvailable.Value)
                return;

            if (!texturesInitialized)
            {
                DashCooldownPlugin.ModLogger?.LogWarning("[RadialIndicator] Textures not initialized in OnGUI, recreating...");
                CreateRadialTextures();
                texturesInitialized = true;
            }

            DrawRadialIndicator();
        }

        private void DrawRadialIndicator()
        {
            if (HeroController.instance == null || cachedTransform == null)
                return;

            // Get the character's position and bounds
            Vector3 worldPos = cachedTransform.position;
            float spriteTopOffset = 0f;
            float dynamicXOffset = 0f;

            // First try collider bounds (more reliable for different poses)
            if (cachedCollider != null)
            {
                Bounds bounds = cachedCollider.bounds;
                // Use the top-right corner of the collider
                worldPos.x = bounds.center.x;
                worldPos.y = bounds.max.y;
                spriteTopOffset = bounds.size.y * 0.2f;
                dynamicXOffset = bounds.size.x * 0.5f; // Offset based on character width
            }
            // Fall back to sprite renderer if no collider
            else if (cachedSpriteRenderer != null && cachedSpriteRenderer.sprite != null)
            {
                Bounds bounds = cachedSpriteRenderer.bounds;
                worldPos.x = bounds.center.x;
                worldPos.y = bounds.max.y;
                spriteTopOffset = bounds.size.y * 0.2f;
                dynamicXOffset = bounds.size.x * 0.5f;
            }
            else
            {
                // Final fallback: estimate based on typical character dimensions
                worldPos.y += 1.5f;
                dynamicXOffset = 20f;
            }

            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

            // Dynamic positioning that accounts for character bounds
            float xOffset = 80f + dynamicXOffset; // Even farther to the right
            float yOffset = -140f - spriteTopOffset; // Higher above character

            // Check if character is facing left (sprite might be flipped)
            if (cachedSpriteRenderer != null && cachedSpriteRenderer.flipX)
            {
                xOffset = -xOffset; // Mirror the position when facing left
            }

            screenPos.x += xOffset;
            screenPos.y = Screen.height - screenPos.y + yOffset; // Convert to GUI coordinates

            float size = TEXTURE_SIZE * 0.7f; // Even smaller for less obstruction
            float halfSize = size / 2f;

            Rect radialRect = new Rect(
                screenPos.x - halfSize,
                screenPos.y - halfSize,
                size,
                size
            );

            // Apply opacity
            Color originalColor = GUI.color;
            float opacity = DashCooldownPlugin.Instance.Opacity.Value;

            // Pulse effect when ready
            if (isDashReady && DashCooldownPlugin.Instance.PulseWhenReady.Value)
            {
                opacity *= 0.7f + 0.3f * Mathf.Sin(pulseTimer);
            }

            // Draw background circle (darker, but more visible)
            GUI.color = new Color(0.15f, 0.15f, 0.15f, opacity * 0.75f);
            GUI.DrawTexture(radialRect, radialTexture);

            // Draw radial fill animation
            if (!isDashReady && currentCooldownPercent > 0f)
            {
                DrawRadialFill(radialRect, currentCooldownPercent, new Color(180f/255f, 61f/255f, 62f/255f, opacity));
            }
            else if (isDashReady)
            {
                // Full green circle when ready
                GUI.color = new Color(120f/255f, 180f/255f, 120f/255f, opacity);
                GUI.DrawTexture(radialRect, radialTexture);
            }

            GUI.color = originalColor;
        }

        private void DrawRadialFill(Rect rect, float fillPercent, Color color)
        {
            if (radialSegments == null || radialSegments.Length == 0)
                return;

            GUI.color = color;

            // Calculate how many segments to draw based on fill percent
            float fillAngle = (1f - fillPercent) * 360f;
            int segmentsToRender = Mathf.CeilToInt(fillAngle / (360f / SEGMENT_COUNT));

            Matrix4x4 matrixBackup = GUI.matrix;
            Vector2 pivot = new Vector2(rect.center.x, rect.center.y);

            // Draw segments in a circle, starting from top
            for (int i = 0; i < segmentsToRender && i < SEGMENT_COUNT; i++)
            {
                float segmentAngle = i * (360f / SEGMENT_COUNT) - 90f; // Start from top

                // Skip segments beyond the fill amount
                if (segmentAngle - (-90f) > fillAngle)
                    break;

                GUI.matrix = matrixBackup;
                GUIUtility.RotateAroundPivot(segmentAngle, pivot);
                GUI.DrawTexture(rect, radialSegments[i % radialSegments.Length]);
            }

            GUI.matrix = matrixBackup;
        }

        private void CreateRadialTextures()
        {
            DashCooldownPlugin.ModLogger?.LogInfo($"[RadialIndicator] Creating textures (size: {TEXTURE_SIZE}x{TEXTURE_SIZE})");

            // Create the background circle
            radialTexture = CreateCircleTexture();

            // Create segment textures for animation
            radialSegments = new Texture2D[SEGMENT_COUNT];
            for (int i = 0; i < SEGMENT_COUNT; i++)
            {
                radialSegments[i] = CreateSegmentTexture(i);
            }

            texturesInitialized = true;
            DashCooldownPlugin.ModLogger?.LogInfo($"[RadialIndicator] Created {SEGMENT_COUNT} segment textures");
        }

        private Texture2D CreateCircleTexture()
        {
            var texture = new Texture2D(TEXTURE_SIZE, TEXTURE_SIZE, TextureFormat.ARGB32, false);
            Color[] pixels = new Color[TEXTURE_SIZE * TEXTURE_SIZE];

            int centerX = TEXTURE_SIZE / 2;
            int centerY = TEXTURE_SIZE / 2;
            float radius = TEXTURE_SIZE / 2f - 1;
            float innerRadius = radius - 6;

            for (int y = 0; y < TEXTURE_SIZE; y++)
            {
                for (int x = 0; x < TEXTURE_SIZE; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));

                    if (distance <= radius && distance >= innerRadius)
                    {
                        float alpha = 1f;
                        if (distance > radius - 1)
                            alpha = radius - distance;
                        else if (distance < innerRadius + 1)
                            alpha = distance - innerRadius;

                        pixels[y * TEXTURE_SIZE + x] = new Color(1f, 1f, 1f, alpha);
                    }
                    else
                    {
                        pixels[y * TEXTURE_SIZE + x] = Color.clear;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }

        private Texture2D CreateSegmentTexture(int segmentIndex)
        {
            var texture = new Texture2D(TEXTURE_SIZE, TEXTURE_SIZE, TextureFormat.ARGB32, false);
            Color[] pixels = new Color[TEXTURE_SIZE * TEXTURE_SIZE];

            int centerX = TEXTURE_SIZE / 2;
            int centerY = TEXTURE_SIZE / 2;
            float radius = TEXTURE_SIZE / 2f - 1;
            float innerRadius = radius - 6;

            float segmentAngle = 360f / SEGMENT_COUNT;
            float startAngle = 0; // Segment starts at top
            float endAngle = segmentAngle;

            for (int y = 0; y < TEXTURE_SIZE; y++)
            {
                for (int x = 0; x < TEXTURE_SIZE; x++)
                {
                    float dx = x - centerX;
                    float dy = y - centerY;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    if (distance <= radius && distance >= innerRadius)
                    {
                        // Calculate angle from center (0 = top, going clockwise)
                        float angle = Mathf.Atan2(dx, -dy) * Mathf.Rad2Deg;
                        if (angle < 0) angle += 360;

                        // Check if pixel is within this segment's angle range
                        if (angle >= startAngle && angle <= endAngle)
                        {
                            float alpha = 1f;
                            if (distance > radius - 1)
                                alpha = radius - distance;
                            else if (distance < innerRadius + 1)
                                alpha = distance - innerRadius;

                            pixels[y * TEXTURE_SIZE + x] = new Color(1f, 1f, 1f, alpha);
                        }
                        else
                        {
                            pixels[y * TEXTURE_SIZE + x] = Color.clear;
                        }
                    }
                    else
                    {
                        pixels[y * TEXTURE_SIZE + x] = Color.clear;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }
    }
}