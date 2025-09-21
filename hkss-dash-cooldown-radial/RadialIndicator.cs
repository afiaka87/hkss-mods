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
        private float animationTimer = 0f;
        private float dashStartTime = 0f;

        // Sine wave animation parameters
        private float[] sineWaveOffsets;
        private float[] sineWaveSpeeds;
        private const int WAVE_COUNT = 3;
        private const float WAVE_AMPLITUDE = 0.15f;
        private const float WAVE_DAMPING_SPEED = 2f;

        private Texture2D radialTexture;
        private Texture2D glowTexture;

        // Cache references for performance
        private SpriteRenderer cachedSpriteRenderer;
        private Collider2D cachedCollider;
        private Transform cachedTransform;

        private const int TEXTURE_SIZE = 128; // Higher quality for smooth edges
        private const float PULSE_SPEED = 2f;
        private const int RADIAL_SEGMENTS = 64; // More segments for smoother animation

        // Hollow Knight color palette
        private readonly Color HK_WHITE = new Color(0.95f, 0.95f, 0.90f);
        private readonly Color HK_SILK_RED = new Color(0.85f, 0.25f, 0.25f);
        private readonly Color HK_VOID_BLACK = new Color(0.08f, 0.08f, 0.12f);
        private readonly Color HK_SOUL_WHITE = new Color(0.95f, 0.95f, 0.98f);

        void Awake()
        {
            instance = this;
            DashCooldownPlugin.ModLogger?.LogInfo("[RadialIndicator] Awake called - initializing sine waves");
            InitializeSineWaves();
            // Defer texture creation to ensure proper initialization
        }

        private void InitializeSineWaves()
        {
            sineWaveOffsets = new float[WAVE_COUNT];
            sineWaveSpeeds = new float[WAVE_COUNT];

            for (int i = 0; i < WAVE_COUNT; i++)
            {
                sineWaveOffsets[i] = Random.Range(0f, Mathf.PI * 2f);
                sineWaveSpeeds[i] = Random.Range(1.5f, 3.5f) * (1f + i * 0.3f);
            }
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
            if (glowTexture != null)
                Destroy(glowTexture);
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

            if (!ready && isDashReady)
            {
                // Dash just started cooldown
                dashStartTime = Time.time;
                animationTimer = 0f;
                DashCooldownPlugin.ModLogger?.LogDebug($"[RadialIndicator] Dash started cooldown");
            }
            else if (ready && !isDashReady)
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

            // Update animation timer for sine waves
            if (!isDashReady)
            {
                animationTimer += Time.deltaTime;
            }
        }

        void OnGUI()
        {
            // Early return checks
            if (DashCooldownPlugin.Instance == null || !DashCooldownPlugin.Instance.Enabled.Value)
                return;

            if (hideTimer > 0f && DashCooldownPlugin.Instance.HideWhenAvailable.Value)
                return;

            // Ensure textures are created
            if (radialTexture == null || glowTexture == null)
            {
                CreateRadialTextures();
            }

            DrawRadialIndicator();
        }

        private void DrawRadialIndicator()
        {
            // Ensure we have required references
            if (HeroController.instance == null)
                return;

            // Use HeroController directly if cached transform is not available yet
            Transform characterTransform = cachedTransform ?? HeroController.instance.transform;
            if (characterTransform == null)
                return;

            // Check for camera
            if (Camera.main == null)
                return;

            // Get the character's position
            Vector3 worldPos = characterTransform.position;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

            // Calculate padding based on resolution
            float basePadding = 16f;
            if (Screen.height >= 1440) basePadding = 64f;
            else if (Screen.height >= 1080) basePadding = 48f;

            // Position to top-right of character with proper padding
            float xOffset = basePadding;
            float yOffset = basePadding;

            // Position relative to character
            Collider2D collider = cachedCollider ?? HeroController.instance.GetComponent<Collider2D>();
            if (collider != null)
            {
                Bounds bounds = collider.bounds;
                Vector3 topRight = Camera.main.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.max.y, worldPos.z));
                screenPos = topRight;
            }

            // Apply padding offsets
            screenPos.x += xOffset;
            screenPos.y = Screen.height - screenPos.y - yOffset; // Convert to GUI coordinates and apply top padding

            float size = TEXTURE_SIZE * 0.8f;
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
                opacity *= 0.85f + 0.15f * Mathf.Sin(pulseTimer);
            }

            // Draw glow/shadow effect
            if (!isDashReady)
            {
                Rect glowRect = new Rect(radialRect.x - 4, radialRect.y - 4, radialRect.width + 8, radialRect.height + 8);
                GUI.color = new Color(HK_SILK_RED.r, HK_SILK_RED.g, HK_SILK_RED.b, opacity * 0.3f);
                GUI.DrawTexture(glowRect, glowTexture);
            }

            // Draw background circle (Hollow Knight void black)
            GUI.color = new Color(HK_VOID_BLACK.r, HK_VOID_BLACK.g, HK_VOID_BLACK.b, opacity * 0.9f);
            GUI.DrawTexture(radialRect, radialTexture);

            // Draw radial fill animation with sine wave distortion
            if (!isDashReady && currentCooldownPercent > 0f)
            {
                DrawSineWaveRadial(radialRect, currentCooldownPercent, opacity);
            }
            else if (isDashReady)
            {
                // Full white circle when ready (Hollow Knight soul white)
                GUI.color = new Color(HK_SOUL_WHITE.r, HK_SOUL_WHITE.g, HK_SOUL_WHITE.b, opacity * 0.95f);
                GUI.DrawTexture(radialRect, radialTexture);
            }

            GUI.color = originalColor;
        }

        private void DrawSineWaveRadial(Rect rect, float fillPercent, float opacity)
        {
            // Create a custom texture for the sine wave radial
            Texture2D waveTexture = CreateSineWaveRadialTexture(fillPercent);

            if (waveTexture != null)
            {
                // Lerp color from red to white as it approaches ready
                Color fillColor = Color.Lerp(HK_SILK_RED, HK_WHITE, 1f - fillPercent);
                GUI.color = new Color(fillColor.r, fillColor.g, fillColor.b, opacity);
                GUI.DrawTexture(rect, waveTexture);
                Destroy(waveTexture); // Clean up temporary texture
            }
        }

        private Texture2D CreateSineWaveRadialTexture(float fillPercent)
        {
            var texture = new Texture2D(TEXTURE_SIZE, TEXTURE_SIZE, TextureFormat.ARGB32, false);
            Color[] pixels = new Color[TEXTURE_SIZE * TEXTURE_SIZE];

            int centerX = TEXTURE_SIZE / 2;
            int centerY = TEXTURE_SIZE / 2;
            float baseRadius = TEXTURE_SIZE / 2f - 8;
            float innerRadius = baseRadius - 8;

            // Calculate damping factor - sine waves calm down as dash approaches ready
            float dampingFactor = Mathf.Pow(fillPercent, 0.5f);
            float timeSinceDash = Time.time - dashStartTime;

            for (int y = 0; y < TEXTURE_SIZE; y++)
            {
                for (int x = 0; x < TEXTURE_SIZE; x++)
                {
                    float dx = x - centerX;
                    float dy = y - centerY;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx) + Mathf.PI / 2f; // Start from top

                    if (angle < 0) angle += Mathf.PI * 2f;

                    // Calculate sine wave distortion
                    float waveOffset = 0f;
                    for (int w = 0; w < WAVE_COUNT; w++)
                    {
                        float wavePhase = angle * (w + 2) + sineWaveOffsets[w] + animationTimer * sineWaveSpeeds[w];
                        waveOffset += Mathf.Sin(wavePhase) * WAVE_AMPLITUDE * dampingFactor / (w + 1);
                    }

                    // Apply wave to radius
                    float waveRadius = baseRadius + waveOffset * baseRadius;
                    float waveInnerRadius = innerRadius + waveOffset * innerRadius;

                    // Check if pixel should be filled based on angle
                    float fillAngle = (1f - fillPercent) * Mathf.PI * 2f;
                    bool shouldFill = angle <= fillAngle;

                    if (shouldFill && distance <= waveRadius && distance >= waveInnerRadius)
                    {
                        float alpha = 1f;

                        // Smooth edges
                        if (distance > waveRadius - 2)
                            alpha = (waveRadius - distance) / 2f;
                        else if (distance < waveInnerRadius + 2)
                            alpha = (distance - waveInnerRadius) / 2f;

                        // Fade at the fill edge
                        float edgeFade = 1f;
                        float angleToEdge = fillAngle - angle;
                        if (angleToEdge < 0.1f)
                            edgeFade = angleToEdge / 0.1f;

                        alpha *= edgeFade;
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

        private void CreateRadialTextures()
        {
            // Avoid recreating if already exists
            if (radialTexture != null && glowTexture != null)
                return;

            DashCooldownPlugin.ModLogger?.LogInfo($"[RadialIndicator] Creating textures (size: {TEXTURE_SIZE}x{TEXTURE_SIZE})");

            // Destroy old textures if they exist
            if (radialTexture != null)
                Destroy(radialTexture);
            if (glowTexture != null)
                Destroy(glowTexture);

            // Create the background circle
            radialTexture = CreateCircleTexture();

            // Create glow texture for effects
            glowTexture = CreateGlowTexture();

            DashCooldownPlugin.ModLogger?.LogInfo($"[RadialIndicator] Created radial textures");
        }

        private Texture2D CreateCircleTexture()
        {
            var texture = new Texture2D(TEXTURE_SIZE, TEXTURE_SIZE, TextureFormat.ARGB32, false);
            Color[] pixels = new Color[TEXTURE_SIZE * TEXTURE_SIZE];

            int centerX = TEXTURE_SIZE / 2;
            int centerY = TEXTURE_SIZE / 2;
            float radius = TEXTURE_SIZE / 2f - 8;
            float innerRadius = radius - 8;

            for (int y = 0; y < TEXTURE_SIZE; y++)
            {
                for (int x = 0; x < TEXTURE_SIZE; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));

                    if (distance <= radius && distance >= innerRadius)
                    {
                        float alpha = 1f;

                        // Smoother edge falloff
                        if (distance > radius - 2)
                            alpha = (radius - distance) / 2f;
                        else if (distance < innerRadius + 2)
                            alpha = (distance - innerRadius) / 2f;

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

        private Texture2D CreateGlowTexture()
        {
            var texture = new Texture2D(TEXTURE_SIZE + 16, TEXTURE_SIZE + 16, TextureFormat.ARGB32, false);
            Color[] pixels = new Color[(TEXTURE_SIZE + 16) * (TEXTURE_SIZE + 16)];

            int centerX = (TEXTURE_SIZE + 16) / 2;
            int centerY = (TEXTURE_SIZE + 16) / 2;
            float maxRadius = (TEXTURE_SIZE + 16) / 2f;

            for (int y = 0; y < TEXTURE_SIZE + 16; y++)
            {
                for (int x = 0; x < TEXTURE_SIZE + 16; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));

                    if (distance < maxRadius)
                    {
                        float alpha = 1f - (distance / maxRadius);
                        alpha = Mathf.Pow(alpha, 2f); // Quadratic falloff for softer glow
                        pixels[y * (TEXTURE_SIZE + 16) + x] = new Color(1f, 1f, 1f, alpha);
                    }
                    else
                    {
                        pixels[y * (TEXTURE_SIZE + 16) + x] = Color.clear;
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