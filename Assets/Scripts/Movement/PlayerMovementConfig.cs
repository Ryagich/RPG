using UnityEngine;

namespace Movement
{
    [CreateAssetMenu(fileName = "PlayerMovementConfig", menuName = "configs/Player Movement/PlayerMovementConfig")]
    public class PlayerMovementConfig : ScriptableObject
    {
        [field: SerializeField] public float Speed { get; private set; } = 5f;
    }
}