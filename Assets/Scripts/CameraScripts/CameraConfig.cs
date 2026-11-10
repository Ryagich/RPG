using UnityEngine;

namespace CameraScripts
{
    [CreateAssetMenu(fileName = "CameraConfig", menuName = "configs/Camera/CameraConfig")]
    public class CameraConfig : ScriptableObject
    {
        [field: Header("Non-Gameplay")]
        [field: SerializeField, Tooltip("Скорость сглаживания камеры вне игрового режима: в диалогах и других неигровых состояниях. Большее значение — камера быстрее достигает цели.")]
        public float Smoothing { get; private set; } = 4;

        [field: Space]
        [field: Header("Gameplay Follow")]
        [field: SerializeField, Tooltip("Смещение точки, вокруг которой игровая камера вращается, относительно позиции персонажа.")]
        public Vector3 PivotOffset { get; private set; } = new(0f, 1.6f, 0f);
        [field: SerializeField, Min(0f), Tooltip("Базовая дистанция от точки вращения до камеры в игровом режиме.")]
        public float Distance { get; private set; } = 4.5f;
        [field: SerializeField, Tooltip("Боковое смещение камеры от центра персонажа. Положительное значение размещает её справа.")]
        public float ShoulderOffset { get; private set; } = 0.45f;
        [field: SerializeField, Min(0.01f), Tooltip("Минимальный множитель дистанции при изменении масштаба камеры. Например, 0.5 — половина базовой дистанции.")]
        public float MinimumDistanceMultiplier { get; private set; } = 0.5f;
        [field: SerializeField, Min(0.01f), Tooltip("Максимальный множитель дистанции при изменении масштаба камеры. Например, 2 — двойная базовая дистанция.")]
        public float MaximumDistanceMultiplier { get; private set; } = 2.5f;

        [field: Space]
        [field: Header("Gameplay Collision")]
        [field: SerializeField, Tooltip("Слои, которые препятствуют камере. Коллайдеры персонажа всегда игнорируются.")]
        public LayerMask CollisionLayers { get; private set; } = Physics.DefaultRaycastLayers;
        [field: SerializeField, Min(0f), Tooltip("Радиус проверки столкновения камеры. При нуле используется тонкий луч вместо сферы.")]
        public float CollisionRadius { get; private set; } = 0.25f;
        [field: SerializeField, Min(0f), Tooltip("Отступ камеры от обнаруженного препятствия, чтобы она не касалась поверхности.")]
        public float CollisionPadding { get; private set; } = 0.08f;

        [field: Space]
        [field: Header("Gameplay Orbit")]
        [field: SerializeField, Tooltip("Начальный вертикальный угол камеры в градусах при её первом создании.")]
        public float DefaultPitch { get; private set; } = 12f;
        [field: SerializeField, Tooltip("Минимальный вертикальный угол камеры в градусах. Ограничивает взгляд снизу.")]
        public float MinPitch { get; private set; } = -20f;
        [field: SerializeField, Tooltip("Максимальный вертикальный угол камеры в градусах. Ограничивает взгляд сверху.")]
        public float MaxPitch { get; private set; } = 55f;
        [field: SerializeField, Min(0f), Tooltip("Базовая чувствительность горизонтального вращения. Используется, пока игрок не задал своё значение в настройках.")]
        public float HorizontalSensitivity { get; private set; } = 0.15f;
        [field: SerializeField, Min(0f), Tooltip("Базовая чувствительность вертикального вращения. Используется, пока игрок не задал своё значение в настройках.")]
        public float VerticalSensitivity { get; private set; } = 0.12f;
        [field: SerializeField, Min(1f), Tooltip("Максимальная скорость ручного поворота камеры по горизонтали в градусах в секунду.")]
        public float MaximumHorizontalRotationSpeed { get; private set; } = 3600f;
        [field: SerializeField, Min(1f), Tooltip("Максимальная скорость ручного поворота камеры по вертикали в градусах в секунду.")]
        public float MaximumVerticalRotationSpeed { get; private set; } = 3600f;
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
