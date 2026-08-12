using TMPro;
using UnityEngine;

namespace UI.UIElements
{
    [DisallowMultipleComponent]
    public sealed class LessonPageHolder : MonoBehaviour
    {
        [field: SerializeField] public TMP_Text DescriptionText { get; private set; }
        [field: SerializeField] public TMP_Text SkipText { get; private set; }
        [field: SerializeField] public CanvasGroup SkipCanvasGroup { get; private set; }

        private void Awake()
        {
            if (SkipText != null && SkipCanvasGroup == null)
            {
                SkipCanvasGroup = SkipText.GetComponent<CanvasGroup>();
            }
        }

        public void SetDescription(string description)
        {
            if (DescriptionText != null)
            {
                DescriptionText.text = description ?? string.Empty;
            }
        }

        public void SetSkipVisible(bool isVisible)
        {
            if (SkipCanvasGroup == null)
            {
                return;
            }

            SkipCanvasGroup.alpha = isVisible ? 1f : 0f;
            SkipCanvasGroup.interactable = false;
            SkipCanvasGroup.blocksRaycasts = false;
        }
    }
}
