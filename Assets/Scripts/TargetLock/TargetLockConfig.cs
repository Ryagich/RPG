using UnityEngine;

namespace TargetLock
{
    public enum TargetLockControlMode
    {
        Switch,
        Hard,
        Soft,
        Off
    }

    [CreateAssetMenu(fileName = "TargetLockConfig", menuName = "configs/Target Lock/TargetLockConfig")]
    public sealed class TargetLockConfig : ScriptableObject
    {
        [field: Header("Player Settings")]
        [field: SerializeField] public TargetLockControlMode ControlMode { get; private set; } = TargetLockControlMode.Soft;

        [field: SerializeField, Min(0f)] public float SearchRadius { get; private set; } = 14f;
        [field: SerializeField, Min(0f)] public float BreakRadius { get; private set; } = 18f;
        [field: SerializeField, Range(1f, 180f)] public float MaxScreenAngle { get; private set; } = 70f;
        [field: SerializeField, Range(1f, 180f)] public float MaxPlayerAngle { get; private set; } = 135f;
        [field: SerializeField, Range(1f, 180f)] public float BreakScreenAngle { get; private set; } = 135f;
        [field: SerializeField, Min(0f)] public float LostTargetGraceSeconds { get; private set; } = 0.45f;
        [field: SerializeField] public Vector3 LineOfSightOriginOffset { get; private set; } = new(0f, 1.4f, 0f);
        [field: SerializeField] public LayerMask LineOfSightMask { get; private set; } = ~0;
        [field: SerializeField, Min(0f)] public float FacingRotationSpeed { get; private set; } = 720f;
        [field: SerializeField, Min(0f)] public float CenterWeight { get; private set; } = 1f;
        [field: SerializeField, Min(0f)] public float DistanceWeight { get; private set; } = 0.25f;
        [field: SerializeField, Min(0f)] public float PlayerFacingWeight { get; private set; } = 0.2f;

        [field: Space]
        [field: Header("Hard Lock Camera")]
        [field: SerializeField] public Vector3 CameraTargetOffset { get; private set; } = new(0f, 1.2f, 0f);
        [field: SerializeField, Min(0f)] public float CameraYawSharpness { get; private set; } = 9f;
        [field: SerializeField] public float CameraPitch { get; private set; } = 10f;
        [field: SerializeField, Min(0f)] public float CameraDistanceMultiplier { get; private set; } = 1.05f;
        [field: SerializeField] public float CameraShoulderOffsetMultiplier { get; private set; } = 0.4f;
        [field: SerializeField, Range(0f, 1f)] public float CameraFocusBlend { get; private set; } = 0.45f;
        [field: SerializeField, Range(0f, 60f)] public float CameraMaxManualYawOffset { get; private set; } = 18f;
        [field: SerializeField, Range(0f, 30f)] public float CameraMaxManualPitchOffset { get; private set; } = 8f;

        public void CycleControlMode()
        {
            ControlMode = ControlMode switch
            {
                TargetLockControlMode.Switch => TargetLockControlMode.Soft,
                TargetLockControlMode.Soft => TargetLockControlMode.Hard,
                TargetLockControlMode.Hard => TargetLockControlMode.Off,
                _ => TargetLockControlMode.Switch
            };
        }
    }
}
