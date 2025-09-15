using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HarmonyLib;

namespace HKSS.InputTimeline
{
    public class InputRecorder : MonoBehaviour
    {
        private static InputRecorder instance;

        // Input event tracking
        private readonly List<InputEvent> inputHistory = new List<InputEvent>();
        private readonly Dictionary<string, float> buttonPressStartTimes = new Dictionary<string, float>();
        private readonly List<ComboSequence> detectedCombos = new List<ComboSequence>();

        // Combo detection
        private readonly float comboWindowTime = 0.5f; // Time between inputs to consider them part of a combo
        private readonly List<string> currentComboBuffer = new List<string>();
        private float lastComboInputTime = 0f;

        // Known combo patterns (can be extended)
        private readonly Dictionary<string, string> comboPatterns = new Dictionary<string, string>
        {
            { "Jump,Attack", "Jump Attack" },
            { "Dash,Attack", "Dash Strike" },
            { "Jump,Dash", "Air Dash" },
            { "Attack,Attack,Attack", "Triple Strike" },
            { "Down,Attack", "Down Strike" },
            { "Up,Attack", "Up Strike" }
        };

        void Awake()
        {
            instance = this;
        }

        void Update()
        {
            if (!InputTimelinePlugin.Instance.Enabled.Value)
                return;

            if (HeroController.instance == null)
                return;

            // Track inputs using cState and direct input
            var hero = HeroController.instance;

            // Use cState for tracking actual actions
            TrackInput("Jump", hero.cState.jumping);
            TrackInput("Attack", hero.cState.attacking);
            TrackInput("Dash", hero.cState.dashing);
            TrackInput("Focus", hero.cState.focusing);

            // Track directional inputs using Unity Input
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            if (horizontal < -0.5f)
                TrackInput("Left", true);
            else if (horizontal > 0.5f)
                TrackInput("Right", true);
            else
            {
                TrackInput("Left", false);
                TrackInput("Right", false);
            }

            if (vertical < -0.5f)
                TrackInput("Down", true);
            else if (vertical > 0.5f)
                TrackInput("Up", true);
            else
            {
                TrackInput("Down", false);
                TrackInput("Up", false);
            }

            // Clean old events
            float currentTime = Time.time;
            float timeWindow = InputTimelinePlugin.Instance.TimeWindow.Value;
            inputHistory.RemoveAll(e => currentTime - e.timestamp > timeWindow);
            detectedCombos.RemoveAll(c => currentTime - c.timestamp > timeWindow);

            // Check for combo timeout
            if (currentComboBuffer.Count > 0 && currentTime - lastComboInputTime > comboWindowTime)
            {
                currentComboBuffer.Clear();
            }
        }

        private void TrackInput(string inputName, bool isPressed)
        {
            bool wasPressed = buttonPressStartTimes.ContainsKey(inputName);

            if (isPressed && !wasPressed)
            {
                // Button just pressed
                float currentTime = Time.time;
                buttonPressStartTimes[inputName] = currentTime;

                var inputEvent = new InputEvent
                {
                    inputName = inputName,
                    timestamp = currentTime,
                    duration = 0f,
                    isHold = false
                };

                inputHistory.Add(inputEvent);

                // Add to combo buffer if enabled
                if (InputTimelinePlugin.Instance.ShowCombos.Value)
                {
                    if (currentTime - lastComboInputTime <= comboWindowTime || currentComboBuffer.Count == 0)
                    {
                        currentComboBuffer.Add(inputName);
                        lastComboInputTime = currentTime;
                        CheckForCombos(currentTime);
                    }
                    else
                    {
                        currentComboBuffer.Clear();
                        currentComboBuffer.Add(inputName);
                        lastComboInputTime = currentTime;
                    }
                }
            }
            else if (!isPressed && wasPressed)
            {
                // Button just released
                float pressStartTime = buttonPressStartTimes[inputName];
                float duration = Time.time - pressStartTime;

                // Update the event with duration
                var inputEvent = inputHistory.LastOrDefault(e => e.inputName == inputName && e.timestamp == pressStartTime);
                if (inputEvent != null)
                {
                    inputEvent.duration = duration;
                    inputEvent.isHold = duration >= InputTimelinePlugin.Instance.HoldThreshold.Value;
                }

                buttonPressStartTimes.Remove(inputName);
            }
        }

        private void CheckForCombos(float timestamp)
        {
            if (currentComboBuffer.Count < 2)
                return;

            // Check if current buffer matches any combo pattern
            string bufferSequence = string.Join(",", currentComboBuffer);

            foreach (var pattern in comboPatterns)
            {
                if (bufferSequence.EndsWith(pattern.Key))
                {
                    var combo = new ComboSequence
                    {
                        comboName = pattern.Value,
                        inputs = pattern.Key.Split(',').ToList(),
                        timestamp = timestamp
                    };

                    detectedCombos.Add(combo);

                    // Log combo detection
                    InputTimelinePlugin.ModLogger?.LogDebug($"Combo detected: {pattern.Value}");
                    break;
                }
            }
        }

        public static List<InputEvent> GetInputHistory()
        {
            return instance?.inputHistory ?? new List<InputEvent>();
        }

        public static List<ComboSequence> GetDetectedCombos()
        {
            return instance?.detectedCombos ?? new List<ComboSequence>();
        }

        public static Dictionary<string, float> GetCurrentlyHeldButtons()
        {
            return instance?.buttonPressStartTimes ?? new Dictionary<string, float>();
        }
    }

    public class InputEvent
    {
        public string inputName;
        public float timestamp;
        public float duration;
        public bool isHold;
    }

    public class ComboSequence
    {
        public string comboName;
        public List<string> inputs;
        public float timestamp;
    }

    // Harmony patches to detect special inputs
    [HarmonyPatch(typeof(HeroController))]
    public class HeroControllerPatches
    {
        [HarmonyPatch("DoAttack")]
        [HarmonyPostfix]
        public static void OnAttack()
        {
            // Attack action triggered
        }

        [HarmonyPatch("HeroDash")]
        [HarmonyPostfix]
        public static void OnDash()
        {
            // Dash action triggered
        }

        [HarmonyPatch("DoJump")]
        [HarmonyPostfix]
        public static void OnJump()
        {
            // Jump action triggered
        }
    }
}