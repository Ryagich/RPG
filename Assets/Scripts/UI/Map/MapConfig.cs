using UnityEngine;

namespace UI.Map
{
    [CreateAssetMenu(fileName = "Map Config", menuName = "configs/UI/Map Config")]
    public class MapConfig : ScriptableObject
    {
        [field: SerializeField, Min(0.01f)] public float ZoomSpeed { get; private set; } = 0.2f;
        [field: SerializeField, Min(0.1f)] public float MinZoom { get; private set; } = 1f;
        [field: SerializeField, Min(0.1f)] public float MaxZoom { get; private set; } = 3f;
        [field: SerializeField, Min(0.01f)] public float FocusMoveDuration { get; private set; } = 0.4f;
    }
}
