using UnityEngine;
using UnityEngine.UI;

namespace UI.Inventory
{
    public class InventoryView : MonoBehaviour
    {
        [field: SerializeField] public RectTransform ContentForTiles { get; private set; }
        [field: SerializeField] public RectTransform ContentForItems { get; private set; }

        private float minimumContentHeight = -1f;

        public void UpdateScrollableContentHeight(int rowCount)
        {
            var scrollRect = GetComponent<ScrollRect>();
            var gridLayoutGroup = ContentForTiles != null ? ContentForTiles.GetComponent<GridLayoutGroup>() : null;
            if (scrollRect?.content == null || scrollRect.viewport == null || gridLayoutGroup == null || rowCount <= 0)
            {
                return;
            }

            if (minimumContentHeight < 0f)
            {
                minimumContentHeight = Mathf.Max(scrollRect.content.rect.height, scrollRect.viewport.rect.height);
            }

            var requiredGridHeight = gridLayoutGroup.padding.top
                                     + gridLayoutGroup.padding.bottom
                                     + rowCount * gridLayoutGroup.cellSize.y
                                     + Mathf.Max(0, rowCount - 1) * gridLayoutGroup.spacing.y;
            var targetContentHeight = Mathf.Max(minimumContentHeight, requiredGridHeight);
            if (Mathf.Approximately(scrollRect.content.rect.height, targetContentHeight))
            {
                return;
            }

            scrollRect.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetContentHeight);
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
        }
    }
}
