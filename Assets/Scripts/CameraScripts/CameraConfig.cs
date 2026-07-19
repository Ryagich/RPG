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
}
