using Inventory.Item;

namespace Inventory
{
    /// <summary>Reports a new world drop without coupling inventory to persistence.</summary>
    public interface IWorldItemDropObserver
    {
        void RegisterRuntimeDrop(ItemHolder itemHolder);
    }
}
