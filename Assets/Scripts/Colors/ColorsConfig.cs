using UnityEngine;

namespace Colors
{
    [CreateAssetMenu(fileName = "ColorsConfig", menuName = "configs/Colors/ColorsConfig")]
    public class ColorsConfig : ScriptableObject
    {
        [field: SerializeField] public Color White { get; private set; } = Color.white;
        [field: SerializeField] public Color Gray { get; private set; } = Color.gray;
    }
}