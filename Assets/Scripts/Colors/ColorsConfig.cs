using UnityEngine;

namespace Colors
{
    [CreateAssetMenu(fileName = "ColorsConfig", menuName = "configs/Colors/ColorsConfig")]
    public class ColorsConfig : ScriptableObject
    {
        [field: SerializeField] public string WhiteHex { get; private set; } = "#FFFFFF";
        [field: SerializeField] public string GrayHex { get; private set; } = "#808080";
    }
}