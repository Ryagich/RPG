using UnityEngine;

namespace CameraScripts
{
    [CreateAssetMenu(fileName = "DialogueCameraConfig", menuName = "configs/Camera/Dialogue Camera Config")]
    public sealed class DialogueCameraConfig : ScriptableObject
    {
        [field: Header("Композиция кадра")]
        [field: SerializeField, Tooltip("Положение диалоговой камеры относительно корня собеседника в его локальных координатах. Ось X — вправо персонажа, Y — вверх, Z — в сторону его взгляда.")]
        public Vector3 CameraLocalOffset { get; private set; } = new(0.35f, 1.6f, 1.2f);
        [field: SerializeField, Tooltip("Точка, на которую смотрит камера, в локальных координатах собеседника. Используется, если для цели не задана отдельная точка лица.")]
        public Vector3 FocusLocalOffset { get; private set; } = new(0f, 1.55f, 0f);
        [field: Space]
        [field: Header("Перемещение в диалог (секунды)")]
        [field: SerializeField, Min(0.01f), Tooltip("Время сглаживания перемещения к диалоговой позиции в секундах. Чем больше значение, тем медленнее и плавнее движение.")]
        public float PositionSmoothTime { get; private set; } = 0.55f;
        [field: SerializeField, Min(0.01f), Tooltip("Время сглаживания поворота к собеседнику в секундах. Чем больше значение, тем медленнее и плавнее поворот.")]
        public float RotationSmoothTime { get; private set; } = 0.35f;

        [field: Space]
        [field: Header("Возврат в геймплей (секунды)")]
        [field: SerializeField, Min(0.01f), Tooltip("Время сглаживания перемещения из диалоговой позиции в игровую в секундах. Чем больше значение, тем медленнее возврат.")]
        public float ReturnPositionSmoothTime { get; private set; } = 0.6f;
        [field: SerializeField, Min(0.01f), Tooltip("Время сглаживания поворота из диалогового ракурса в игровой в секундах. Чем больше значение, тем медленнее возврат.")]
        public float ReturnRotationSmoothTime { get; private set; } = 0.4f;

        [field: Space]
        [field: Header("Возврат управления")]
        [field: SerializeField, Range(0f, 1f), Tooltip("Доля завершённого возврата, после которой игрок получает управление. Камера продолжает плавно двигаться до цели, пока игрок не повернёт, не приблизит/отдалит камеру или не начнёт движение.")]
        public float ReturnInputUnlockProgress { get; private set; } = 0.95f;
        [field: SerializeField, Min(0f), Tooltip("Допустимое расстояние до игровой позиции в мировых единицах. При достижении этого порога возврат считается завершённым и камера точно ставится в игровую позицию.")]
        public float ReturnPositionTolerance { get; private set; } = 0.03f;
        [field: SerializeField, Min(0f), Tooltip("Допустимая разница поворота с игровой камерой в градусах. При достижении этого порога возврат считается завершённым.")]
        public float ReturnRotationTolerance { get; private set; } = 0.5f;

        [field: Space]
        [field: Header("Дуга перехода")]
        [field: SerializeField, Range(0.9f, 1f), Tooltip("Нормализованный прогресс маршрута обхода, после которого камера ставится точно в целевую позицию. Оставляйте близким к 1, чтобы финальный участок не был заметен.")]
        public float ArcCompletionProgress { get; private set; } = 0.999f;

        [field: Space]
        [field: Header("Столкновения камеры")]
        [field: SerializeField, Tooltip("Слои препятствий, которые не позволяют камере проходить сквозь геометрию при переходе к диалоговой позиции.")]
        public LayerMask CollisionLayers { get; private set; } = Physics.DefaultRaycastLayers;
        [field: SerializeField, Min(0f), Tooltip("Радиус объёмной проверки препятствий в мировых единицах. При нуле используется тонкий луч; большее значение лучше защищает камеру от прохода через углы.")]
        public float CollisionRadius { get; private set; } = 0.2f;
        [field: SerializeField, Min(0f), Tooltip("Минимальный отступ камеры от найденного препятствия в мировых единицах.")]
        public float CollisionPadding { get; private set; } = 0.08f;

        [field: Space]
        [field: Header("Обход игрока")]
        [field: SerializeField, Min(0f), Tooltip("Дополнительный радиус вокруг CharacterController игрока в мировых единицах. Если прямой путь камеры пересекает контроллер, камера строит дугу на расстоянии «радиус контроллера + это значение» — при входе в диалог и при возврате в gameplay.")]
        public float PlayerAvoidanceOffset { get; private set; } = 0.35f;
    }
}
