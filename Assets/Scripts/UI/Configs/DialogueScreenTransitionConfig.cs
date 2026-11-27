using UnityEngine;

namespace UI.Configs
{
    [CreateAssetMenu(fileName = "DialogueScreenTransitionConfig", menuName = "configs/UI/Dialogue Screen Transition Config")]
    public sealed class DialogueScreenTransitionConfig : ScriptableObject
    {
        [field: SerializeField, Range(0f, 1f)]
        public float MaximumOpacity { get; private set; } = 1f;

        [field: SerializeField, Min(0f)]
        public float FadeInTime { get; private set; } = 0.12f;

        [field: SerializeField, Min(0f)]
        public float HoldTime { get; private set; } = 0.08f;

        [field: SerializeField, Min(0f)]
        public float FadeOutTime { get; private set; } = 0.35f;
    }
}
