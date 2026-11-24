using UnityEngine;

namespace UI.Configs
{
    [CreateAssetMenu(fileName = "Target Lock Marker Config", menuName = "configs/UI/Target Lock Marker Config")]
    public sealed class TargetLockMarkerConfig : ScriptableObject
    {
        [field: Header("World placement")]
        [field: SerializeField] public Vector3 WorldOffset { get; private set; } = new(0f, 0.4f, 0f);
        [field: SerializeField, Range(0f, 0.49f), Tooltip("Normalized inset from every screen edge. Targets outside this area do not receive a marker.")]
        public float ScreenEdgeInset { get; private set; }
        [field: SerializeField, Min(0.02f), Tooltip("How often marker eligibility is refreshed. Marker positions themselves update every frame.")]
        public float CandidateRefreshInterval { get; private set; } = 0.1f;

        [field: Header("Available targets")]
        [field: SerializeField, Min(1f)] public float AvailableTargetDiameter { get; private set; } = 12f;
        [field: SerializeField] public Sprite AvailableTargetSprite { get; private set; }

        [field: Header("Current target")]
        [field: SerializeField, Min(1f)] public float CurrentTargetDiameter { get; private set; } = 18f;
        [field: SerializeField] public Sprite CurrentTargetSprite { get; private set; }
    }
}
