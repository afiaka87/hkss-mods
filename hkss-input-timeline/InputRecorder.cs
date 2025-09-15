using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HarmonyLib;

namespace HKSS.InputTimeline
{
    public class InputRecorder : MonoBehaviour
    {
        internal static InputRecorder instance;

        // Track recent actions (3-5 most recent)
        private readonly Queue<PlayerAction> recentActions = new Queue<PlayerAction>();
        private int maxActions = 5; // Configurable, defaults to 5

        // Track previous states to detect transitions
        private bool wasJumping = false;
        private bool wasAttacking = false;
        private bool wasDashing = false;
        private bool wasFocusing = false;
        private bool wasOnGround = true;
        private float lastGroundTime = 0f;

        // Track analog stick states (single-shot)
        private bool wasAnalogLeft = false;
        private bool wasAnalogRight = false;
        private bool wasAnalogUp = false;
        private bool wasAnalogDown = false;
        private const float ANALOG_THRESHOLD = 0.5f;

        // Track D-Pad states (single-shot)
        private bool wasDPadLeft = false;
        private bool wasDPadRight = false;
        private bool wasDPadUp = false;
        private bool wasDPadDown = false;

        void Awake()
        {
            instance = this;
            maxActions = InputTimelinePlugin.Instance.MaxRecentActions.Value;
            InputTimelinePlugin.ModLogger?.LogInfo($"InputRecorder Awake - maxActions={maxActions}, instance set={instance != null}");
        }

        void Update()
        {
            if (!InputTimelinePlugin.Instance.Enabled.Value)
                return;

            if (HeroController.instance == null)
                return;

            var hero = HeroController.instance;
            float currentTime = Time.time;

            // Detect Jump action (transition from grounded to airborne)
            if (!wasJumping && hero.cState.jumping)
            {
                AddAction(new PlayerAction
                {
                    actionName = "Jump",
                    timestamp = currentTime,
                    icon = "JUMP",
                    iconChar = '^'
                });
            }

            // Detect Attack action (transition to attacking)
            if (!wasAttacking && hero.cState.attacking)
            {
                AddAction(new PlayerAction
                {
                    actionName = "Attack",
                    timestamp = currentTime,
                    icon = "ATTK",
                    iconChar = 'X'
                });
            }

            // Detect Dash action (transition to dashing)
            if (!wasDashing && hero.cState.dashing)
            {
                AddAction(new PlayerAction
                {
                    actionName = "Dash",
                    timestamp = currentTime,
                    icon = "DASH",
                    iconChar = '>'
                });
            }

            // Detect Focus/Heal action (transition to focusing)
            if (!wasFocusing && hero.cState.focusing)
            {
                AddAction(new PlayerAction
                {
                    actionName = "Focus",
                    timestamp = currentTime,
                    icon = "HEAL",
                    iconChar = '+'
                });
            }

            // Detect Landing (transition from airborne to grounded)
            if (!wasOnGround && hero.cState.onGround)
            {
                float airTime = currentTime - lastGroundTime;
                if (airTime > 0.1f) // Only track significant air time
                {
                    AddAction(new PlayerAction
                    {
                        actionName = "Land",
                        timestamp = currentTime,
                        icon = "LAND",
                        iconChar = 'v',
                        extraInfo = $"{airTime:F1}s"
                    });
                }
            }

            // Detect Analog Stick movements (single-shot)
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            // Analog Left
            bool analogLeft = horizontal < -ANALOG_THRESHOLD;
            if (analogLeft && !wasAnalogLeft)
            {
                AddAction(new PlayerAction
                {
                    actionName = "Analog Left",
                    timestamp = currentTime,
                    icon = "L<",
                    iconChar = '<'
                });
            }
            wasAnalogLeft = analogLeft;

            // Analog Right
            bool analogRight = horizontal > ANALOG_THRESHOLD;
            if (analogRight && !wasAnalogRight)
            {
                AddAction(new PlayerAction
                {
                    actionName = "Analog Right",
                    timestamp = currentTime,
                    icon = "L>",
                    iconChar = '>'
                });
            }
            wasAnalogRight = analogRight;

            // Analog Up
            bool analogUp = vertical > ANALOG_THRESHOLD;
            if (analogUp && !wasAnalogUp)
            {
                AddAction(new PlayerAction
                {
                    actionName = "Analog Up",
                    timestamp = currentTime,
                    icon = "L^",
                    iconChar = '^'
                });
            }
            wasAnalogUp = analogUp;

            // Analog Down
            bool analogDown = vertical < -ANALOG_THRESHOLD;
            if (analogDown && !wasAnalogDown)
            {
                AddAction(new PlayerAction
                {
                    actionName = "Analog Down",
                    timestamp = currentTime,
                    icon = "Lv",
                    iconChar = 'v'
                });
            }
            wasAnalogDown = analogDown;

            // Detect D-Pad inputs (these are typically separate from analog)
            // Unity maps D-Pad to different input axes or buttons depending on the controller
            float dpadH = 0f;
            float dpadV = 0f;
            try
            {
                dpadH = Input.GetAxis("DPadX");
                dpadV = Input.GetAxis("DPadY");
            }
            catch { /* D-Pad axes might not exist */ }

            // D-Pad Left
            bool dpadLeft = dpadH < -0.5f || Input.GetKeyDown(KeyCode.LeftArrow);
            if (dpadLeft && !wasDPadLeft)
            {
                AddAction(new PlayerAction
                {
                    actionName = "D-Pad Left",
                    timestamp = currentTime,
                    icon = "D<",
                    iconChar = '<'
                });
            }
            wasDPadLeft = dpadLeft;

            // D-Pad Right
            bool dpadRight = dpadH > 0.5f || Input.GetKeyDown(KeyCode.RightArrow);
            if (dpadRight && !wasDPadRight)
            {
                AddAction(new PlayerAction
                {
                    actionName = "D-Pad Right",
                    timestamp = currentTime,
                    icon = "D>",
                    iconChar = '>'
                });
            }
            wasDPadRight = dpadRight;

            // D-Pad Up
            bool dpadUp = dpadV > 0.5f || Input.GetKeyDown(KeyCode.UpArrow);
            if (dpadUp && !wasDPadUp)
            {
                AddAction(new PlayerAction
                {
                    actionName = "D-Pad Up",
                    timestamp = currentTime,
                    icon = "D^",
                    iconChar = '^'
                });
            }
            wasDPadUp = dpadUp;

            // D-Pad Down
            bool dpadDown = dpadV < -0.5f || Input.GetKeyDown(KeyCode.DownArrow);
            if (dpadDown && !wasDPadDown)
            {
                AddAction(new PlayerAction
                {
                    actionName = "D-Pad Down",
                    timestamp = currentTime,
                    icon = "Dv",
                    iconChar = 'v'
                });
            }
            wasDPadDown = dpadDown;

            // Update previous states
            wasJumping = hero.cState.jumping;
            wasAttacking = hero.cState.attacking;
            wasDashing = hero.cState.dashing;
            wasFocusing = hero.cState.focusing;

            if (hero.cState.onGround)
            {
                lastGroundTime = currentTime;
            }
            wasOnGround = hero.cState.onGround;

            // Remove old actions beyond display time
            float displayTime = InputTimelinePlugin.Instance.TimeWindow.Value;
            while (recentActions.Count > 0 && currentTime - recentActions.Peek().timestamp > displayTime)
            {
                recentActions.Dequeue();
            }
        }

        internal void AddAction(PlayerAction action)
        {
            // Add to queue
            recentActions.Enqueue(action);

            // Keep only the configured max number of actions
            while (recentActions.Count > maxActions)
            {
                recentActions.Dequeue();
            }

            // Log action with Info level so we can see it
            InputTimelinePlugin.ModLogger?.LogInfo($"[ACTION] {action.actionName} at {action.timestamp:F2} - Queue size: {recentActions.Count}");
        }

        public static List<PlayerAction> GetRecentActions()
        {
            return instance?.recentActions.ToList() ?? new List<PlayerAction>();
        }
    }

    public class PlayerAction
    {
        public string actionName;
        public float timestamp;
        public string icon;
        public char iconChar; // Single character representation
        public string extraInfo; // Optional extra info like air time
    }

    // Harmony patches to detect special actions - temporarily disabled to test base functionality
    /*
    [HarmonyPatch(typeof(HeroController))]
    public class HeroControllerPatches
    {
        [HarmonyPatch("TakeDamage")]
        [HarmonyPostfix]
        public static void OnTakeDamage(HeroController __instance, int damageAmount)
        {
            if (InputRecorder.instance != null)
            {
                InputRecorder.instance.AddAction(new PlayerAction
                {
                    actionName = "Hit",
                    timestamp = Time.time,
                    icon = "💔",
                    extraInfo = $"-{damageAmount}"
                });
            }
        }

        [HarmonyPatch("Die")]
        [HarmonyPostfix]
        public static void OnDeath()
        {
            if (InputRecorder.instance != null)
            {
                InputRecorder.instance.AddAction(new PlayerAction
                {
                    actionName = "Death",
                    timestamp = Time.time,
                    icon = "☠"
                });
            }
        }
    }
    */
}