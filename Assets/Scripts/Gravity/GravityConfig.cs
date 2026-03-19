using UnityEngine;

namespace Gravity
{
    [CreateAssetMenu(fileName = "GravityConfig", menuName = "configs/Gravity/GravityConfig")]
    public class GravityConfig : ScriptableObject
    {
        [field: SerializeField] public float Gravity { get; private set; } = 9.81f;
    }
}