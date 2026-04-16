using UnityEngine;

namespace Movement
{
    [CreateAssetMenu(fileName = "PlayerMovementConfig", menuName = "configs/Player Movement/PlayerMovementConfig")]
    public class PlayerMovementConfig : ScriptableObject
    {
        [field: Header("Walk")]
        [field: SerializeField] public float ForwardSpeed { get; private set; } = 5f;
        [field: SerializeField] public float BackwardSpeed { get; private set; } = 4f;
        [field: SerializeField] public float StrafeSpeed { get; private set; } = 4.5f;
        
        [field: Space]
        [field: Header("Run")]
        [field: SerializeField] public float RunForwardSpeed { get; private set; } = 7f;
        [field: SerializeField] public float RunBackwardSpeed { get; private set; } = 5.5f;
        [field: SerializeField] public float RunStrafeSpeed { get; private set; } = 6f;
        
        [field: Space]
        [field: Header("Rotation")]
        [field: SerializeField] public float RotationSpeed { get; private set; } = 720f;

        [field: Space]
        [field: Header("Acceleration")]
        [field: SerializeField] public float WalkSpeedChangeRate { get; private set; } = 5f;
        [field: SerializeField] public float RunSpeedChangeRate { get; private set; } = 7f;
        [field: SerializeField] public float SpeedChangeRateBlendSpeed { get; private set; } = 6f;
    }
}
