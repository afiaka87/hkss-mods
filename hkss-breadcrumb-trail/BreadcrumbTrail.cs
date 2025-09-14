using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace HKSS.BreadcrumbTrail
{
    public class TrailPoint
    {
        public Vector3 position;
        public float timestamp;
        public float speed;
        public bool inCombat;
        public Color color;
    }

    public class BreadcrumbTrail : MonoBehaviour
    {
        private List<TrailPoint> trail = new List<TrailPoint>();
        private LineRenderer lineRenderer;
        private float lastDropTime = 0f;
        private HeroController heroController;
        private Rigidbody2D heroRigidbody;
        private Material trailMaterial;
        private Gradient colorGradient;

        void Start()
        {
            BreadcrumbPlugin.ModLogger?.LogInfo("BreadcrumbTrail started");
            BreadcrumbPlugin.Instance.CreateTrailObject();
            InitializeLineRenderer();
            CreateColorGradient();
        }

        void InitializeLineRenderer()
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();

            // Create a simple unlit material for the trail
            trailMaterial = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.material = trailMaterial;

            lineRenderer.startWidth = BreadcrumbPlugin.Instance.TrailWidth.Value;
            lineRenderer.endWidth = BreadcrumbPlugin.Instance.TrailWidth.Value * 0.5f;

            // Set line renderer settings
            lineRenderer.numCapVertices = 5;
            lineRenderer.numCornerVertices = 5;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.textureMode = LineTextureMode.Stretch;
        }

        void CreateColorGradient()
        {
            colorGradient = new Gradient();
            GradientColorKey[] colorKeys = new GradientColorKey[2];
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[3];

            colorKeys[0] = new GradientColorKey(BreadcrumbPlugin.Instance.BaseColor.Value, 0f);
            colorKeys[1] = new GradientColorKey(BreadcrumbPlugin.Instance.SpeedColor.Value, 1f);

            alphaKeys[0] = new GradientAlphaKey(1f, 0f);
            alphaKeys[1] = new GradientAlphaKey(0.5f, 0.5f);
            alphaKeys[2] = new GradientAlphaKey(0f, 1f);

            colorGradient.SetKeys(colorKeys, alphaKeys);
            lineRenderer.colorGradient = colorGradient;
        }

        void Update()
        {
            if (!BreadcrumbPlugin.Instance.Enabled.Value)
                return;

            if (heroController == null)
            {
                heroController = HeroController.instance;
                if (heroController != null)
                {
                    heroRigidbody = heroController.GetComponent<Rigidbody2D>();
                }
                if (heroController == null)
                    return;
            }

            // Check if we should drop a new point
            if (Time.time - lastDropTime >= BreadcrumbPlugin.Instance.DropFrequency.Value)
            {
                DropTrailPoint();
                lastDropTime = Time.time;
            }

            // Clean up old points
            RemoveOldPoints();

            // Update line renderer
            UpdateLineRenderer();
        }

        void DropTrailPoint()
        {
            if (heroController == null)
                return;

            // Check combat state
            bool inCombat = IsInCombat();
            if (!BreadcrumbPlugin.Instance.ShowInCombat.Value && inCombat)
                return;

            Vector3 position = heroController.transform.position;
            float speed = heroRigidbody != null ? heroRigidbody.linearVelocity.magnitude : 0f;

            TrailPoint newPoint = new TrailPoint
            {
                position = position,
                timestamp = Time.time,
                speed = speed,
                inCombat = inCombat,
                color = CalculatePointColor(position, speed, inCombat)
            };

            trail.Add(newPoint);

            // Limit max points for performance
            if (trail.Count > BreadcrumbPlugin.Instance.MaxPoints.Value)
            {
                trail.RemoveAt(0);
            }
        }

        void RemoveOldPoints()
        {
            float currentTime = Time.time;
            float duration = BreadcrumbPlugin.Instance.TrailDuration.Value;

            trail.RemoveAll(p => currentTime - p.timestamp > duration);
        }

        void UpdateLineRenderer()
        {
            if (trail.Count < 2)
            {
                lineRenderer.positionCount = 0;
                return;
            }

            lineRenderer.positionCount = trail.Count;

            // Update positions and colors
            Gradient gradient = new Gradient();
            GradientColorKey[] colorKeys = new GradientColorKey[Mathf.Min(trail.Count, 8)];
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[Mathf.Min(trail.Count, 8)];

            for (int i = 0; i < trail.Count; i++)
            {
                TrailPoint point = trail[i];
                lineRenderer.SetPosition(i, point.position);

                // Calculate alpha based on age and fade style
                float age = Time.time - point.timestamp;
                float normalizedAge = age / BreadcrumbPlugin.Instance.TrailDuration.Value;
                float alpha = CalculateFade(normalizedAge);

                // Set gradient keys at intervals
                int keyIndex = (i * (colorKeys.Length - 1)) / (trail.Count - 1);
                if (keyIndex < colorKeys.Length)
                {
                    float gradientTime = (float)i / (trail.Count - 1);
                    colorKeys[keyIndex] = new GradientColorKey(point.color, gradientTime);
                    alphaKeys[keyIndex] = new GradientAlphaKey(alpha, gradientTime);
                }
            }

            gradient.SetKeys(colorKeys, alphaKeys);
            lineRenderer.colorGradient = gradient;
        }

        Color CalculatePointColor(Vector3 position, float speed, bool inCombat)
        {
            var colorMode = BreadcrumbPlugin.Instance.TrailColorMode.Value;

            switch (colorMode)
            {
                case ColorMode.Speed:
                    float normalizedSpeed = Mathf.Clamp01(speed / 20f);
                    return Color.Lerp(BreadcrumbPlugin.Instance.BaseColor.Value,
                                     BreadcrumbPlugin.Instance.SpeedColor.Value,
                                     normalizedSpeed);

                case ColorMode.State:
                    return inCombat ? BreadcrumbPlugin.Instance.CombatColor.Value
                                   : BreadcrumbPlugin.Instance.BaseColor.Value;

                case ColorMode.Height:
                    float height = position.y;
                    float normalizedHeight = Mathf.Clamp01((height + 10f) / 20f);
                    return Color.Lerp(BreadcrumbPlugin.Instance.BaseColor.Value,
                                     BreadcrumbPlugin.Instance.SpeedColor.Value,
                                     normalizedHeight);

                case ColorMode.Static:
                default:
                    return BreadcrumbPlugin.Instance.BaseColor.Value;
            }
        }

        float CalculateFade(float normalizedAge)
        {
            var fadeStyle = BreadcrumbPlugin.Instance.TrailFadeStyle.Value;

            switch (fadeStyle)
            {
                case FadeStyle.Exponential:
                    return Mathf.Pow(1f - normalizedAge, 2f);

                case FadeStyle.Stepped:
                    if (normalizedAge < 0.33f) return 1f;
                    if (normalizedAge < 0.66f) return 0.66f;
                    if (normalizedAge < 0.9f) return 0.33f;
                    return 0.1f;

                case FadeStyle.Linear:
                default:
                    return 1f - normalizedAge;
            }
        }

        bool IsInCombat()
        {
            // Check if there are enemies nearby
            Collider2D[] nearbyEnemies = Physics2D.OverlapCircleAll(
                heroController.transform.position,
                10f,
                LayerMask.GetMask("Enemies")
            );

            return nearbyEnemies.Length > 0;
        }

        void OnDestroy()
        {
            BreadcrumbPlugin.Instance?.DestroyTrailObject();
        }
    }

    [HarmonyPatch]
    public static class TrailPatches
    {
        [HarmonyPatch(typeof(HeroController), "Start")]
        [HarmonyPostfix]
        public static void OnHeroStart(HeroController __instance)
        {
            BreadcrumbPlugin.ModLogger?.LogInfo("HeroController started - creating trail object");
            BreadcrumbPlugin.Instance?.CreateTrailObject();
        }

        [HarmonyPatch(typeof(HeroController), "EnterScene")]
        [HarmonyPostfix]
        public static void OnSceneTransition(HeroController __instance)
        {
            BreadcrumbPlugin.ModLogger?.LogInfo("Scene transition - clearing trail");
            // The trail will naturally clear due to the time-based removal
        }
    }
}