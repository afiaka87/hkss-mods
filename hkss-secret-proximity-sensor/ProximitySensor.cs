using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace HKSS.SecretProximity
{
    public class SecretInfo
    {
        public GameObject gameObject;
        public SecretType type;
        public float distance;
        public Vector3 direction;
        public string name;
    }

    public enum SecretType
    {
        Grub,
        Charm,
        MaskShard,
        VesselFragment,
        Key,
        Essence,
        Other
    }

    public class ProximitySensor : MonoBehaviour
    {
        private List<SecretInfo> nearbySecrets = new List<SecretInfo>();
        private SecretInfo closestSecret;
        private HeroController heroController;
        private AudioSource audioSource;
        private float pulseTime = 0f;
        private float lastBeepTime = 0f;
        private Texture2D indicatorTexture;
        private Texture2D arrowTexture;
        private GUIStyle distanceStyle;

        void Start()
        {
            SecretProximityPlugin.ModLogger?.LogInfo("ProximitySensor started");
            CreateTextures();
            SetupAudio();
            InitializeGUIStyles();
        }

        void CreateTextures()
        {
            // Create indicator texture
            indicatorTexture = new Texture2D(64, 64);
            DrawCircle(indicatorTexture, 32, 32, 30, Color.white);
            indicatorTexture.Apply();

            // Create arrow texture
            arrowTexture = new Texture2D(32, 32);
            DrawArrow(arrowTexture, Color.white);
            arrowTexture.Apply();
        }

        void DrawCircle(Texture2D tex, int cx, int cy, int r, Color col)
        {
            // Clear texture
            Color[] clearColors = new Color[tex.width * tex.height];
            for (int i = 0; i < clearColors.Length; i++)
                clearColors[i] = Color.clear;
            tex.SetPixels(clearColors);

            // Draw circle
            for (int x = 0; x < tex.width; x++)
            {
                for (int y = 0; y < tex.height; y++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist <= r && dist >= r - 3)
                    {
                        tex.SetPixel(x, y, col);
                    }
                }
            }
        }

        void DrawArrow(Texture2D tex, Color col)
        {
            // Clear texture
            Color[] clearColors = new Color[tex.width * tex.height];
            for (int i = 0; i < clearColors.Length; i++)
                clearColors[i] = Color.clear;
            tex.SetPixels(clearColors);

            // Draw arrow pointing up
            int cx = tex.width / 2;
            int cy = tex.height / 2;

            // Arrow shaft
            for (int y = cy - 10; y <= cy + 5; y++)
            {
                tex.SetPixel(cx, y, col);
                tex.SetPixel(cx - 1, y, col);
                tex.SetPixel(cx + 1, y, col);
            }

            // Arrow head
            for (int i = 0; i < 8; i++)
            {
                int y = cy - 10 + i;
                for (int x = cx - (8 - i); x <= cx + (8 - i); x++)
                {
                    if (x >= 0 && x < tex.width && y >= 0 && y < tex.height)
                        tex.SetPixel(x, y, col);
                }
            }
        }

        void SetupAudio()
        {
            if (SecretProximityPlugin.Instance.AudioFeedback.Value)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.volume = SecretProximityPlugin.Instance.AudioVolume.Value;
                // In real implementation, load actual audio clip
            }
        }

        void InitializeGUIStyles()
        {
            distanceStyle = new GUIStyle();
            distanceStyle.normal.textColor = Color.white;
            distanceStyle.fontSize = 14;
            distanceStyle.alignment = TextAnchor.MiddleCenter;
            distanceStyle.fontStyle = FontStyle.Bold;
        }

        void Update()
        {
            if (!SecretProximityPlugin.Instance.Enabled.Value)
                return;

            if (heroController == null)
            {
                heroController = HeroController.instance;
                if (heroController == null)
                    return;
            }

            pulseTime += Time.deltaTime;

            // Scan for nearby secrets
            ScanForSecrets();

            // Update closest secret
            UpdateClosestSecret();

            // Handle audio feedback
            if (SecretProximityPlugin.Instance.AudioFeedback.Value && closestSecret != null)
            {
                HandleAudioFeedback();
            }
        }

        void ScanForSecrets()
        {
            nearbySecrets.Clear();

            if (heroController == null)
                return;

            Vector3 playerPos = heroController.transform.position;
            float range = SecretProximityPlugin.Instance.DetectionRange.Value;

            // Find all collectibles in range
            Collider2D[] colliders = Physics2D.OverlapCircleAll(playerPos, range);

            foreach (var collider in colliders)
            {
                SecretInfo secret = IdentifySecret(collider.gameObject);
                if (secret != null)
                {
                    secret.distance = Vector3.Distance(playerPos, collider.transform.position);
                    secret.direction = (collider.transform.position - playerPos).normalized;
                    nearbySecrets.Add(secret);
                }
            }
        }

        SecretInfo IdentifySecret(GameObject obj)
        {
            if (obj == null)
                return null;

            string name = obj.name.ToLower();
            string tag = obj.tag?.ToLower() ?? "";

            // Check for grubs
            if (SecretProximityPlugin.Instance.DetectGrubs.Value &&
                (name.Contains("grub") || tag.Contains("grub")))
            {
                return new SecretInfo
                {
                    gameObject = obj,
                    type = SecretType.Grub,
                    name = "Grub"
                };
            }

            // Check for charms
            if (SecretProximityPlugin.Instance.DetectCharms.Value &&
                (name.Contains("charm") || tag.Contains("charm")))
            {
                return new SecretInfo
                {
                    gameObject = obj,
                    type = SecretType.Charm,
                    name = "Charm"
                };
            }

            // Check for mask shards
            if (SecretProximityPlugin.Instance.DetectMasks.Value &&
                (name.Contains("mask") || name.Contains("shard")))
            {
                return new SecretInfo
                {
                    gameObject = obj,
                    type = SecretType.MaskShard,
                    name = "Mask Shard"
                };
            }

            // Check for vessel fragments
            if (SecretProximityPlugin.Instance.DetectVessels.Value &&
                (name.Contains("vessel") || name.Contains("fragment")))
            {
                return new SecretInfo
                {
                    gameObject = obj,
                    type = SecretType.VesselFragment,
                    name = "Vessel Fragment"
                };
            }

            // Check for keys
            if (SecretProximityPlugin.Instance.DetectKeys.Value &&
                (name.Contains("key") || name.Contains("simple") || name.Contains("elegant")))
            {
                return new SecretInfo
                {
                    gameObject = obj,
                    type = SecretType.Key,
                    name = "Key"
                };
            }

            // Check for essence
            if (SecretProximityPlugin.Instance.DetectEssence.Value &&
                (name.Contains("essence") || name.Contains("dream")))
            {
                return new SecretInfo
                {
                    gameObject = obj,
                    type = SecretType.Essence,
                    name = "Essence"
                };
            }

            // Check for generic collectibles
            if (tag.Contains("collect") || tag.Contains("pickup") ||
                name.Contains("shiny") || name.Contains("geo"))
            {
                return new SecretInfo
                {
                    gameObject = obj,
                    type = SecretType.Other,
                    name = "Collectible"
                };
            }

            return null;
        }

        void UpdateClosestSecret()
        {
            if (nearbySecrets.Count == 0)
            {
                closestSecret = null;
                return;
            }

            closestSecret = nearbySecrets.OrderBy(s => s.distance).FirstOrDefault();
        }

        void HandleAudioFeedback()
        {
            if (closestSecret == null || audioSource == null)
                return;

            // Calculate beep frequency based on distance
            float normalizedDistance = closestSecret.distance / SecretProximityPlugin.Instance.DetectionRange.Value;
            float beepInterval = Mathf.Lerp(0.2f, 2f, normalizedDistance);

            if (Time.time - lastBeepTime >= beepInterval)
            {
                // Play beep sound
                // audioSource.Play();
                lastBeepTime = Time.time;
                SecretProximityPlugin.ModLogger?.LogDebug($"Beep! Secret {closestSecret.name} at {closestSecret.distance:F1}m");
            }
        }

        void OnGUI()
        {
            if (!SecretProximityPlugin.Instance.Enabled.Value || closestSecret == null)
                return;

            // Calculate indicator position
            Vector2 indicatorPos = GetIndicatorPosition();
            float size = SecretProximityPlugin.Instance.IndicatorSize.Value;

            // Draw visual pulse
            if (SecretProximityPlugin.Instance.VisualPulse.Value)
            {
                DrawPulseIndicator(indicatorPos, size);
            }

            // Draw directional arrow
            if (SecretProximityPlugin.Instance.DirectionalIndicator.Value)
            {
                DrawDirectionalArrow(indicatorPos, size);
            }

            // Draw distance text
            if (SecretProximityPlugin.Instance.ShowDistance.Value)
            {
                DrawDistanceText(indicatorPos, size);
            }
        }

        Vector2 GetIndicatorPosition()
        {
            float size = SecretProximityPlugin.Instance.IndicatorSize.Value;
            float padding = 20f;
            var position = SecretProximityPlugin.Instance.IndicatorPosition.Value;

            switch (position)
            {
                case HUDPosition.TopLeft:
                    return new Vector2(padding + size/2, padding + size/2);
                case HUDPosition.TopCenter:
                    return new Vector2(Screen.width / 2f, padding + size/2);
                case HUDPosition.TopRight:
                    return new Vector2(Screen.width - padding - size/2, padding + size/2);
                case HUDPosition.MiddleLeft:
                    return new Vector2(padding + size/2, Screen.height / 2f);
                case HUDPosition.MiddleRight:
                    return new Vector2(Screen.width - padding - size/2, Screen.height / 2f);
                case HUDPosition.BottomLeft:
                    return new Vector2(padding + size/2, Screen.height - padding - size/2);
                case HUDPosition.BottomCenter:
                    return new Vector2(Screen.width / 2f, Screen.height - padding - size/2);
                case HUDPosition.BottomRight:
                    return new Vector2(Screen.width - padding - size/2, Screen.height - padding - size/2);
                default:
                    return new Vector2(Screen.width / 2f, padding + size/2);
            }
        }

        void DrawPulseIndicator(Vector2 position, float size)
        {
            // Calculate pulse effect
            float pulseSpeed = SecretProximityPlugin.Instance.PulseSpeed.Value;
            float normalizedDistance = closestSecret.distance / SecretProximityPlugin.Instance.DetectionRange.Value;
            float pulseRate = Mathf.Lerp(5f, 1f, normalizedDistance) * pulseSpeed;
            float pulse = (Mathf.Sin(pulseTime * pulseRate) + 1f) / 2f;

            // Calculate color with pulse
            Color color = SecretProximityPlugin.Instance.ProximityColor.Value;
            color.a = Mathf.Lerp(0.3f, 1f, pulse);

            // Scale based on proximity
            float scale = Mathf.Lerp(1.2f, 0.8f, normalizedDistance);
            float currentSize = size * scale * (1f + pulse * 0.2f);

            GUI.color = color;
            Rect rect = new Rect(position.x - currentSize/2, position.y - currentSize/2, currentSize, currentSize);
            GUI.DrawTexture(rect, indicatorTexture);
            GUI.color = Color.white;
        }

        void DrawDirectionalArrow(Vector2 position, float size)
        {
            if (closestSecret == null || heroController == null)
                return;

            // Calculate angle to secret
            Vector2 direction = new Vector2(closestSecret.direction.x, -closestSecret.direction.y);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

            // Draw rotated arrow
            Color color = SecretProximityPlugin.Instance.ProximityColor.Value;
            GUI.color = color;

            Matrix4x4 matrixBackup = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, position);

            float arrowSize = size * 0.5f;
            Rect arrowRect = new Rect(position.x - arrowSize/2, position.y - arrowSize/2, arrowSize, arrowSize);
            GUI.DrawTexture(arrowRect, arrowTexture);

            GUI.matrix = matrixBackup;
            GUI.color = Color.white;
        }

        void DrawDistanceText(Vector2 position, float size)
        {
            string distanceText = $"{closestSecret.distance:F1}m";
            string typeText = closestSecret.name;

            Rect textRect = new Rect(position.x - size, position.y + size/2 + 5, size * 2, 20);
            GUI.Label(textRect, distanceText, distanceStyle);

            textRect.y += 20;
            GUI.Label(textRect, typeText, distanceStyle);
        }

        void OnDestroy()
        {
            if (indicatorTexture != null)
                Destroy(indicatorTexture);
            if (arrowTexture != null)
                Destroy(arrowTexture);
        }
    }

    [HarmonyPatch]
    public static class ProximityPatches
    {
        [HarmonyPatch(typeof(HeroController), "Start")]
        [HarmonyPostfix]
        public static void OnHeroStart(HeroController __instance)
        {
            SecretProximityPlugin.ModLogger?.LogInfo("HeroController started - proximity sensor active");
            SecretProximityPlugin.Instance?.CreateSensorObject();
        }

        [HarmonyPatch(typeof(HeroController), "EnterScene")]
        [HarmonyPostfix]
        public static void OnSceneTransition(HeroController __instance)
        {
            SecretProximityPlugin.ModLogger?.LogInfo("Scene transition - resetting sensor");
            // Sensor will automatically rescan in new scene
        }
    }
}