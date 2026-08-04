using System;
using System.Collections.Generic;
using UI.UIElements;
using UnityEngine;
using UnityEngine.Localization;

namespace UI.Configs
{
    [CreateAssetMenu(fileName = "Opening Slides Config", menuName = "configs/UI/OpeningSlidesConfig")]
    public sealed class OpeningSlidesConfig : ScriptableObject
    {
        [field: SerializeField] public OpeningSlidesView ViewPrefab { get; private set; }
        [field: SerializeField] public List<OpeningSlide> Slides { get; private set; } = new();
        [field: SerializeField, Min(0f)] public float InitialHintsShowTime { get; private set; } = 3f;
        [field: SerializeField, Min(0f)] public float HoldSpaceToSkipTime { get; private set; } = 1.5f;
        [field: SerializeField, Min(0f)] public float AutoAdvanceDelay { get; private set; } = 0.8f;
    }

    [Serializable]
    public sealed class OpeningSlide
    {
        [field: SerializeField] public Sprite Image { get; private set; }
        [field: SerializeField] public AudioClip VoiceOver { get; private set; }
        [field: SerializeField] public LocalizedString Text { get; private set; }
    }
}
