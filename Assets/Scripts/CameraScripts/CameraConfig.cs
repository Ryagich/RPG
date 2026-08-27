using UnityEngine;

namespace CameraScripts
{
    [CreateAssetMenu(fileName = "CameraConfig", menuName = "configs/Camera/CameraConfig")]
    public class CameraConfig : ScriptableObject
    {
        [field: Header("Non-Gameplay")]
        [field: SerializeField] public float Smoothing { get; private set; } = 4;

        [field: Space]
        [field: Header("Gameplay Follow")]
        [field: SerializeField] public Vector3 PivotOffset { get; private set; } = new(0f, 1.6f, 0f);
        [field: SerializeField, Min(0f)] public float Distance { get; private set; } = 4.5f;
        [field: SerializeField] public float ShoulderOffset { get; private set; } = 0.45f;
        [field: SerializeField, Min(0.01f)] public float MinimumDistanceMultiplier { get; private set; } = 0.5f;
        [field: SerializeField, Min(0.01f)] public float MaximumDistanceMultiplier { get; private set; } = 2.5f;
        [field: SerializeField, Min(0f)] public float PositionSharpness { get; private set; } = 10f;
        [field: SerializeField, Min(0f)] public float RotationSharpness { get; private set; } = 12f;

        [field: Space]
        [field: Header("Gameplay Collision")]
        [field: SerializeField] public LayerMask CollisionLayers { get; private set; } = Physics.DefaultRaycastLayers;
        [field: SerializeField, Min(0f)] public float CollisionRadius { get; private set; } = 0.25f;
        [field: SerializeField, Min(0f)] public float CollisionPadding { get; private set; } = 0.08f;

        [field: Space]
        [field: Header("Gameplay Orbit")]
        [field: SerializeField] public float DefaultPitch { get; private set; } = 12f;
        [field: SerializeField] public float MinPitch { get; private set; } = -20f;
        [field: SerializeField] public float MaxPitch { get; private set; } = 55f;
        [field: SerializeField, Min(0f)] public float HorizontalSensitivity { get; private set; } = 0.15f;
        [field: SerializeField, Min(0f)] public float VerticalSensitivity { get; private set; } = 0.12f;
    }

    /// <summary>
    /// Persistent player preferences for the gameplay camera's per-axis rotation speed.
    /// The configured values remain the defaults for players who have not changed them.
    /// </summary>
    public static class CameraSensitivitySettings
    {
        private const string HorizontalPreferenceKey = "RPG.Camera.HorizontalSensitivity";
        private const string VerticalPreferenceKey = "RPG.Camera.VerticalSensitivity";

        public const float Minimum = 0.01f;
        public const float Maximum = 1f;
        private static float horizontal = -1f;
        private static float vertical = -1f;

        public static float GetHorizontal(float defaultValue) => GetValue(ref horizontal, HorizontalPreferenceKey, defaultValue);
        public static float GetVertical(float defaultValue) => GetValue(ref vertical, VerticalPreferenceKey, defaultValue);

        public static void SetHorizontal(float value)
        {
            SetValue(ref horizontal, HorizontalPreferenceKey, value);
        }

        public static void SetVertical(float value)
        {
            SetValue(ref vertical, VerticalPreferenceKey, value);
        }

        private static float GetValue(ref float value, string preferenceKey, float defaultValue)
        {
            if (value < Minimum)
            {
                value = Mathf.Clamp(PlayerPrefs.GetFloat(preferenceKey, defaultValue), Minimum, Maximum);
            }

            return value;
        }

        private static void SetValue(ref float storedValue, string preferenceKey, float value)
        {
            storedValue = Mathf.Clamp(value, Minimum, Maximum);
            PlayerPrefs.SetFloat(preferenceKey, storedValue);
            PlayerPrefs.Save();
        }
    }
}
