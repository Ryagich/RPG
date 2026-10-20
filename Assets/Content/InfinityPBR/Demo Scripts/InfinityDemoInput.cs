using UnityEngine;
using UnityEngine.InputSystem;

namespace InfinityPBR.Demo
{
    /// <summary>Keeps optional demo controls independent from the gameplay input layer.</summary>
    internal static class InfinityDemoInput
    {
        public static bool RightMouseHeld => Mouse.current != null && Mouse.current.rightButton.isPressed;
        public static bool RightMousePressed => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        public static Vector2 MousePosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        public static Vector2 MouseDelta => Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        public static bool IsPressedThisFrame(Key key) => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
        public static bool IsHeld(Key key) => Keyboard.current != null && Keyboard.current[key].isPressed;
        public static bool ShiftHeld => IsHeld(Key.LeftShift) || IsHeld(Key.RightShift);
        public static bool ControlHeld => IsHeld(Key.LeftCtrl) || IsHeld(Key.RightCtrl);
        public static float Vertical => (IsHeld(Key.W) || IsHeld(Key.UpArrow) ? 1f : 0f) - (IsHeld(Key.S) || IsHeld(Key.DownArrow) ? 1f : 0f);
        public static float Horizontal => (IsHeld(Key.D) || IsHeld(Key.RightArrow) ? 1f : 0f) - (IsHeld(Key.A) || IsHeld(Key.LeftArrow) ? 1f : 0f);

        public static bool IsPressedThisFrame(KeyCode keyCode)
        {
            if (System.Enum.TryParse(keyCode.ToString(), true, out Key key))
                return IsPressedThisFrame(key);

            return keyCode switch
            {
                KeyCode.Return => IsPressedThisFrame(Key.Enter),
                KeyCode.LeftControl => IsPressedThisFrame(Key.LeftCtrl),
                KeyCode.RightControl => IsPressedThisFrame(Key.RightCtrl),
                _ => false
            };
        }
    }
}
