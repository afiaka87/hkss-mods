using UnityEngine;
using System.Collections.Generic;

namespace HKSS.VelocityVector
{
    public class VelocityDisplay : MonoBehaviour
    {
        private static VelocityDisplay instance;

        private Vector2 currentVelocity;
        private float currentSpeed;
        private float currentAngle;
        private float peakSpeed;

        private GUIStyle speedStyle;
        private GUIStyle peakStyle;
        private Texture2D arrowTexture;
        private bool stylesInitialized = false;

        private const float SMOOTHING = 0.1f;
        private Vector2 smoothedVelocity;

        void Awake()
        {
            instance = this;
            CreateArrowTexture();
        }

        void OnDestroy()
        {
            if (arrowTexture != null)
            {
                Destroy(arrowTexture);
            }
        }

        public static void UpdateVelocity(Vector2 velocity)
        {
            if (instance != null)
            {
                instance.SetVelocity(velocity);
            }
        }

        private void SetVelocity(Vector2 velocity)
        {
            smoothedVelocity = Vector2.Lerp(smoothedVelocity, velocity, SMOOTHING);
            currentVelocity = smoothedVelocity;
            currentSpeed = currentVelocity.magnitude;

            if (currentSpeed > 0.01f)
            {
                // Invert X component for correct left-right direction
                currentAngle = Mathf.Atan2(currentVelocity.y, -currentVelocity.x) * Mathf.Rad2Deg;
            }

            if (currentSpeed > peakSpeed)
            {
                peakSpeed = currentSpeed;
            }
        }

        void OnGUI()
        {
            if (!VelocityVectorPlugin.Instance.Enabled.Value)
                return;

            if (!stylesInitialized)
            {
                InitializeStyles();
            }

            Vector2 basePosition = GetBasePosition();

            if (VelocityVectorPlugin.Instance.ShowNumeric.Value)
            {
                DrawNumericDisplay(basePosition);
            }

            if (VelocityVectorPlugin.Instance.ShowVector.Value && currentSpeed > 0.01f)
            {
                DrawVectorArrow(basePosition);
            }

            if (VelocityVectorPlugin.Instance.ShowPeakSpeed.Value)
            {
                DrawPeakSpeed(basePosition);
            }
        }

        private void DrawNumericDisplay(Vector2 position)
        {
            string speedText = FormatSpeed(currentSpeed);

            Rect labelRect = new Rect(position.x, position.y, 200, 30);
            GUI.Label(labelRect, speedText, speedStyle);
        }

        private void DrawVectorArrow(Vector2 position)
        {
            if (arrowTexture == null)
                return;

            float scale = VelocityVectorPlugin.Instance.ArrowScale.Value;
            float size = 40 * scale;

            Vector2 arrowPos = position + new Vector2(0, 40);

            Matrix4x4 matrixBackup = GUI.matrix;

            Vector2 pivotPoint = arrowPos + new Vector2(size / 2, size / 2);
            GUIUtility.RotateAroundPivot(currentAngle - 90, pivotPoint);

            Color originalColor = GUI.color;
            GUI.color = VelocityVectorPlugin.Instance.ArrowColor.Value;

            GUI.DrawTexture(new Rect(arrowPos.x, arrowPos.y, size, size), arrowTexture);

            GUI.color = originalColor;
            GUI.matrix = matrixBackup;
        }

        private void DrawPeakSpeed(Vector2 position)
        {
            string peakText = $"Peak: {FormatSpeed(peakSpeed)}";
            Rect peakRect = new Rect(position.x, position.y + 25, 200, 30);
            GUI.Label(peakRect, peakText, peakStyle);
        }

        private string FormatSpeed(float speed)
        {
            var units = VelocityVectorPlugin.Instance.Units.Value;

            switch (units)
            {
                case DisplayUnits.MetersPerSecond:
                    return $"{(speed * 0.1f):F1} m/s";
                case DisplayUnits.PixelsPerFrame:
                    return $"{(speed / 60f):F1} px/f";
                case DisplayUnits.UnitsPerSecond:
                default:
                    return $"{speed:F1} u/s";
            }
        }

        private Vector2 GetBasePosition()
        {
            var position = VelocityVectorPlugin.Instance.Position.Value;
            float margin = 20;

            float x = margin;
            float y = margin;

            switch (position)
            {
                case DisplayPosition.TopCenter:
                case DisplayPosition.MiddleCenter:
                case DisplayPosition.BottomCenter:
                    x = Screen.width / 2 - 100;
                    break;
                case DisplayPosition.TopRight:
                case DisplayPosition.MiddleRight:
                case DisplayPosition.BottomRight:
                    x = Screen.width - 220;
                    break;
            }

            switch (position)
            {
                case DisplayPosition.MiddleLeft:
                case DisplayPosition.MiddleCenter:
                case DisplayPosition.MiddleRight:
                    y = Screen.height / 2 - 50;
                    break;
                case DisplayPosition.BottomLeft:
                case DisplayPosition.BottomCenter:
                case DisplayPosition.BottomRight:
                    y = Screen.height - 100;
                    break;
            }

            return new Vector2(x, y);
        }

        private void InitializeStyles()
        {
            int fontSize = VelocityVectorPlugin.Instance.FontSize.Value;
            Color textColor = VelocityVectorPlugin.Instance.TextColor.Value;

            speedStyle = new GUIStyle();
            speedStyle.fontSize = fontSize;
            speedStyle.fontStyle = FontStyle.Bold;
            speedStyle.normal.textColor = textColor;

            peakStyle = new GUIStyle();
            peakStyle.fontSize = fontSize - 4;
            peakStyle.fontStyle = FontStyle.Normal;
            peakStyle.normal.textColor = new Color(textColor.r * 0.8f, textColor.g * 0.8f, textColor.b * 0.8f, textColor.a);

            stylesInitialized = true;
        }

        private void CreateArrowTexture()
        {
            int size = 32;
            arrowTexture = new Texture2D(size, size, TextureFormat.ARGB32, false);

            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.clear;
            }

            int centerX = size / 2;

            for (int y = 4; y < size - 4; y++)
            {
                int width = Mathf.Min((size - 8 - y) / 2, 6);
                for (int x = centerX - width; x <= centerX + width; x++)
                {
                    pixels[y * size + x] = Color.white;
                }
            }

            for (int y = size - 12; y < size - 4; y++)
            {
                int width = (size - 4 - y);
                for (int x = centerX - width; x <= centerX + width; x++)
                {
                    pixels[y * size + x] = Color.white;
                }
            }

            arrowTexture.SetPixels(pixels);
            arrowTexture.Apply();
        }
    }
}