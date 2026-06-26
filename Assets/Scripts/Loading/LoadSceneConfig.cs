using UnityEngine;

namespace Loading
{
    [CreateAssetMenu(fileName = "LoadSceneConfig", menuName = "configs/Loading/LoadSceneConfig")]
    public sealed class LoadSceneConfig : ScriptableObject
    {
        [field: SerializeField] public string LoadSceneName { get; private set; } = "Load Scene";
        [field: SerializeField] public string MenuSceneName { get; private set; } = "Menu";
        [field: SerializeField] public string PressAnyKeyText { get; private set; } = "Press any key";

        [field: Space]
        [field: SerializeField, Min(0.01f)] public float SimpleAnimationFrameSeconds { get; private set; } = 0.18f;
        [field: SerializeField, Min(0.01f)] public float ReadyTextBlinkSpeed { get; private set; } = 1.25f;
        [field: SerializeField, Range(0f, 1f)] public float ReadyTextMinAlpha { get; private set; } = 0.45f;
        [field: SerializeField, Range(0f, 1f)] public float ReadyTextMaxAlpha { get; private set; } = 0.95f;
    }
}
