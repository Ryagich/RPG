using System;
using System.Collections.Generic;
using Inventory.Grid;
using Inventory.Inventories;
using Inventory.Item;
using UI.Pages;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Inventory
{
    /// <summary>
    /// Presentation state for one tiled inventory. It owns only generated grid UI and coordinate
    /// conversion; item rendering and gameplay commands remain with the caller.
    /// </summary>
    public sealed class InventoryGridView
    {
        private readonly IInventory inventory;
        private readonly InventoryView view;
        private Vector2Int renderedGridSize = new(-1, -1);

        public InventoryGridView(IInventory inventory, InventoryView view)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public IInventory Inventory => inventory;
        public InventoryView View => view;
        public bool NeedsTileRefresh => inventory is ITiledInventory tiledInventory
                                        && new Vector2Int(tiledInventory.Tiles.tiles.GetLength(0), tiledInventory.Tiles.tiles.GetLength(1)) != renderedGridSize;

        public void ClearItems()
        {
            PageUiUtilities.ClearChildren(view.ContentForItems);
        }

        public void UpdateScrollHeight()
        {
            if (inventory is ITiledInventory tiledInventory)
            {
                view.UpdateScrollableContentHeight(tiledInventory.Tiles.tiles.GetLength(1));
            }
        }

        public bool TryGetPlacementTile(Camera eventCamera, Vector2 screenPoint, ItemStack stack, Vector2 grabOffset, out Tile tile)
        {
            tile = null;
            return inventory is ITiledInventory tiledInventory
                   && InventoryGridGeometry.TryGetPlacementCell(view.ContentForTiles, eventCamera, screenPoint, stack, grabOffset, out var cell)
                   && tiledInventory.Tiles.TryGetTile(cell.x, cell.y, out tile);
        }

        public bool TryGetSnappedLocalPosition(Camera eventCamera, Vector2 screenPoint, ItemStack stack, Vector2 grabOffset, out Vector3 position)
        {
            position = default;
            return inventory is ITiledInventory tiledInventory
                   && InventoryGridGeometry.TryGetSnappedLocalPosition(view.ContentForTiles, eventCamera, screenPoint, tiledInventory.Tiles, stack, grabOffset, out position);
        }

        public void EnsureTiles(Action<RectTransform, Tile> createTile)
        {
            if (inventory is not ITiledInventory tiledInventory || createTile == null)
            {
                return;
            }

            var tiles = tiledInventory.Tiles.tiles;
            var size = new Vector2Int(tiles.GetLength(0), tiles.GetLength(1));
            if (size == renderedGridSize)
            {
                return;
            }

            PageUiUtilities.ClearChildren(view.ContentForTiles);
            foreach (var tile in tiles)
            {
                createTile(view.ContentForTiles, tile);
            }

            renderedGridSize = size;
        }

        public void InvalidateTiles()
        {
            renderedGridSize = new Vector2Int(-1, -1);
        }
    }

    /// <summary>
    /// Owns the shared presentation lifecycle of several tiled inventory grids: their views,
    /// scroll locking, tile invalidation and item rendering. Pages supply only page-specific
    /// callbacks for created tiles and items.
    /// </summary>
    public sealed class InventoryGridCollectionView
    {
        private readonly Dictionary<IInventory, InventoryGridView> gridViews = new();
        private readonly List<ScrollRect> scrollRects = new();

        public void Configure(IEnumerable<KeyValuePair<IInventory, InventoryView>> views)
        {
            gridViews.Clear();
            scrollRects.Clear();

            if (views == null)
            {
                return;
            }

            foreach (var pair in views)
            {
                if (pair.Key == null || pair.Value == null)
                {
                    continue;
                }

                var gridView = new InventoryGridView(pair.Key, pair.Value);
                gridViews[pair.Key] = gridView;
                var scrollRect = pair.Value.GetComponent<ScrollRect>() ?? pair.Value.GetComponentInParent<ScrollRect>();
                if (scrollRect != null && !scrollRects.Contains(scrollRect))
                {
                    scrollRects.Add(scrollRect);
                }
            }
        }

        public void Clear()
        {
            gridViews.Clear();
            scrollRects.Clear();
        }

        public bool TryGetGridView(IInventory inventory, out InventoryGridView gridView)
        {
            return gridViews.TryGetValue(inventory, out gridView);
        }

        public bool TryGetInventoryView(IInventory inventory, out InventoryView inventoryView)
        {
            inventoryView = null;
            if (!TryGetGridView(inventory, out var gridView))
            {
                return false;
            }

            inventoryView = gridView.View;
            return true;
        }

        public bool TryGetInventoryAt(Camera eventCamera, Vector2 screenPoint, out IInventory inventory)
        {
            foreach (var gridView in gridViews.Values)
            {
                var gridRect = gridView.View.ContentForTiles;
                var scrollRect = gridView.View.GetComponent<ScrollRect>() ?? gridView.View.GetComponentInParent<ScrollRect>();
                if (gridRect != null
                    && (scrollRect?.viewport == null || RectTransformUtility.RectangleContainsScreenPoint(scrollRect.viewport, screenPoint, eventCamera))
                    && RectTransformUtility.RectangleContainsScreenPoint(gridRect, screenPoint, eventCamera))
                {
                    inventory = gridView.Inventory;
                    return true;
                }
            }

            inventory = null;
            return false;
        }

        public bool HasGridSizeChanged()
        {
            foreach (var gridView in gridViews.Values)
            {
                if (gridView.NeedsTileRefresh)
                {
                    return true;
                }
            }

            return false;
        }

        public void EnsureTiles(Action<IInventory, RectTransform, Tile> createTile)
        {
            if (createTile == null)
            {
                return;
            }

            foreach (var gridView in gridViews.Values)
            {
                var inventory = gridView.Inventory;
                gridView.EnsureTiles((parent, tile) => createTile(inventory, parent, tile));
                gridView.UpdateScrollHeight();
            }
        }

        public void SetScrollEnabled(bool enabled)
        {
            foreach (var scrollRect in scrollRects)
            {
                if (scrollRect != null)
                {
                    scrollRect.enabled = enabled;
                }
            }
        }

        public void ClearItems()
        {
            foreach (var gridView in gridViews.Values)
            {
                gridView.ClearItems();
            }
        }

        public void RenderItems(InventoryItemsView itemsView, Action<IInventory, RectTransform, ItemInInventory> onItemRendered)
        {
            if (itemsView == null)
            {
                return;
            }

            foreach (var gridView in gridViews.Values)
            {
                var inventory = gridView.Inventory;
                itemsView.Render(inventory, gridView.View, (itemRect, item) => onItemRendered?.Invoke(inventory, itemRect, item));
            }
        }
    }
}
