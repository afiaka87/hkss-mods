using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace HKSS.AirTime
{
    public class AirTimeDisplay : MonoBehaviour
    {
        private static AirTimeDisplay instance;

        private float currentAirTime;
        private float sessionTotalAirTime;
        private List<float> jumpHistory = new List<float>();

        private GUIStyle currentStyle;
        private GUIStyle totalStyle;
        private GUIStyle historyLabelStyle;
        private Texture2D barTexture;
        private bool stylesInitialized = false;

        void Awake()
        {
            instance = this;
            CreateBarTexture();
        }

        void OnDestroy()
        {
            if (barTexture != null)
            {
                Destroy(barTexture);
            }
        }

        public static void UpdateAirTime(float current, float sessionTotal)
        {
            if (instance != null)
            {
                instance.SetAirTime(current, sessionTotal);
            }
        }

        public static void RecordJump(float duration)
        {
            if (instance != null && duration > 0.1f)
            {
                instance.AddJumpToHistory(duration);
            }
        }

        private void SetAirTime(float current, float sessionTotal)
        {
            currentAirTime = current;
            sessionTotalAirTime = sessionTotal;
        }

        private void AddJumpToHistory(float duration)
        {
            jumpHistory.Add(duration);

            int maxHistory = AirTimePlugin.Instance.HistorySize.Value;
            if (jumpHistory.Count > maxHistory)
            {
                jumpHistory.RemoveAt(0);
            }

            AirTimePlugin.ModLogger?.LogInfo($"Jump recorded: {duration:F2}s");
        }

        void OnGUI()
        {
            if (!AirTimePlugin.Instance.Enabled.Value)
                return;

            if (!stylesInitialized)
            {
                InitializeStyles();
            }

            Vector2 basePosition = GetBasePosition();
            float yOffset = 0;

            if (AirTimePlugin.Instance.ShowCurrentJump.Value)
            {
                DrawCurrentJump(basePosition + new Vector2(0, yOffset));
                yOffset += 25;
            }

            if (AirTimePlugin.Instance.ShowSessionTotal.Value)
            {
                DrawSessionTotal(basePosition + new Vector2(0, yOffset));
                yOffset += 25;
            }

            if (AirTimePlugin.Instance.ShowJumpHistory.Value && jumpHistory.Count > 0)
            {
                DrawJumpHistory(basePosition + new Vector2(0, yOffset + 10));
            }
        }

        private void DrawCurrentJump(Vector2 position)
        {
            string format = GetTimeFormat();
            string text = currentAirTime > 0.01f
                ? string.Format("Air: " + format + "s", currentAirTime)
                : "Grounded";

            Rect labelRect = new Rect(position.x, position.y, 200, 30);
            GUI.Label(labelRect, text, currentStyle);
        }

        private void DrawSessionTotal(Vector2 position)
        {
            string format = GetTimeFormat();
            string text = string.Format("Total: " + format + "s", sessionTotalAirTime);

            Rect labelRect = new Rect(position.x, position.y, 200, 30);
            GUI.Label(labelRect, text, totalStyle);
        }

        private void DrawJumpHistory(Vector2 position)
        {
            if (jumpHistory.Count == 0)
                return;

            GUI.Label(new Rect(position.x, position.y, 200, 20), "Recent Jumps:", historyLabelStyle);

            float barWidth = 150;
            float barHeight = 8;
            float spacing = 10;
            float maxJump = jumpHistory.Max();

            Color originalColor = GUI.color;
            GUI.color = AirTimePlugin.Instance.HistoryColor.Value;

            for (int i = 0; i < jumpHistory.Count; i++)
            {
                float jump = jumpHistory[i];
                float normalizedWidth = (jump / maxJump) * barWidth;

                Rect barRect = new Rect(
                    position.x,
                    position.y + 25 + (i * spacing),
                    normalizedWidth,
                    barHeight
                );

                GUI.DrawTexture(barRect, barTexture);

                string format = GetTimeFormat();
                string timeText = string.Format(format + "s", jump);
                GUI.Label(new Rect(barRect.x + normalizedWidth + 5, barRect.y - 2, 50, 20),
                    timeText, historyLabelStyle);
            }

            GUI.color = originalColor;
        }

        private string GetTimeFormat()
        {
            int places = AirTimePlugin.Instance.DecimalPlaces.Value;
            return "{0:F" + places + "}";
        }

        private Vector2 GetBasePosition()
        {
            var position = AirTimePlugin.Instance.Position.Value;
            float margin = 40; // Increased margin for better padding
            float rightMargin = 280; // More space on right to avoid overlap with health/UI
            float bottomMargin = 250; // More space at bottom for game HUD

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
                    x = Screen.width - rightMargin;
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
                    y = Screen.height - bottomMargin;
                    break;
            }

            return new Vector2(x, y);
        }

        private void InitializeStyles()
        {
            int fontSize = AirTimePlugin.Instance.FontSize.Value;
            Color textColor = AirTimePlugin.Instance.TextColor.Value;

            currentStyle = new GUIStyle();
            currentStyle.fontSize = fontSize;
            currentStyle.fontStyle = FontStyle.Bold;
            currentStyle.normal.textColor = textColor;

            totalStyle = new GUIStyle();
            totalStyle.fontSize = fontSize - 2;
            totalStyle.fontStyle = FontStyle.Normal;
            totalStyle.normal.textColor = new Color(textColor.r * 0.9f, textColor.g * 0.9f, textColor.b * 0.9f, textColor.a);

            historyLabelStyle = new GUIStyle();
            historyLabelStyle.fontSize = fontSize - 4;
            historyLabelStyle.fontStyle = FontStyle.Normal;
            historyLabelStyle.normal.textColor = new Color(textColor.r * 0.8f, textColor.g * 0.8f, textColor.b * 0.8f, textColor.a);

            stylesInitialized = true;
        }

        private void CreateBarTexture()
        {
            barTexture = new Texture2D(1, 1);
            barTexture.SetPixel(0, 0, Color.white);
            barTexture.Apply();
        }
    }
}