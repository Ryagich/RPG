using System.Collections.Generic;
using Inventory.Item;

namespace Inventory.Inventories
{
    /// <summary>
    /// Exposes items displaced by an inventory operation so the owning character can drop them.
    /// </summary>
    public interface IInventoryOverflow
    {
        IReadOnlyList<ItemStack> ConsumePendingOverflowItems();
    }
}
