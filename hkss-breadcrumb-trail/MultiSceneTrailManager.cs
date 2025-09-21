using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using BepInEx.Logging;

namespace HKSS.BreadcrumbTrail
{
    // Manages trails across multiple scenes with persistence
    public class MultiSceneTrailManager : MonoBehaviour
    {
        private static MultiSceneTrailManager instance;
        public static MultiSceneTrailManager Instance => instance;

        // Scene-specific trail data
        private Dictionary<string, SceneTrailData> sceneTrails = new Dictionary<string, SceneTrailData>();
        private string currentSceneName = "";
        private SceneTrailData currentSceneTrail = null;

        // Trail rendering
        private Dictionary<string, GameObject> sceneTrailObjects = new Dictionary<string, GameObject>();
        private const int MAX_SCENES_LOADED = 20; // Allow many more scenes to be rendered simultaneously

        // Persistence
        private string saveFilePath;
        private float lastSaveTime = 0f;
        private const float AUTO_SAVE_INTERVAL = 5f; // Save every 5 seconds

        // Configuration
        private bool enablePersistence = true;
        private int maxPointsPerScene => BreadcrumbPlugin.Instance?.MaxPoints?.Value ?? 1000;

        public class SceneTrailData
        {
            public string sceneName;
            public List<TrailPoint> points = new List<TrailPoint>();
            public DateTime firstVisit;
            public DateTime lastVisit;
            public Bounds sceneBounds;
            public Vector3 sceneOffset; // For coordinate transformation
            public bool isActive;

            public SceneTrailData(string name)
            {
                sceneName = name;
                firstVisit = DateTime.Now;
                lastVisit = DateTime.Now;
                points = new List<TrailPoint>();
            }
        }

        void Awake()
        {
            try
            {
                BreadcrumbPlugin.ModLogger?.LogInfo("[MultiSceneTrailManager] Awake starting...");

                if (instance != null)
                {
                    BreadcrumbPlugin.ModLogger?.LogInfo("[MultiSceneTrailManager] Instance already exists, destroying duplicate");
                    Destroy(this);
                    return;
                }

                instance = this;
                DontDestroyOnLoad(gameObject);
                BreadcrumbPlugin.ModLogger?.LogInfo($"[MultiSceneTrailManager] Singleton instance set: {instance != null}");

                // Set up save path
                try
                {
                    string modDir = Path.GetDirectoryName(typeof(BreadcrumbPlugin).Assembly.Location);
                    string saveDir = Path.Combine(modDir, "TrailSaves");
                    Directory.CreateDirectory(saveDir);
                    saveFilePath = Path.Combine(saveDir, "trail_data.json");
                    BreadcrumbPlugin.ModLogger?.LogInfo($"[MultiSceneTrailManager] Save path configured: {saveFilePath}");
                }
                catch (Exception pathEx)
                {
                    BreadcrumbPlugin.ModLogger?.LogError($"[MultiSceneTrailManager] Failed to set up save path: {pathEx.Message}");
                }

                // Hook scene events
                SceneManager.sceneLoaded += OnSceneLoaded;
                SceneManager.sceneUnloaded += OnSceneUnloaded;
                SceneManager.activeSceneChanged += OnActiveSceneChanged;
                BreadcrumbPlugin.ModLogger?.LogInfo("[MultiSceneTrailManager] Scene event hooks registered");

                // Load existing trail data
                LoadTrailData();

                BreadcrumbPlugin.ModLogger?.LogInfo($"[MultiSceneTrailManager] ✓ Initialized successfully");
                BreadcrumbPlugin.ModLogger?.LogInfo($"  - Scene trails count: {sceneTrails.Count}");
                BreadcrumbPlugin.ModLogger?.LogInfo($"  - Trail objects count: {sceneTrailObjects.Count}");
            }
            catch (Exception ex)
            {
                BreadcrumbPlugin.ModLogger?.LogError($"[MultiSceneTrailManager] ✗ Awake failed: {ex.Message}");
                BreadcrumbPlugin.ModLogger?.LogError($"  Stack: {ex.StackTrace}");
            }
        }

        void OnDestroy()
        {
            SaveTrailData();

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;

            // Clean up all trail objects
            foreach (var kvp in sceneTrailObjects)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value);
            }
        }

        void Update()
        {
            // Auto-save periodically
            if (Time.time - lastSaveTime > AUTO_SAVE_INTERVAL)
            {
                SaveTrailData();
                lastSaveTime = Time.time;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (string.IsNullOrEmpty(scene.name) || scene.name.StartsWith("Pre_Menu") || scene.name.Contains("Menu"))
                return;

            BreadcrumbPlugin.ModLogger?.LogInfo($"[MultiSceneTrailManager] Scene loaded: {scene.name}");

            // Create or retrieve scene trail data
            if (!sceneTrails.ContainsKey(scene.name))
            {
                sceneTrails[scene.name] = new SceneTrailData(scene.name);
            }

            var sceneData = sceneTrails[scene.name];
            sceneData.lastVisit = DateTime.Now;
            sceneData.isActive = true;

            // Calculate scene bounds
            CalculateSceneBounds(scene, sceneData);

            // Create trail renderer for this scene
            CreateSceneTrailRenderer(scene.name, sceneData);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (sceneTrails.ContainsKey(scene.name))
            {
                sceneTrails[scene.name].isActive = false;

                // Hide but don't destroy the trail renderer
                if (sceneTrailObjects.ContainsKey(scene.name))
                {
                    sceneTrailObjects[scene.name].SetActive(false);
                }
            }

            // Save on scene unload (in case of crash)
            SaveTrailData();
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene newScene)
        {
            currentSceneName = newScene.name;

            if (sceneTrails.ContainsKey(currentSceneName))
            {
                currentSceneTrail = sceneTrails[currentSceneName];
            }
            else
            {
                currentSceneTrail = null;
            }

            BreadcrumbPlugin.ModLogger?.LogInfo($"[MultiSceneTrailManager] Active scene: {currentSceneName}");

            // Show current scene trail, hide others if too many
            ManageVisibleTrails();
        }

        private void CalculateSceneBounds(Scene scene, SceneTrailData sceneData)
        {
            Bounds bounds = new Bounds();
            bool boundsInitialized = false;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    if (!boundsInitialized)
                    {
                        bounds = renderer.bounds;
                        boundsInitialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }
            }

            sceneData.sceneBounds = bounds;
            sceneData.sceneOffset = bounds.center;

            BreadcrumbPlugin.ModLogger?.LogInfo($"  Scene bounds: center={bounds.center}, size={bounds.size}");
        }

        private void CreateSceneTrailRenderer(string sceneName, SceneTrailData sceneData)
        {
            if (sceneTrailObjects.ContainsKey(sceneName))
            {
                // Reactivate existing trail
                sceneTrailObjects[sceneName].SetActive(true);
                UpdateSceneTrailRenderer(sceneName, sceneData);
                return;
            }

            // Create new trail GameObject
            GameObject trailObj = new GameObject($"Trail_{sceneName}");
            DontDestroyOnLoad(trailObj);

            // Add LineRenderer
            LineRenderer lineRenderer = trailObj.AddComponent<LineRenderer>();
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startWidth = BreadcrumbPlugin.Instance.TrailWidth.Value;
            lineRenderer.endWidth = BreadcrumbPlugin.Instance.TrailWidth.Value * 0.5f;
            lineRenderer.numCapVertices = 5;
            lineRenderer.numCornerVertices = 5;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.textureMode = LineTextureMode.Stretch;
            lineRenderer.useWorldSpace = true; // Important for multi-scene

            // Add trail component
            var trailComponent = trailObj.AddComponent<SceneTrailRenderer>();
            trailComponent.Initialize(sceneName, sceneData, lineRenderer);

            sceneTrailObjects[sceneName] = trailObj;

            // Update with existing points
            UpdateSceneTrailRenderer(sceneName, sceneData);
        }

        private void UpdateSceneTrailRenderer(string sceneName, SceneTrailData sceneData)
        {
            if (!sceneTrailObjects.ContainsKey(sceneName))
                return;

            var trailObj = sceneTrailObjects[sceneName];
            var renderer = trailObj.GetComponent<SceneTrailRenderer>();
            renderer?.UpdateTrail(sceneData);
        }

        private void ManageVisibleTrails()
        {
            // Don't show any trails if visibility is toggled off
            if (!BreadcrumbPlugin.Instance.TrailVisible)
            {
                foreach (var kvp in sceneTrailObjects)
                {
                    kvp.Value.SetActive(false);
                }
                return;
            }

            // Show current scene and nearby scenes
            var activeScenes = new HashSet<string> { currentSceneName };

            // Add recently visited scenes (keep trails visible for much longer)
            var recentScenes = sceneTrails
                .Where(kvp => kvp.Value.isActive && (DateTime.Now - kvp.Value.lastVisit).TotalSeconds < 3600) // 1 hour
                .OrderByDescending(kvp => kvp.Value.lastVisit)
                .Take(MAX_SCENES_LOADED - 1)
                .Select(kvp => kvp.Key);

            foreach (var scene in recentScenes)
                activeScenes.Add(scene);

            // Show/hide trail objects
            foreach (var kvp in sceneTrailObjects)
            {
                kvp.Value.SetActive(activeScenes.Contains(kvp.Key));
            }
        }

        public void SetTrailsVisible(bool visible)
        {
            BreadcrumbPlugin.ModLogger?.LogInfo($"[MultiSceneTrailManager] Setting trail visibility to: {visible}");

            // Update visibility of all trail objects
            foreach (var kvp in sceneTrailObjects)
            {
                if (visible)
                {
                    // Use ManageVisibleTrails logic to determine which should be shown
                    ManageVisibleTrails();
                    break; // Only need to call once
                }
                else
                {
                    kvp.Value.SetActive(false);
                }
            }
        }

        public void AddTrailPoint(Vector3 position, float speed, bool inCombat)
        {
            if (currentSceneTrail == null || string.IsNullOrEmpty(currentSceneName))
                return;

            var point = new TrailPoint
            {
                position = position,
                timestamp = Time.time,
                speed = speed,
                inCombat = inCombat,
                color = CalculatePointColor(position, speed, inCombat)
            };

            currentSceneTrail.points.Add(point);

            // Limit points per scene
            if (currentSceneTrail.points.Count > maxPointsPerScene)
            {
                currentSceneTrail.points.RemoveAt(0);
            }

            // Update renderer
            UpdateSceneTrailRenderer(currentSceneName, currentSceneTrail);
        }

        private Color CalculatePointColor(Vector3 position, float speed, bool inCombat)
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

                default:
                    return BreadcrumbPlugin.Instance.BaseColor.Value;
            }
        }

        private void SaveTrailData()
        {
            if (!enablePersistence)
                return;

            try
            {
                var saveData = new TrailSaveData
                {
                    version = 1,
                    savedAt = DateTime.Now,
                    scenes = sceneTrails.Values.Select(s => new SceneTrailSaveData
                    {
                        sceneName = s.sceneName,
                        points = s.points.Select(p => new TrailPointSaveData
                        {
                            x = p.position.x,
                            y = p.position.y,
                            z = p.position.z,
                            timestamp = p.timestamp,
                            speed = p.speed,
                            inCombat = p.inCombat
                        }).ToList(),
                        firstVisit = s.firstVisit,
                        lastVisit = s.lastVisit
                    }).ToList()
                };

                string json = JsonUtility.ToJson(saveData, true);
                File.WriteAllText(saveFilePath, json);

                BreadcrumbPlugin.ModLogger?.LogInfo($"[MultiSceneTrailManager] Saved {sceneTrails.Count} scenes");
            }
            catch (Exception e)
            {
                BreadcrumbPlugin.ModLogger?.LogError($"[MultiSceneTrailManager] Save failed: {e.Message}");
            }
        }

        private void LoadTrailData()
        {
            if (!File.Exists(saveFilePath))
                return;

            try
            {
                string json = File.ReadAllText(saveFilePath);
                var saveData = JsonUtility.FromJson<TrailSaveData>(json);

                foreach (var sceneData in saveData.scenes)
                {
                    var trail = new SceneTrailData(sceneData.sceneName)
                    {
                        firstVisit = sceneData.firstVisit,
                        lastVisit = sceneData.lastVisit
                    };

                    foreach (var pointData in sceneData.points)
                    {
                        trail.points.Add(new TrailPoint
                        {
                            position = new Vector3(pointData.x, pointData.y, pointData.z),
                            timestamp = pointData.timestamp,
                            speed = pointData.speed,
                            inCombat = pointData.inCombat,
                            color = CalculatePointColor(
                                new Vector3(pointData.x, pointData.y, pointData.z),
                                pointData.speed,
                                pointData.inCombat
                            )
                        });
                    }

                    sceneTrails[sceneData.sceneName] = trail;
                }

                BreadcrumbPlugin.ModLogger?.LogInfo($"[MultiSceneTrailManager] Loaded {sceneTrails.Count} scenes");
            }
            catch (Exception e)
            {
                BreadcrumbPlugin.ModLogger?.LogError($"[MultiSceneTrailManager] Load failed: {e.Message}");
            }
        }
    }

    // Component to render trail for a specific scene
    public class SceneTrailRenderer : MonoBehaviour
    {
        private string sceneName;
        private MultiSceneTrailManager.SceneTrailData sceneData;
        private LineRenderer lineRenderer;

        public void Initialize(string name, MultiSceneTrailManager.SceneTrailData data, LineRenderer renderer)
        {
            sceneName = name;
            sceneData = data;
            lineRenderer = renderer;
        }

        public void UpdateTrail(MultiSceneTrailManager.SceneTrailData data)
        {
            sceneData = data;

            if (data.points.Count < 2)
            {
                lineRenderer.positionCount = 0;
                return;
            }

            // Remove old points based on time
            float currentTime = Time.time;
            float duration = BreadcrumbPlugin.Instance.TrailDuration.Value;
            data.points.RemoveAll(p => currentTime - p.timestamp > duration);

            if (data.points.Count < 2)
            {
                lineRenderer.positionCount = 0;
                return;
            }

            // Update line renderer
            lineRenderer.positionCount = data.points.Count;

            for (int i = 0; i < data.points.Count; i++)
            {
                lineRenderer.SetPosition(i, data.points[i].position);
            }

            // Update gradient
            UpdateGradient(data.points);
        }

        private void UpdateGradient(List<TrailPoint> points)
        {
            Gradient gradient = new Gradient();
            int keyCount = Mathf.Min(points.Count, 8);
            GradientColorKey[] colorKeys = new GradientColorKey[keyCount];
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[keyCount];

            for (int i = 0; i < keyCount; i++)
            {
                int pointIndex = (i * (points.Count - 1)) / (keyCount - 1);
                float gradientTime = (float)i / (keyCount - 1);

                TrailPoint point = points[pointIndex];
                float age = Time.time - point.timestamp;
                float normalizedAge = age / BreadcrumbPlugin.Instance.TrailDuration.Value;
                float alpha = 1f - normalizedAge;

                colorKeys[i] = new GradientColorKey(point.color, gradientTime);
                alphaKeys[i] = new GradientAlphaKey(alpha, gradientTime);
            }

            gradient.SetKeys(colorKeys, alphaKeys);
            lineRenderer.colorGradient = gradient;
        }
    }

    // Serialization classes
    [Serializable]
    public class TrailSaveData
    {
        public int version;
        public DateTime savedAt;
        public List<SceneTrailSaveData> scenes;
    }

    [Serializable]
    public class SceneTrailSaveData
    {
        public string sceneName;
        public List<TrailPointSaveData> points;
        public DateTime firstVisit;
        public DateTime lastVisit;
    }

    [Serializable]
    public class TrailPointSaveData
    {
        public float x, y, z;
        public float timestamp;
        public float speed;
        public bool inCombat;
    }
}