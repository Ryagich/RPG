using UnityEngine;

namespace NPC
{
    [CreateAssetMenu(fileName = "NpcVisionConfig", menuName = "configs/NPC/NpcVisionConfig")]
    public sealed class NpcVisionConfig : ScriptableObject
    {
        [field: SerializeField, Min(0f)] public float ViewDistance { get; private set; } = 8f;
        [field: SerializeField, Range(0f, 360f)] public float ViewAngle { get; private set; } = 90f;
        [field: SerializeField] public bool DrawVisionForAllNpcs { get; private set; }
    }
}
