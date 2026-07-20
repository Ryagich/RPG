using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace UI.Map
{
    [Serializable]
    public sealed class MapIconDefinition
    {
        [SerializeField] private string name;
        [SerializeField] private Sprite sprite;
        [SerializeField] private Color color = Color.white;

        public string Name => name;
        public Sprite Sprite => sprite;
        public Color Color => color;
    }

    [CreateAssetMenu(fileName = "Map Icons Config", menuName = "configs/UI/Map Icons Config")]
    public sealed class MapIconsConfig : ScriptableObject
    {
        public const string QuestIconName = "Quest";

        [Header("Icons")]
        [SerializeField] private List<MapIconDefinition> icons = new();

        [Header("Hover animation")]
        [SerializeField, Min(0f)] private float hoverAnimationDuration = 0.15f;
        [SerializeField] private Ease hoverEase = Ease.OutQuad;
        [SerializeField] private Vector3 hoveredIconScale = new(1.15f, 1.15f, 1f);

        [Header("Popup")]
        [SerializeField, Min(0f)] private float popupHoverDelaySeconds = 0.5f;

        public float HoverAnimationDuration => hoverAnimationDuration;
        public Ease HoverEase => hoverEase;
        public Vector3 HoveredIconScale => hoveredIconScale;
        public float PopupHoverDelaySeconds => popupHoverDelaySeconds;

        public bool TryGetIcon(string iconName, out MapIconDefinition iconDefinition)
        {
            if (!string.IsNullOrWhiteSpace(iconName))
            {
                for (var i = 0; i < icons.Count; i++)
                {
                    MapIconDefinition icon = icons[i];
                    if (icon != null && string.Equals(icon.Name, iconName, StringComparison.Ordinal))
                    {
                        iconDefinition = icon;
                        return true;
                    }
                }
            }

            iconDefinition = null;
            return false;
        }
    }
}
