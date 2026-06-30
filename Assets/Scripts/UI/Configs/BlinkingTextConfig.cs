using UnityEngine;

namespace UI.Configs
{
    [CreateAssetMenu(fileName = "Blinking Text Config", menuName = "configs/UI/BlinkingTextConfig")]
    public sealed class BlinkingTextConfig : ScriptableObject
    {
        [field: SerializeField, Min(0f)] public float BlinkSpeed { get; private set; } = 1.25f;
        [field: SerializeField, Range(0f, 1f)] public float MinAlpha { get; private set; } = 0.45f;
        [field: SerializeField, Range(0f, 1f)] public float MaxAlpha { get; private set; } = 0.95f;
    }
}
