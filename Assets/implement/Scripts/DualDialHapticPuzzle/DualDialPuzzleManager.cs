using UnityEngine;
using UnityEngine.Events;

namespace VR.DualDialHapticPuzzle
{
    /// <summary>
    /// Gives each dial an independent proximity pulse and completes when both are aligned.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DualDialPuzzleManager : MonoBehaviour
    {
        [Header("Dials and targets")]
        [SerializeField] private DialInteraction leftDial;
        [SerializeField] private DialInteraction rightDial;
        [SerializeField, Range(0f, 180f)] private float leftTargetAngle = 70f;
        [SerializeField, Range(0f, 180f)] private float rightTargetAngle = 120f;
        [SerializeField, Range(0.5f, 20f)] private float angleTolerance = 6f;
        [SerializeField, Min(0f)] private float requiredHoldSeconds = 1f;

        [Header("Proximity haptics")]
        [SerializeField, Min(0.01f)] private float pulseDuration = 0.06f;
        [SerializeField, Min(0f)] private float farPulseInterval = 0.9f;
        [SerializeField, Min(0f)] private float nearPulseInterval = 0.12f;
        [SerializeField, Range(0f, 1f)] private float farPulseIntensity = 0.16f;
        [SerializeField, Range(0f, 1f)] private float nearPulseIntensity = 0.8f;

        [Header("Events")]
        [SerializeField] private UnityEvent puzzleCompleted = new UnityEvent();

        private float leftPulseTimer;
        private float rightPulseTimer;
        private float alignedTimer;
        private bool isCompleted;

        public bool IsCompleted => isCompleted;
        public float LeftError => leftDial == null ? 180f : Mathf.Abs(leftDial.CurrentAngle - leftTargetAngle);
        public float RightError => rightDial == null ? 180f : Mathf.Abs(rightDial.CurrentAngle - rightTargetAngle);

        private void Update()
        {
            if (isCompleted || leftDial == null || rightDial == null)
            {
                return;
            }

            float leftError = LeftError;
            float rightError = RightError;
            leftDial.SetTargetFeedback(leftError, angleTolerance);
            rightDial.SetTargetFeedback(rightError, angleTolerance);

            UpdateProximityPulse(leftDial, leftError, ref leftPulseTimer);
            UpdateProximityPulse(rightDial, rightError, ref rightPulseTimer);

            if (leftError <= angleTolerance && rightError <= angleTolerance)
            {
                alignedTimer += Time.deltaTime;
                if (alignedTimer >= requiredHoldSeconds)
                {
                    CompletePuzzle();
                }
            }
            else
            {
                alignedTimer = 0f;
            }
        }

        public void ResetPuzzle()
        {
            isCompleted = false;
            alignedTimer = 0f;
            leftPulseTimer = 0f;
            rightPulseTimer = 0f;
            if (leftDial != null) leftDial.ResetAngle();
            if (rightDial != null) rightDial.ResetAngle();
        }

        private void UpdateProximityPulse(DialInteraction dial, float error, ref float timer)
        {
            timer -= Time.deltaTime;
            if (timer > 0f)
            {
                return;
            }

            float proximity = 1f - Mathf.Clamp01(error / 90f);
            float interval = Mathf.Lerp(farPulseInterval, nearPulseInterval, proximity);
            float intensity = Mathf.Lerp(farPulseIntensity, nearPulseIntensity, proximity);
            timer = Mathf.Max(0.01f, interval);
            dial.Pulse(intensity, pulseDuration);
        }

        private void CompletePuzzle()
        {
            isCompleted = true;
            leftDial.Pulse(nearPulseIntensity, 0.18f);
            rightDial.Pulse(nearPulseIntensity, 0.18f);
            puzzleCompleted.Invoke();
            Debug.Log("양손 다이얼 햅틱 퍼즐 완료", this);
        }
    }
}
