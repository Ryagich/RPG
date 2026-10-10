using UnityEngine;

namespace Monsters
{
    [CreateAssetMenu(fileName = "PlantMonsterSearchConfig", menuName = "configs/Monsters/Plant Monster Search")]
    public sealed class PlantMonsterSearchConfig : ScriptableObject
    {
        [field: SerializeField, Min(0.1f)] public float ReachedDistance { get; private set; } = 1.2f;
    }
}
