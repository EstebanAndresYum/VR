using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace VR.DualDialHapticPuzzle
{
    /// <summary>
    /// Reads one XR controller and converts its movement around this dial into an angle.
    /// Put this component on the dial pivot and assign a child as dialVisual.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DialInteraction : MonoBehaviour
    {
        [Header("Tracked hand")]
        [SerializeField] private Transform trackingOrigin;
        [SerializeField] private InputDeviceCharacteristics hand =
            InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left;
        [SerializeField] private bool useTriggerInsteadOfGrip;

        [Header("Dial")]
        [SerializeField] private Transform dialVisual;
        [SerializeField, Range(0f, 180f)] private float initialAngle;
        [SerializeField, Range(0f, 180f)] private float minimumAngle;
        [SerializeField, Range(0f, 180f)] private float maximumAngle = 180f;
        [SerializeField] private bool invertRotation;

        [Header("Optional visual feedback")]
        [SerializeField] private Renderer statusRenderer;
        [SerializeField] private Color farColor = new Color(0.32f, 0.32f, 0.38f);
        [SerializeField] private Color nearColor = new Color(0.55f, 0.24f, 0.28f);
        [SerializeField] private Color correctColor = new Color(0.35f, 0.72f, 0.62f);

        private readonly List<InputDevice> matchingDevices = new List<InputDevice>(4);
        private InputDevice controller;
        private Quaternion visualStartRotation;
        private float currentAngle;
        private float previousHandAngle;
        private bool wasGripping;
        private bool hasHandAngle;
        private Color appliedColor;

        public float CurrentAngle => currentAngle;
        public bool IsControllerConnected => controller.isValid;

        private void Awake()
        {
            currentAngle = Mathf.Clamp(initialAngle, minimumAngle, maximumAngle);
            if (dialVisual != null)
            {
                visualStartRotation = dialVisual.localRotation;
                ApplyVisualAngle();
            }

            SetStatusColor(farColor);
        }

        private void Update()
        {
            ResolveController();

            if (!controller.isValid || trackingOrigin == null)
            {
                wasGripping = false;
                hasHandAngle = false;
                return;
            }

            bool gripping = ReadGripState();
            if (!gripping)
            {
                wasGripping = false;
                hasHandAngle = false;
                return;
            }

            if (!TryGetWorldHandPosition(out Vector3 handPosition))
            {
                return;
            }

            if (TryGetDialPlaneAngle(handPosition, out float handAngle))
            {
                if (wasGripping && hasHandAngle)
                {
                    float delta = Mathf.DeltaAngle(previousHandAngle, handAngle);
                    currentAngle = Mathf.Clamp(
                        currentAngle + (invertRotation ? -delta : delta),
                        minimumAngle,
                        maximumAngle);
                    ApplyVisualAngle();
                }

                previousHandAngle = handAngle;
                hasHandAngle = true;
            }

            wasGripping = true;
        }

        public void SetTargetFeedback(float absoluteError, float tolerance)
        {
            if (absoluteError <= tolerance)
            {
                SetStatusColor(correctColor);
            }
            else if (absoluteError <= 30f)
            {
                SetStatusColor(nearColor);
            }
            else
            {
                SetStatusColor(farColor);
            }
        }

        public void ResetAngle()
        {
            currentAngle = Mathf.Clamp(initialAngle, minimumAngle, maximumAngle);
            wasGripping = false;
            hasHandAngle = false;
            ApplyVisualAngle();
        }

        public void Pulse(float intensity, float duration)
        {
            if (!controller.isValid)
            {
                return;
            }

            HapticCapabilities capabilities;
            if (controller.TryGetHapticCapabilities(out capabilities) && capabilities.supportsImpulse)
            {
                controller.SendHapticImpulse(0u, Mathf.Clamp01(intensity), Mathf.Max(0.01f, duration));
            }
        }

        private void ResolveController()
        {
            if (controller.isValid)
            {
                return;
            }

            matchingDevices.Clear();
            InputDevices.GetDevicesWithCharacteristics(hand, matchingDevices);
            if (matchingDevices.Count > 0)
            {
                controller = matchingDevices[0];
            }
        }

        private bool ReadGripState()
        {
            bool pressed;
            InputFeatureUsage<bool> usage = useTriggerInsteadOfGrip
                ? CommonUsages.triggerButton
                : CommonUsages.gripButton;
            return controller.TryGetFeatureValue(usage, out pressed) && pressed;
        }

        private bool TryGetWorldHandPosition(out Vector3 worldPosition)
        {
            if (controller.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 localPosition))
            {
                worldPosition = trackingOrigin.TransformPoint(localPosition);
                return true;
            }

            worldPosition = default;
            return false;
        }

        private bool TryGetDialPlaneAngle(Vector3 handPosition, out float angle)
        {
            Vector3 fromCenter = handPosition - transform.position;
            Vector3 projected = Vector3.ProjectOnPlane(fromCenter, transform.forward);
            if (projected.sqrMagnitude < 0.0001f)
            {
                angle = 0f;
                return false;
            }

            projected.Normalize();
            angle = Mathf.Atan2(
                Vector3.Dot(projected, transform.up),
                Vector3.Dot(projected, transform.right)) * Mathf.Rad2Deg;
            return true;
        }

        private void ApplyVisualAngle()
        {
            if (dialVisual != null)
            {
                dialVisual.localRotation = visualStartRotation * Quaternion.AngleAxis(currentAngle, Vector3.forward);
            }
        }

        private void SetStatusColor(Color color)
        {
            if (statusRenderer == null || appliedColor == color)
            {
                return;
            }

            appliedColor = color;
            statusRenderer.material.color = color;
        }
    }
}
