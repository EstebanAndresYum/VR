using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace VR.CrowbarPuzzle
{
    [DisallowMultipleComponent]
    public sealed class CrowbarDemoCameraController : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float moveSpeed = 1.5f;
        [SerializeField, Min(1f)] private float turnSpeed = 70f;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            Vector3 movement = Vector3.zero;
            movement += transform.forward * ReadAxis(keyboard.wKey, keyboard.sKey);
            movement += transform.right * ReadAxis(keyboard.dKey, keyboard.aKey);
            movement += Vector3.up * ReadAxis(keyboard.eKey, keyboard.qKey);

            float speedMultiplier = keyboard.leftShiftKey.isPressed ? 2.5f : 1f;
            transform.position += movement.normalized * (moveSpeed * speedMultiplier * Time.deltaTime);

            float yaw = ReadAxis(keyboard.rightArrowKey, keyboard.leftArrowKey);
            float pitch = ReadAxis(keyboard.upArrowKey, keyboard.downArrowKey);
            transform.Rotate(Vector3.up, yaw * turnSpeed * Time.deltaTime, Space.World);
            transform.Rotate(Vector3.right, -pitch * turnSpeed * Time.deltaTime, Space.Self);
        }

        private static float ReadAxis(KeyControl positive, KeyControl negative)
        {
            return (positive.isPressed ? 1f : 0f) - (negative.isPressed ? 1f : 0f);
        }
    }
}
