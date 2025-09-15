using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HKSS.InputTimeline
{
    public class TimelineRenderer : MonoBehaviour
    {
        private Texture2D backgroundTexture;
        private Texture2D buttonTexture;
        private Texture2D holdTexture;
        private Texture2D comboTexture;
        private Texture2D markerTexture;
        private GUIStyle labelStyle;
        private GUIStyle timestampStyle;

        private readonly Dictionary<string, string> buttonSymbols = new Dictionary<string, string>
        {
            { "Jump", "↑" },
            { "Attack", "⚔" },
            { "Dash", "→" },
            { "Focus", "◉" },
            { "Left", "←" },
            { "Right", "→" },
            { "Up", "↑" },
            { "Down", "↓" }
        };

        void Awake()
        {
            CreateTextures();
            CreateStyles();
        }

        void OnDestroy()
        {
            if (backgroundTexture != null) Destroy(backgroundTexture);
            if (buttonTexture != null) Destroy(buttonTexture);
            if (holdTexture != null) Destroy(holdTexture);
            if (comboTexture != null) Destroy(comboTexture);
            if (markerTexture != null) Destroy(markerTexture);
        }

        private void CreateTextures()
        {
            // Create background texture
            backgroundTexture = new Texture2D(1, 1);
            backgroundTexture.SetPixel(0, 0, Color.white);
            backgroundTexture.Apply();

            // Create button texture (rounded rectangle)
            buttonTexture = CreateRoundedRectTexture(40, 30, 5);

            // Create hold texture (wider rounded rectangle)
            holdTexture = CreateRoundedRectTexture(80, 30, 5);

            // Create combo texture
            comboTexture = CreateRoundedRectTexture(100, 35, 8);

            // Create marker texture (vertical line for current time)
            markerTexture = new Texture2D(2, 1);
            markerTexture.SetPixel(0, 0, Color.white);
            markerTexture.SetPixel(1, 0, Color.white);
            markerTexture.Apply();
        }

        private Texture2D CreateRoundedRectTexture(int width, int height, int radius)
        {
            var texture = new Texture2D(width, height);
            var pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool inRoundedRect = true;

                    // Check corners
                    if (x < radius && y < radius)
                    {
                        inRoundedRect = Vector2.Distance(new Vector2(x, y), new Vector2(radius, radius)) <= radius;
                    }
                    else if (x >= width - radius && y < radius)
                    {
                        inRoundedRect = Vector2.Distance(new Vector2(x, y), new Vector2(width - radius - 1, radius)) <= radius;
                    }
                    else if (x < radius && y >= height - radius)
                    {
                        inRoundedRect = Vector2.Distance(new Vector2(x, y), new Vector2(radius, height - radius - 1)) <= radius;
                    }
                    else if (x >= width - radius && y >= height - radius)
                    {
                        inRoundedRect = Vector2.Distance(new Vector2(x, y), new Vector2(width - radius - 1, height - radius - 1)) <= radius;
                    }

                    pixels[y * width + x] = inRoundedRect ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private void CreateStyles()
        {
            labelStyle = new GUIStyle
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            timestampStyle = new GUIStyle
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f, 1f) }
            };
        }

        void OnGUI()
        {
            if (!InputTimelinePlugin.Instance.Enabled.Value)
                return;

            DrawTimeline();
        }

        private void DrawTimeline()
        {
            var config = InputTimelinePlugin.Instance;
            float width = config.TimelineWidth.Value;
            float height = config.TimelineHeight.Value;
            float opacity = config.Opacity.Value;

            // Calculate timeline position
            float x = (Screen.width - width) / 2f;
            float y = 0f;

            switch (config.Position.Value)
            {
                case TimelinePosition.Top:
                    y = 50f;
                    break;
                case TimelinePosition.Bottom:
                    y = Screen.height - height - 50f;
                    break;
                case TimelinePosition.Center:
                    y = (Screen.height - height) / 2f;
                    break;
            }

            var timelineRect = new Rect(x, y, width, height);

            // Draw background
            var bgColor = config.BackgroundColor.Value;
            bgColor.a *= opacity;
            GUI.color = bgColor;
            GUI.DrawTexture(timelineRect, backgroundTexture);

            // Get current time and time window
            float currentTime = Time.time;
            float timeWindow = config.TimeWindow.Value;
            float startTime = currentTime - timeWindow;

            // Draw time markers
            if (config.ShowTimestamps.Value)
            {
                DrawTimeMarkers(timelineRect, startTime, currentTime);
            }

            // Draw current time indicator
            float markerX = x + width - 2;
            GUI.color = new Color(1f, 1f, 1f, opacity * 0.8f);
            GUI.DrawTexture(new Rect(markerX, y, 2, height), markerTexture);

            // Draw input events
            var inputHistory = InputRecorder.GetInputHistory();
            var heldButtons = InputRecorder.GetCurrentlyHeldButtons();

            foreach (var inputEvent in inputHistory)
            {
                DrawInputEvent(timelineRect, inputEvent, startTime, currentTime, opacity);
            }

            // Draw currently held buttons
            foreach (var held in heldButtons)
            {
                DrawHeldButton(timelineRect, held.Key, held.Value, currentTime, opacity);
            }

            // Draw detected combos
            if (config.ShowCombos.Value)
            {
                var combos = InputRecorder.GetDetectedCombos();
                foreach (var combo in combos)
                {
                    DrawCombo(timelineRect, combo, startTime, currentTime, opacity);
                }
            }

            GUI.color = Color.white;
        }

        private void DrawTimeMarkers(Rect timelineRect, float startTime, float endTime)
        {
            float timeRange = endTime - startTime;
            int markerCount = Mathf.Min(10, (int)timeRange + 1);

            for (int i = 0; i <= markerCount; i++)
            {
                float markerTime = i * (timeRange / markerCount);
                float xPos = timelineRect.x + (markerTime / timeRange) * timelineRect.width;

                // Draw tick mark
                GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                GUI.DrawTexture(new Rect(xPos - 1, timelineRect.y + timelineRect.height - 10, 2, 10), markerTexture);

                // Draw time label
                string timeLabel = $"{markerTime:F1}s";
                var labelRect = new Rect(xPos - 20, timelineRect.y + timelineRect.height - 25, 40, 15);
                GUI.Label(labelRect, timeLabel, timestampStyle);
            }
        }

        private void DrawInputEvent(Rect timelineRect, InputEvent inputEvent, float startTime, float endTime, float opacity)
        {
            float timeRange = endTime - startTime;
            float relativeTime = inputEvent.timestamp - startTime;

            if (relativeTime < 0)
                return;

            float xPos = timelineRect.x + (relativeTime / timeRange) * timelineRect.width;

            // Determine button width based on hold duration
            float buttonWidth = 40f;
            if (inputEvent.isHold && InputTimelinePlugin.Instance.HighlightHolds.Value)
            {
                buttonWidth = Mathf.Min(80f, 40f + inputEvent.duration * 40f);
            }

            // Draw button
            var buttonRect = new Rect(xPos - buttonWidth / 2f, timelineRect.y + (timelineRect.height - 30) / 2f, buttonWidth, 30);

            Color buttonColor = inputEvent.isHold
                ? InputTimelinePlugin.Instance.ButtonHoldColor.Value
                : InputTimelinePlugin.Instance.ButtonPressColor.Value;
            buttonColor.a *= opacity;
            GUI.color = buttonColor;

            GUI.DrawTexture(buttonRect, inputEvent.isHold ? holdTexture : buttonTexture);

            // Draw button label
            if (InputTimelinePlugin.Instance.ShowButtonLabels.Value)
            {
                string label = buttonSymbols.ContainsKey(inputEvent.inputName)
                    ? buttonSymbols[inputEvent.inputName]
                    : inputEvent.inputName.Substring(0, Math.Min(3, inputEvent.inputName.Length));

                GUI.color = Color.white;
                GUI.Label(buttonRect, label, labelStyle);
            }
        }

        private void DrawHeldButton(Rect timelineRect, string buttonName, float startTime, float currentTime, float opacity)
        {
            float holdDuration = currentTime - startTime;
            float timeWindow = InputTimelinePlugin.Instance.TimeWindow.Value;

            // Calculate position for held button (extends from start to current edge)
            float relativeStartTime = Math.Max(0, startTime - (currentTime - timeWindow));
            float xStart = timelineRect.x + (relativeStartTime / timeWindow) * timelineRect.width;
            float xEnd = timelineRect.x + timelineRect.width - 2; // Current time position

            var holdRect = new Rect(xStart, timelineRect.y + (timelineRect.height - 30) / 2f, xEnd - xStart, 30);

            Color holdColor = InputTimelinePlugin.Instance.ButtonHoldColor.Value;
            holdColor.a *= opacity * 0.6f; // Slightly transparent for ongoing holds
            GUI.color = holdColor;
            GUI.DrawTexture(holdRect, backgroundTexture);

            // Draw label at the start
            if (InputTimelinePlugin.Instance.ShowButtonLabels.Value && holdRect.width > 20)
            {
                string label = buttonSymbols.ContainsKey(buttonName)
                    ? buttonSymbols[buttonName]
                    : buttonName.Substring(0, Math.Min(3, buttonName.Length));

                var labelRect = new Rect(xStart, holdRect.y, 40, holdRect.height);
                GUI.color = Color.white;
                GUI.Label(labelRect, label, labelStyle);
            }
        }

        private void DrawCombo(Rect timelineRect, ComboSequence combo, float startTime, float endTime, float opacity)
        {
            float timeRange = endTime - startTime;
            float relativeTime = combo.timestamp - startTime;

            if (relativeTime < 0)
                return;

            float xPos = timelineRect.x + (relativeTime / timeRange) * timelineRect.width;

            // Draw combo indicator above the timeline
            var comboRect = new Rect(xPos - 50, timelineRect.y - 25, 100, 20);

            Color comboColor = InputTimelinePlugin.Instance.ComboColor.Value;
            comboColor.a *= opacity;
            GUI.color = comboColor;
            GUI.DrawTexture(comboRect, comboTexture);

            // Draw combo name
            GUI.color = Color.white;
            var comboLabelStyle = new GUIStyle(labelStyle) { fontSize = 12 };
            GUI.Label(comboRect, combo.comboName, comboLabelStyle);
        }
    }
}