using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace HKSS.ParryWindowFlash
{
    public class ParryIndicator : MonoBehaviour
    {
        private static ParryIndicator instance;

        private Texture2D screenBorderTexture;
        private Texture2D warningTexture;
        private bool isFlashing = false;
        private float flashAlpha = 0f;
        private float warningAlpha = 0f;
        private AudioSource audioSource;
        private GameObject characterGlow;
        private List<FlashRequest> pendingFlashes = new List<FlashRequest>();

        private class FlashRequest
        {
            public float timeToFlash;
            public float intensity;
            public bool hasWarning;
        }

        void Awake()
        {
            instance = this;
            CreateTextures();
            SetupAudio();
        }

        void Start()
        {
            ParryWindowPlugin.ModLogger?.LogInfo("ParryIndicator started");
        }

        void OnDestroy()
        {
            if (screenBorderTexture != null)
                Destroy(screenBorderTexture);
            if (warningTexture != null)
                Destroy(warningTexture);
            if (characterGlow != null)
                Destroy(characterGlow);
        }

        private void CreateTextures()
        {
            // Create a white texture for screen border flash
            screenBorderTexture = new Texture2D(1, 1);
            screenBorderTexture.SetPixel(0, 0, Color.white);
            screenBorderTexture.Apply();

            // Create warning texture
            warningTexture = new Texture2D(1, 1);
            warningTexture.SetPixel(0, 0, new Color(1f, 1f, 0f, 0.5f)); // Yellow warning
            warningTexture.Apply();
        }

        private void SetupAudio()
        {
            if (ParryWindowPlugin.Instance.AudioCue.Value)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.volume = ParryWindowPlugin.Instance.AudioVolume.Value;
                // In a real implementation, you would load an actual audio clip here
                // audioSource.clip = Resources.Load<AudioClip>("ParrySound");
            }
        }

        public static void TriggerParryFlash(float delay, float attackDistance)
        {
            if (instance != null)
            {
                instance.ScheduleFlash(delay, attackDistance);
            }
        }

        private void ScheduleFlash(float delay, float attackDistance)
        {
            float intensity = CalculateIntensity(attackDistance);

            FlashRequest request = new FlashRequest
            {
                timeToFlash = Time.time + delay,
                intensity = intensity,
                hasWarning = ParryWindowPlugin.Instance.EarlyWarning.Value
            };

            pendingFlashes.Add(request);
            StartCoroutine(FlashCoroutine(request));
        }

        private float CalculateIntensity(float distance)
        {
            float maxRange = ParryWindowPlugin.Instance.ParryRange.Value;
            float normalizedDistance = Mathf.Clamp01(1f - (distance / maxRange));
            return normalizedDistance * ParryWindowPlugin.Instance.FlashIntensity.Value;
        }

        private IEnumerator FlashCoroutine(FlashRequest request)
        {
            // Show early warning if enabled
            if (request.hasWarning)
            {
                float warningTime = request.timeToFlash - Time.time - ParryWindowPlugin.Instance.EarlyWarningTime.Value;
                if (warningTime > 0)
                {
                    yield return new WaitForSeconds(warningTime);
                    StartCoroutine(ShowWarning(ParryWindowPlugin.Instance.EarlyWarningTime.Value));
                }
            }

            // Wait for the exact parry moment
            float timeToWait = request.timeToFlash - Time.time;
            if (timeToWait > 0)
            {
                yield return new WaitForSeconds(timeToWait);
            }

            // Trigger the flash
            StartCoroutine(DoFlash(request.intensity));

            // Play audio cue if enabled
            if (ParryWindowPlugin.Instance.AudioCue.Value && audioSource != null)
            {
                // audioSource.Play();
                ParryWindowPlugin.ModLogger?.LogDebug("Audio cue would play here");
            }

            // Show character glow if enabled
            if (ParryWindowPlugin.Instance.ShowCharacterGlow.Value)
            {
                StartCoroutine(ShowCharacterGlow(ParryWindowPlugin.Instance.FlashDuration.Value));
            }

            // Clean up the request
            pendingFlashes.Remove(request);
        }

        private IEnumerator DoFlash(float intensity)
        {
            isFlashing = true;
            float duration = ParryWindowPlugin.Instance.FlashDuration.Value;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                // Quick flash in, slower fade out
                if (t < 0.2f)
                {
                    flashAlpha = Mathf.Lerp(0f, intensity, t / 0.2f);
                }
                else
                {
                    flashAlpha = Mathf.Lerp(intensity, 0f, (t - 0.2f) / 0.8f);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            flashAlpha = 0f;
            isFlashing = false;
        }

        private IEnumerator ShowWarning(float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                warningAlpha = Mathf.PingPong(t * 4f, 1f) * 0.3f; // Pulsing warning
                elapsed += Time.deltaTime;
                yield return null;
            }

            warningAlpha = 0f;
        }

        private IEnumerator ShowCharacterGlow(float duration)
        {
            if (HeroController.instance == null)
                yield break;

            // In a full implementation, you would add a glow effect to the character sprite
            // For now, we'll just log it
            ParryWindowPlugin.ModLogger?.LogDebug($"Character glow for {duration}s");
            yield return new WaitForSeconds(duration);
        }

        void OnGUI()
        {
            if (!ParryWindowPlugin.Instance.Enabled.Value)
                return;

            // Draw warning indicator
            if (warningAlpha > 0f)
            {
                DrawWarning();
            }

            // Draw flash effect
            if (isFlashing && flashAlpha > 0f)
            {
                DrawFlash();
            }
        }

        private void DrawFlash()
        {
            Color flashColor = ParryWindowPlugin.Instance.FlashColor.Value;
            flashColor.a = flashAlpha;
            GUI.color = flashColor;

            var flashStyle = ParryWindowPlugin.Instance.FlashStyle.Value;

            switch (flashStyle)
            {
                case FlashType.ScreenEdge:
                    DrawScreenEdge();
                    break;
                case FlashType.FullScreen:
                    DrawFullScreen();
                    break;
                case FlashType.Corner:
                    DrawCorners();
                    break;
                case FlashType.CharacterOnly:
                    // Handled by character glow
                    break;
            }

            GUI.color = Color.white;
        }

        private void DrawScreenEdge()
        {
            int borderWidth = 20;

            // Top
            GUI.DrawTexture(new Rect(0, 0, Screen.width, borderWidth), screenBorderTexture);
            // Bottom
            GUI.DrawTexture(new Rect(0, Screen.height - borderWidth, Screen.width, borderWidth), screenBorderTexture);
            // Left
            GUI.DrawTexture(new Rect(0, 0, borderWidth, Screen.height), screenBorderTexture);
            // Right
            GUI.DrawTexture(new Rect(Screen.width - borderWidth, 0, borderWidth, Screen.height), screenBorderTexture);
        }

        private void DrawFullScreen()
        {
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), screenBorderTexture);
        }

        private void DrawCorners()
        {
            int cornerSize = 100;

            // Top-left
            GUI.DrawTexture(new Rect(0, 0, cornerSize, cornerSize), screenBorderTexture);
            // Top-right
            GUI.DrawTexture(new Rect(Screen.width - cornerSize, 0, cornerSize, cornerSize), screenBorderTexture);
            // Bottom-left
            GUI.DrawTexture(new Rect(0, Screen.height - cornerSize, cornerSize, cornerSize), screenBorderTexture);
            // Bottom-right
            GUI.DrawTexture(new Rect(Screen.width - cornerSize, Screen.height - cornerSize, cornerSize, cornerSize), screenBorderTexture);
        }

        private void DrawWarning()
        {
            Color warningColor = Color.yellow;
            warningColor.a = warningAlpha;
            GUI.color = warningColor;

            // Draw subtle warning indicators
            int indicatorSize = 50;
            float centerX = Screen.width / 2f;
            float centerY = Screen.height / 2f;

            // Draw warning arrows pointing inward
            GUI.DrawTexture(new Rect(centerX - indicatorSize/2, 50, indicatorSize, indicatorSize), warningTexture);
            GUI.DrawTexture(new Rect(centerX - indicatorSize/2, Screen.height - 100, indicatorSize, indicatorSize), warningTexture);

            GUI.color = Color.white;
        }
    }
}