using Inventory.Item;

namespace Messages
{
    public readonly struct InteractableMessage
    {
        public readonly Interactable.Interactable Interactable;

        public InteractableMessage(Interactable.Interactable interactable)
        {
            Interactable = interactable;
        }
    }

    public readonly struct InteractableEndMessage
    {
        public readonly Interactable.Interactable Interactable;

        public InteractableEndMessage(Interactable.Interactable interactable)
        {
            Interactable = interactable;
        }
    }

    /// <summary>
    /// Signals that the player has been relocated. Interaction sensing clears its previous
    /// zone state; Unity then repopulates it through the normal trigger lifecycle.
    /// </summary>
    public readonly struct PlayerRelocatedMessage
    {
    }
    
    public readonly struct ItemHolderFoundMessage
    {
        public readonly ItemHolder ItemHolder;

        public ItemHolderFoundMessage(ItemHolder itemHolder)
        {
            ItemHolder = itemHolder;
        }
    }
    
    public readonly struct ItemHolderLostMessage
    {
        public readonly ItemHolder ItemHolder;

        public ItemHolderLostMessage(ItemHolder itemHolder)
        {
            ItemHolder = itemHolder;
        }
    }
}
