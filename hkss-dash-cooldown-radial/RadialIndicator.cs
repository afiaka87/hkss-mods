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
        private Texture2D backgroundTexture;
        private bool texturesInitialized = false;

        private const int TEXTURE_SIZE = 128;
        private const float PULSE_SPEED = 3f;

        void Awake()
        {
            instance = this;
            CreateTextures();
        }

        void OnDestroy()
        {
            if (radialTexture != null)
                Destroy(radialTexture);
            if (backgroundTexture != null)
                Destroy(backgroundTexture);
        }

        public static void UpdateCooldown(float percent, bool ready)
        {
            if (instance != null)
            {
                instance.SetCooldown(percent, ready);
            }
        }

        private void SetCooldown(float percent, bool ready)
        {
            currentCooldownPercent = Mathf.Clamp01(percent);

            if (ready && !isDashReady)
            {
                // Dash just became ready
                hideTimer = DashCooldownPlugin.Instance.HideDelay.Value;
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
                CreateTextures();
                texturesInitialized = true;
            }

            DrawRadialIndicator();
        }

        private void DrawRadialIndicator()
        {
            if (HeroController.instance == null)
                return;

            // Get player position on screen
            Vector3 worldPos = HeroController.instance.transform.position;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

            // Adjust position based on config
            float yOffset = 0f;
            var position = DashCooldownPlugin.Instance.Position.Value;
            switch (position)
            {
                case RadialPosition.AboveCharacter:
                    yOffset = -100;
                    break;
                case RadialPosition.BelowCharacter:
                    yOffset = 100;
                    break;
                case RadialPosition.AroundCharacter:
                default:
                    yOffset = 0;
                    break;
            }

            screenPos.y = Screen.height - screenPos.y + yOffset; // Convert to GUI coordinates

            float size = TEXTURE_SIZE * DashCooldownPlugin.Instance.RadialSize.Value;
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

            // Draw background circle
            GUI.color = new Color(0.2f, 0.2f, 0.2f, opacity * 0.5f);
            GUI.DrawTexture(radialRect, backgroundTexture);

            // Draw radial fill
            Color fillColor = isDashReady
                ? DashCooldownPlugin.Instance.ReadyColor.Value
                : DashCooldownPlugin.Instance.CooldownColor.Value;
            fillColor.a = opacity;
            GUI.color = fillColor;

            // Create radial fill effect using rotation
            Matrix4x4 matrixBackup = GUI.matrix;

            float fillAngle = (1f - currentCooldownPercent) * 360f;
            int segments = Mathf.CeilToInt(fillAngle / 10f);

            for (int i = 0; i < segments; i++)
            {
                float angle = i * 10f;
                if (angle > fillAngle)
                    break;

                GUIUtility.RotateAroundPivot(angle, new Vector2(radialRect.center.x, radialRect.center.y));
                GUI.DrawTexture(radialRect, CreateSegmentTexture());
                GUI.matrix = matrixBackup;
            }

            GUI.color = originalColor;
        }

        private void CreateTextures()
        {
            // Create circular background texture
            backgroundTexture = new Texture2D(TEXTURE_SIZE, TEXTURE_SIZE, TextureFormat.ARGB32, false);
            Color[] pixels = new Color[TEXTURE_SIZE * TEXTURE_SIZE];

            int centerX = TEXTURE_SIZE / 2;
            int centerY = TEXTURE_SIZE / 2;
            float radius = TEXTURE_SIZE / 2f - 2;
            float innerRadius = radius - 10;

            for (int y = 0; y < TEXTURE_SIZE; y++)
            {
                for (int x = 0; x < TEXTURE_SIZE; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));

                    if (distance <= radius && distance >= innerRadius)
                    {
                        pixels[y * TEXTURE_SIZE + x] = Color.white;
                    }
                    else
                    {
                        pixels[y * TEXTURE_SIZE + x] = Color.clear;
                    }
                }
            }

            backgroundTexture.SetPixels(pixels);
            backgroundTexture.Apply();

            // Create radial segment texture
            radialTexture = CreateSegmentTexture();
        }

        private Texture2D CreateSegmentTexture()
        {
            Texture2D segmentTex = new Texture2D(TEXTURE_SIZE, TEXTURE_SIZE, TextureFormat.ARGB32, false);
            Color[] pixels = new Color[TEXTURE_SIZE * TEXTURE_SIZE];

            int centerX = TEXTURE_SIZE / 2;
            int centerY = TEXTURE_SIZE / 2;
            float radius = TEXTURE_SIZE / 2f - 2;
            float innerRadius = radius - 10;

            for (int y = 0; y < TEXTURE_SIZE; y++)
            {
                for (int x = 0; x < TEXTURE_SIZE; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));

                    if (distance <= radius && distance >= innerRadius)
                    {
                        // Create a wedge shape for radial fill
                        float angle = Mathf.Atan2(y - centerY, x - centerX) * Mathf.Rad2Deg;
                        if (angle < 0) angle += 360;

                        if (angle >= 0 && angle <= 10) // 10 degree segment
                        {
                            pixels[y * TEXTURE_SIZE + x] = Color.white;
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

            segmentTex.SetPixels(pixels);
            segmentTex.Apply();
            return segmentTex;
        }
    }
}