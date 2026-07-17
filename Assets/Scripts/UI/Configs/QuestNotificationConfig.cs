using UnityEngine;

namespace UI.Configs
{
    [CreateAssetMenu(fileName = "Quest Notification Config", menuName = "configs/UI/Quest Notification Config")]
    public sealed class QuestNotificationConfig : ScriptableObject
    {
        [field: SerializeField, Min(0f)] public float FadeInTime { get; private set; } = 0.25f;
        [field: SerializeField, Min(0f)] public float HoldTime { get; private set; } = 3f;
        [field: SerializeField, Min(0f)] public float FadeOutTime { get; private set; } = 0.25f;
    }
}
