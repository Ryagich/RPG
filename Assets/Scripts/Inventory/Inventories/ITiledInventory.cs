using Inventory.Grid;

namespace Inventory.Inventories
{
    public interface ITiledInventory : IInventory
    {
        public Tiles Tiles { get; }
    }
}