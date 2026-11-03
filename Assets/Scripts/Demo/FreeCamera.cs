using UnityEngine;
using UnityEngine.InputSystem;

namespace Demo
{
    [RequireComponent(typeof(Camera)), DisallowMultipleComponent]
    [AddComponentMenu("Demo/Free Camera")]
    public sealed class FreeCamera : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 10f;
        [SerializeField, Min(1f)] private float boostMultiplier = 4f;
        [Tooltip("Degrees per pixel of mouse movement.")]
        [SerializeField, Min(0f)] private float lookSensitivity = 0.15f;

        private bool flying;
        private Mouse capturedMouse;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private float yaw;
        private float pitch;
        private bool lookSuspended;

        private void OnEnable() => lookSuspended = false;

        private void Update()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (!Application.isFocused || mouse == null)
            {
                ReleaseCursor();
                return;
            }
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                lookSuspended = true;
                ReleaseCursor();
                return;
            }
            if (lookSuspended)
            {
                if (!mouse.leftButton.wasPressedThisFrame)
                    return;
                lookSuspended = false;
            }

            bool justCaptured = !flying;
            if (justCaptured)
                CaptureCursor(mouse);
            if (mouse != capturedMouse || Cursor.lockState != CursorLockMode.Locked)
            {
                lookSuspended = true;
                ReleaseCursor();
                return;
            }

            if (!justCaptured)
            {
                // Mouse delta is already per frame; do not multiply by deltaTime.
                Vector2 look = mouse.delta.ReadValue() * lookSensitivity;
                yaw = Mathf.Repeat(yaw + look.x, 360f);
                pitch = Mathf.Clamp(pitch - look.y, -89f, 89f);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            if (keyboard == null)
                return;
            float forward = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            float right = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            float up = (keyboard.eKey.isPressed ? 1f : 0f) - (keyboard.qKey.isPressed ? 1f : 0f);
            Vector3 movement = transform.forward * forward + transform.right * right + Vector3.up * up;
            float speed = moveSpeed * (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed
                ? boostMultiplier : 1f);
            transform.position += Vector3.ClampMagnitude(movement, 1f) * (speed * Time.unscaledDeltaTime);
        }

        private void CaptureCursor(Mouse mouse)
        {
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Vector3 angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.x), -89f, 89f);
            capturedMouse = mouse;
            flying = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void ReleaseCursor()
        {
            if (!flying)
                return;
            flying = false;
            capturedMouse = null;
            // Do not overwrite a lock mode set by another cursor owner.
            if (Cursor.lockState == CursorLockMode.Locked || Cursor.lockState == previousCursorLock)
            {
                Cursor.lockState = previousCursorLock;
                Cursor.visible = previousCursorVisible;
            }
        }

        private void OnDisable() => ReleaseCursor();

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                lookSuspended = true;
                ReleaseCursor();
            }
        }
    }
}
