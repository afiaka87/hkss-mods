using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;

namespace HKSS.BreadcrumbTrail
{
    /// <summary>
    /// Optimizes trail point generation by reducing redundant points and adapting sampling rate
    /// </summary>
    public class TrailOptimizer
    {
        private ManualLogSource logger;

        // Optimization parameters (will be configurable)
        private float angleCullThreshold = 5f; // Degrees
        private float minPointDistance = 0.5f; // Units
        private bool useAdaptiveSampling = true;

        // Adaptive sampling state
        private float currentSampleRate = 0.1f;
        private float lastMovementComplexity = 0f;
        private Vector3 lastVelocity = Vector3.zero;
        private bool wasInCombat = false;

        // Point history for angle calculation
        private readonly LinkedList<TrailPoint> recentPoints = new LinkedList<TrailPoint>();
        private const int HISTORY_SIZE = 10;

        // Performance metrics
        private int totalPointsGenerated = 0;
        private int pointsAccepted = 0;
        private int pointsCulledByAngle = 0;
        private int pointsCulledByDistance = 0;

        public TrailOptimizer(ManualLogSource logger = null)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Update optimization parameters from config
        /// </summary>
        public void UpdateConfig(float angleCullThreshold, float minPointDistance, bool useAdaptiveSampling)
        {
            this.angleCullThreshold = angleCullThreshold;
            this.minPointDistance = minPointDistance;
            this.useAdaptiveSampling = useAdaptiveSampling;
        }

        /// <summary>
        /// Determines if a new trail point should be added based on optimization criteria
        /// </summary>
        public bool ShouldAddPoint(Vector3 position, float speed, bool inCombat, float timeSinceLastPoint)
        {
            totalPointsGenerated++;

            // Always add first few points
            if (recentPoints.Count < 2)
            {
                AcceptPoint(position, speed, inCombat);
                return true;
            }

            // Check adaptive sampling rate
            if (useAdaptiveSampling && !CheckAdaptiveSampling(timeSinceLastPoint, speed, inCombat))
            {
                return false;
            }

            // Check minimum distance
            if (!CheckMinimumDistance(position))
            {
                pointsCulledByDistance++;
                return false;
            }

            // Check angle culling (skip if movement is too straight)
            if (!CheckAngleCulling(position, speed, inCombat))
            {
                pointsCulledByAngle++;
                return false;
            }

            // Point accepted
            AcceptPoint(position, speed, inCombat);
            return true;
        }

        /// <summary>
        /// Check if enough time has passed based on adaptive sampling rate
        /// </summary>
        private bool CheckAdaptiveSampling(float timeSinceLastPoint, float speed, bool inCombat)
        {
            UpdateSampleRate(speed, inCombat);
            return timeSinceLastPoint >= currentSampleRate;
        }

        /// <summary>
        /// Update the adaptive sampling rate based on movement complexity
        /// </summary>
        private void UpdateSampleRate(float speed, bool inCombat)
        {
            float complexity = CalculateMovementComplexity(speed, inCombat);

            // Determine sample rate based on complexity
            if (inCombat || complexity > 0.7f)
            {
                // High complexity: sample frequently
                currentSampleRate = 0.05f;
            }
            else if (complexity > 0.3f)
            {
                // Medium complexity: normal sampling
                currentSampleRate = 0.1f;
            }
            else
            {
                // Low complexity: sample infrequently
                currentSampleRate = 0.2f;
            }

            lastMovementComplexity = complexity;
        }

        /// <summary>
        /// Calculate movement complexity score (0-1)
        /// </summary>
        private float CalculateMovementComplexity(float speed, bool inCombat)
        {
            float complexity = 0f;

            // Combat adds complexity
            if (inCombat) complexity += 0.4f;
            if (inCombat != wasInCombat) complexity += 0.2f; // State change

            // Speed changes add complexity
            float speedNormalized = Mathf.Clamp01(speed / 20f);
            complexity += speedNormalized * 0.3f;

            // High speed itself adds some complexity
            if (speed > 15f) complexity += 0.2f;

            // Velocity direction changes add complexity
            if (recentPoints.Count >= 2)
            {
                var lastPoint = recentPoints.Last.Value;
                var secondLastPoint = recentPoints.Last.Previous.Value;

                Vector3 currentVelocity = (lastPoint.position - secondLastPoint.position).normalized;
                float velocityChange = Vector3.Angle(currentVelocity, lastVelocity);
                complexity += (velocityChange / 180f) * 0.3f;

                lastVelocity = currentVelocity;
            }

            wasInCombat = inCombat;
            return Mathf.Clamp01(complexity);
        }

        /// <summary>
        /// Check if the new point is far enough from the last point
        /// </summary>
        private bool CheckMinimumDistance(Vector3 position)
        {
            if (recentPoints.Count == 0) return true;

            var lastPoint = recentPoints.Last.Value;
            float distance = Vector3.Distance(position, lastPoint.position);

            return distance >= minPointDistance;
        }

        /// <summary>
        /// Check if the angle between last three points warrants a new point
        /// </summary>
        private bool CheckAngleCulling(Vector3 position, float speed, bool inCombat)
        {
            if (recentPoints.Count < 2) return true;

            // Always add points during combat or state changes
            if (inCombat != wasInCombat) return true;

            // Get last two points
            var lastPoint = recentPoints.Last.Value;
            var secondLastPoint = recentPoints.Last.Previous.Value;

            // Calculate vectors
            Vector3 v1 = (lastPoint.position - secondLastPoint.position).normalized;
            Vector3 v2 = (position - lastPoint.position).normalized;

            // Calculate angle
            float angle = Vector3.Angle(v1, v2);

            // Allow point if angle exceeds threshold (i.e., direction change)
            if (angle > angleCullThreshold)
            {
                return true;
            }

            // For nearly straight lines, only add if we've traveled far enough
            float distanceFromSecondLast = Vector3.Distance(position, secondLastPoint.position);
            if (distanceFromSecondLast > minPointDistance * 3f)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Accept a point and update history
        /// </summary>
        private void AcceptPoint(Vector3 position, float speed, bool inCombat)
        {
            pointsAccepted++;

            var point = new TrailPoint
            {
                position = position,
                timestamp = Time.time,
                speed = speed,
                inCombat = inCombat,
                color = Color.white // Will be set by caller
            };

            recentPoints.AddLast(point);

            // Maintain history size
            while (recentPoints.Count > HISTORY_SIZE)
            {
                recentPoints.RemoveFirst();
            }
        }

        /// <summary>
        /// Get the current adaptive sample rate
        /// </summary>
        public float GetCurrentSampleRate()
        {
            return currentSampleRate;
        }

        /// <summary>
        /// Get the current movement complexity score
        /// </summary>
        public float GetMovementComplexity()
        {
            return lastMovementComplexity;
        }

        /// <summary>
        /// Get optimization statistics
        /// </summary>
        public OptimizationStats GetStats()
        {
            return new OptimizationStats
            {
                TotalPointsGenerated = totalPointsGenerated,
                PointsAccepted = pointsAccepted,
                PointsCulledByAngle = pointsCulledByAngle,
                PointsCulledByDistance = pointsCulledByDistance,
                ReductionPercentage = totalPointsGenerated > 0
                    ? (1f - (float)pointsAccepted / totalPointsGenerated) * 100f
                    : 0f,
                CurrentSampleRate = currentSampleRate,
                MovementComplexity = lastMovementComplexity
            };
        }

        /// <summary>
        /// Reset optimizer state (e.g., on scene change)
        /// </summary>
        public void Reset()
        {
            recentPoints.Clear();
            lastVelocity = Vector3.zero;
            wasInCombat = false;
            currentSampleRate = 0.1f;
            lastMovementComplexity = 0f;
        }

        /// <summary>
        /// Log current optimization statistics
        /// </summary>
        public void LogStats()
        {
            var stats = GetStats();
            logger?.LogInfo($"[TrailOptimizer] Stats: Generated={stats.TotalPointsGenerated}, " +
                          $"Accepted={stats.PointsAccepted} ({100f - stats.ReductionPercentage:F1}%), " +
                          $"CulledByAngle={stats.PointsCulledByAngle}, " +
                          $"CulledByDistance={stats.PointsCulledByDistance}, " +
                          $"Reduction={stats.ReductionPercentage:F1}%, " +
                          $"SampleRate={stats.CurrentSampleRate:F3}s");
        }
    }

    /// <summary>
    /// Statistics about optimization performance
    /// </summary>
    public struct OptimizationStats
    {
        public int TotalPointsGenerated;
        public int PointsAccepted;
        public int PointsCulledByAngle;
        public int PointsCulledByDistance;
        public float ReductionPercentage;
        public float CurrentSampleRate;
        public float MovementComplexity;
    }
}