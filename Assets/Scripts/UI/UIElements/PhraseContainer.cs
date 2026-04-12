using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.UIElements
{
    public class PhraseContainer : MonoBehaviour
    {
        [field: SerializeField] public TMP_Text Name { get; private set; }
        [field: SerializeField] public TMP_Text Phrase { get; private set; }

        private RectTransform rectTransform;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
        }

        public void SetContent(string characterName, string phraseText)
        {
            Name.text = characterName;
            Phrase.text = phraseText;

            RebuildLayoutHierarchy();
            RefreshLayout();
        }

        public void RefreshLayout()
        {
            RebuildLayoutHierarchy();

            var totalHeight = ResizeText(Name) + ResizeText(Phrase);

            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalHeight);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }

        private static float ResizeText(TMP_Text text)
        {
            if (text == null)
            {
                return 0f;
            }

            var textRect = (RectTransform)text.transform;
            var width = ResolveWidth(textRect);

            text.ForceMeshUpdate();

            var preferredHeight = text.GetPreferredValues(text.text, width, float.PositiveInfinity).y;
            textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferredHeight);
            LayoutRebuilder.ForceRebuildLayoutImmediate(textRect);

            return preferredHeight;
        }

        private void RebuildLayoutHierarchy()
        {
            Canvas.ForceUpdateCanvases();

            for (Transform current = transform; current != null; current = current.parent)
            {
                if (current is RectTransform currentRect)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(currentRect);
                }
            }
        }

        private static float ResolveWidth(RectTransform textRect)
        {
            for (Transform current = textRect; current != null; current = current.parent)
            {
                if (current is not RectTransform currentRect)
                {
                    continue;
                }

                var width = currentRect.rect.width;
                if (current == textRect)
                {
                    width = Mathf.Max(width, 0f);
                }

                if (width > 0f)
                {
                    return current == textRect
                        ? width
                        : Mathf.Max(width + textRect.sizeDelta.x, 0f);
                }
            }

            return 1f;
        }
    }
}
