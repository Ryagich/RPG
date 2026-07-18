using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.UIElements
{
    public sealed class QuestDescriptionHolder : MonoBehaviour
    {
        [field: SerializeField] public TMP_Text Title { get; private set; }
        [field: SerializeField] public TMP_Text Description { get; private set; }
        [field: SerializeField] public CanvasGroup CanvasGroup { get; private set; }

        private RectTransform rootRect;
        private float heightWithoutDescription;
        private bool isLayoutInitialized;

        private void Awake()
        {
            BindRuntimeTextReferences();
            CanvasGroup ??= GetComponent<CanvasGroup>();
            rootRect = transform as RectTransform;
            SetAlpha(0f);
        }

        public void SetContent(string title, string description)
        {
            if (Title != null)
            {
                Title.text = title ?? string.Empty;
            }

            if (Description != null)
            {
                Description.text = description ?? string.Empty;
            }

            ResizeToDescription();
        }

        public void SetAlpha(float alpha)
        {
            var canvasGroup = CanvasGroup != null ? CanvasGroup : GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = Mathf.Clamp01(alpha);
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void ResizeToDescription()
        {
            if (Description == null || !TryInitializeLayout())
            {
                return;
            }

            Canvas.ForceUpdateCanvases();

            var descriptionRect = Description.rectTransform;
            var availableWidth = descriptionRect.rect.width;
            if (availableWidth <= 0f)
            {
                return;
            }

            var descriptionHeight = string.IsNullOrWhiteSpace(Description.text)
                ? 0f
                : Mathf.Ceil(Description.GetPreferredValues(Description.text, availableWidth, 0f).y);

            rootRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                heightWithoutDescription + descriptionHeight);
            descriptionRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, descriptionHeight);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
        }

        private bool TryInitializeLayout()
        {
            rootRect ??= transform as RectTransform;
            if (rootRect == null || Description == null)
            {
                return false;
            }

            if (isLayoutInitialized)
            {
                return true;
            }

            Canvas.ForceUpdateCanvases();
            heightWithoutDescription = Mathf.Max(0f, rootRect.rect.height - Description.rectTransform.rect.height);
            isLayoutInitialized = true;
            return true;
        }

        private void BindRuntimeTextReferences()
        {
            if (Title == null || !Title.transform.IsChildOf(transform))
            {
                Title = transform.Find("Header Text")?.GetComponent<TMP_Text>();
            }

            if (Description == null || !Description.transform.IsChildOf(transform))
            {
                Description = transform.Find("Description Text")?.GetComponent<TMP_Text>();
            }
        }
    }
}
