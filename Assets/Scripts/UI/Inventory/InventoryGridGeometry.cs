using Inventory.Grid;
using Inventory.Item;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Inventory
{
    /// <summary>
    /// Presentation-only coordinate conversion shared by tiled inventory pages.
    /// </summary>
    public static class InventoryGridGeometry
    {
        public static bool TryGetPlacementCell(RectTransform gridRect, Camera eventCamera, Vector2 screenPoint, ItemStack handItemStack, Vector2 handGrabOffset, out Vector2Int placementCell)
        {
            placementCell = default;
            if (!TryGetCursorCell(gridRect, eventCamera, screenPoint, out var cursorCell, out var layout)
                || handItemStack?.ItemConfig == null)
            {
                return false;
            }

            var stepX = layout.cellSize.x + layout.spacing.x;
            var stepY = layout.cellSize.y + layout.spacing.y;
            var size = GetItemGrabSize(layout, handItemStack.Size);
            var offsetX = Mathf.Clamp(Mathf.FloorToInt((handGrabOffset.x + size.x * .5f) / stepX), 0, handItemStack.Size.x - 1);
            var offsetY = Mathf.Clamp(Mathf.FloorToInt((size.y * .5f - handGrabOffset.y) / stepY), 0, handItemStack.Size.y - 1);
            placementCell = new Vector2Int(cursorCell.x - offsetX, cursorCell.y - offsetY);
            return true;
        }

        public static bool TryGetSnappedLocalPosition(RectTransform gridRect, Camera eventCamera, Vector2 screenPoint, Tiles tiles, ItemStack stack, Vector2 grabOffset, out Vector3 position)
        {
            position = Vector3.zero;
            if (tiles == null
                || !TryGetPlacementCell(gridRect, eventCamera, screenPoint, stack, grabOffset, out var cell)
                || !TryGetLayout(gridRect, out var layout)
                || cell.x < 0 || cell.y < 0
                || cell.x + stack.Size.x > tiles.tiles.GetLength(0)
                || cell.y + stack.Size.y > tiles.tiles.GetLength(1))
            {
                return false;
            }

            position = new Vector3(layout.padding.left + (cell.x + stack.Size.x * .5f) * layout.cellSize.x + (cell.x + (stack.Size.x - 1) * .5f) * layout.spacing.x, -(layout.padding.top + (cell.y + stack.Size.y * .5f) * layout.cellSize.y + (cell.y + (stack.Size.y - 1) * .5f) * layout.spacing.y), 0f);
            return true;
        }

        private static bool TryGetCursorCell(RectTransform rect, Camera camera, Vector2 point, out Vector2Int cell, out GridLayoutGroup layout)
        {
            cell = default;
            layout = rect ? rect.GetComponent<GridLayoutGroup>() : null;
            if (!rect || layout == null
                || !RectTransformUtility.RectangleContainsScreenPoint(rect, point, camera)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, point, camera, out var local))
            {
                return false;
            }

            var xStep = layout.cellSize.x + layout.spacing.x;
            var yStep = layout.cellSize.y + layout.spacing.y;
            if (xStep <= 0f || yStep <= 0f)
            {
                return false;
            }
            var bounds = rect.rect;
            cell = new Vector2Int(Mathf.FloorToInt((local.x + bounds.width * rect.pivot.x - layout.padding.left) / xStep), Mathf.FloorToInt((bounds.height * (1f - rect.pivot.y) - local.y - layout.padding.top) / yStep));
            return true;
        }

        private static bool TryGetLayout(RectTransform rect, out GridLayoutGroup layout)
        {
            layout = rect ? rect.GetComponent<GridLayoutGroup>() : null;
            return layout != null;
        }

        private static Vector2 GetItemGrabSize(GridLayoutGroup layout, Vector2Int size) => new(
            size.x * layout.cellSize.x + (size.x - 1) * layout.spacing.x,
            size.y * layout.cellSize.y + (size.y - 1) * layout.spacing.y);
    }
}
