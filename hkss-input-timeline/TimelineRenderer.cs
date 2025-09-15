using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HKSS.InputTimeline
{
    public class TimelineRenderer : MonoBehaviour
    {
        private Texture2D backgroundTexture;
        private Texture2D actionBoxTexture;
        private Texture2D fadeOverlayTexture;
        private GUIStyle actionStyle;
        private GUIStyle iconStyle;
        private GUIStyle timeStyle;

        void Awake()
        {
            InputTimelinePlugin.ModLogger?.LogInfo("TimelineRenderer Awake - creating textures and styles");
            CreateTextures();
            CreateStyles();
            InputTimelinePlugin.ModLogger?.LogInfo("TimelineRenderer initialized");
        }

        void OnDestroy()
        {
            if (backgroundTexture != null) Destroy(backgroundTexture);
            if (actionBoxTexture != null) Destroy(actionBoxTexture);
            if (fadeOverlayTexture != null) Destroy(fadeOverlayTexture);
        }

        private void CreateTextures()
        {
            // Create background texture
            backgroundTexture = new Texture2D(1, 1);
            backgroundTexture.SetPixel(0, 0, Color.white);
            backgroundTexture.Apply();

            // Create action box texture (rounded rectangle)
            actionBoxTexture = CreateRoundedRectTexture(120, 40, 8);

            // Create fade overlay for older actions
            fadeOverlayTexture = CreateGradientTexture(120, 40);
        }

        private Texture2D CreateGradientTexture(int width, int height)
        {
            var texture = new Texture2D(width, height);
            var pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float alpha = 1f - (x / (float)width) * 0.5f; // Fade from left to right
                    pixels[y * width + x] = new Color(1, 1, 1, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
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
            actionStyle = new GUIStyle
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white },
                padding = new RectOffset(10, 0, 0, 0)
            };

            iconStyle = new GUIStyle
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                fontStyle = FontStyle.Bold,
                richText = true
            };

            timeStyle = new GUIStyle
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f, 1f) },
                padding = new RectOffset(0, 5, 0, 0)
            };
        }

        void OnGUI()
        {
            if (!InputTimelinePlugin.Instance.Enabled.Value)
                return;

            DrawRecentActions();
        }

        private void DrawRecentActions()
        {
            var config = InputTimelinePlugin.Instance;
            float opacity = config.Opacity.Value;

            // Get recent actions
            var recentActions = InputRecorder.GetRecentActions();

            // Always show a debug box to verify rendering is working
            if (recentActions.Count == 0)
            {
                // Draw a stylized "waiting" indicator
                GUI.color = new Color(0.3f, 0.3f, 0.3f, 0.6f);
                GUI.Box(new Rect(20, 80, 180, 25), "[ INPUT TIMELINE - READY ]");
                GUI.color = Color.white;
                return;
            }

            float currentTime = Time.time;

            // Display settings for ASCII boxes
            float boxWidth = 55f;
            float boxHeight = 35f;
            float spacing = 8f;
            float totalWidth = (boxWidth + spacing) * recentActions.Count - spacing;

            // Calculate position
            float x = 20f; // Left side of screen
            float y = 0f;

            switch (config.Position.Value)
            {
                case TimelinePosition.Top:
                    y = 80f;
                    break;
                case TimelinePosition.Bottom:
                    y = Screen.height - boxHeight - 80f;
                    break;
                case TimelinePosition.Center:
                    y = (Screen.height - boxHeight) / 2f;
                    break;
            }

            // Draw sleek background strip
            if (config.ShowBackground.Value)
            {
                var bgRect = new Rect(x - 10, y - 8, totalWidth + 20, boxHeight + 16);
                var bgColor = new Color(0f, 0f, 0f, opacity * 0.5f);
                GUI.color = bgColor;
                GUI.DrawTexture(bgRect, backgroundTexture);

                // Draw border frame
                var borderColor = new Color(0.5f, 0.5f, 0.5f, opacity * 0.3f);
                GUI.color = borderColor;
                GUI.Box(bgRect, "");
            }

            // Draw each action
            int index = 0;
            foreach (var action in recentActions)
            {
                float actionX = x + index * (boxWidth + spacing);
                var actionRect = new Rect(actionX, y, boxWidth, boxHeight);

                DrawAction(actionRect, action, currentTime, opacity, index == recentActions.Count - 1);
                index++;
            }

            GUI.color = Color.white;
        }

        private void DrawAction(Rect rect, PlayerAction action, float currentTime, float opacity, bool isMostRecent)
        {
            float timeSince = currentTime - action.timestamp;
            float fadeAmount = Mathf.Clamp01(1f - (timeSince / InputTimelinePlugin.Instance.TimeWindow.Value));

            // Draw stylized ASCII box
            if (isMostRecent)
            {
                // Highlight most recent with brighter border
                var borderColor = new Color(0.8f, 0.8f, 0.8f, fadeAmount * opacity);
                GUI.color = borderColor;
                GUI.Box(rect, "");

                // Draw inner fill
                var fillRect = new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4);
                var fillColor = new Color(0.2f, 0.3f, 0.4f, fadeAmount * opacity * 0.8f);
                GUI.color = fillColor;
                GUI.DrawTexture(fillRect, backgroundTexture);
            }
            else
            {
                // Normal action box
                var borderColor = new Color(0.5f, 0.5f, 0.5f, fadeAmount * opacity * 0.7f);
                GUI.color = borderColor;
                GUI.Box(rect, "");

                // Draw inner fill
                var fillRect = new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4);
                var fillColor = new Color(0.1f, 0.1f, 0.1f, fadeAmount * opacity * 0.5f);
                GUI.color = fillColor;
                GUI.DrawTexture(fillRect, backgroundTexture);
            }

            // Draw the action text
            var textRect = new Rect(rect.x, rect.y + 2, rect.width, rect.height - 4);
            actionStyle.fontSize = isMostRecent ? 14 : 12;
            actionStyle.alignment = TextAnchor.UpperCenter;

            GUI.color = new Color(1f, 1f, 1f, fadeAmount * opacity);
            GUI.Label(textRect, action.icon, actionStyle);

            // Draw the single character icon below
            var charRect = new Rect(rect.x, rect.y + rect.height * 0.5f, rect.width, rect.height * 0.5f);
            iconStyle.fontSize = isMostRecent ? 18 : 16;
            iconStyle.alignment = TextAnchor.MiddleCenter;

            var iconColor = isMostRecent
                ? new Color(0.9f, 0.95f, 1f, fadeAmount * opacity)
                : new Color(0.7f, 0.8f, 0.9f, fadeAmount * opacity * 0.9f);
            GUI.color = iconColor;
            GUI.Label(charRect, "[" + action.iconChar + "]", iconStyle);

            // Draw extra info if present (like air time)
            if (!string.IsNullOrEmpty(action.extraInfo))
            {
                var infoRect = new Rect(rect.x, rect.y + rect.height - 10, rect.width, 10);
                timeStyle.fontSize = 9;
                timeStyle.alignment = TextAnchor.LowerCenter;
                GUI.color = new Color(0.6f, 0.7f, 0.8f, fadeAmount * opacity * 0.7f);
                GUI.Label(infoRect, action.extraInfo, timeStyle);
            }
        }
    }
}