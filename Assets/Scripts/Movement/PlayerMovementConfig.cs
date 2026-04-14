using UnityEngine;

namespace Movement
{
    [CreateAssetMenu(fileName = "PlayerMovementConfig", menuName = "configs/Player Movement/PlayerMovementConfig")]
    public class PlayerMovementConfig : ScriptableObject
    {
        [field: SerializeField] public float ForwardSpeed { get; private set; } = 5f;
        [field: SerializeField] public float BackwardSpeed { get; private set; } = 4f;
        [field: SerializeField] public float StrafeSpeed { get; private set; } = 4.5f;
        [field: SerializeField] public float SpeedChangeRate { get; private set; } = 5f;
        [field: SerializeField] public float RotationSpeed { get; private set; } = 720f;
    }
}
