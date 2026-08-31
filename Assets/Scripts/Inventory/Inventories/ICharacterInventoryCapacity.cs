namespace Inventory.Inventories
{
    /// <summary>
    /// The physical load currently carried by a character inventory.
    /// </summary>
    public interface ICharacterInventoryCapacity
    {
        float CurrentWeight { get; }
    }
}
