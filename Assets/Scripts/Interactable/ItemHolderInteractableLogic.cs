using System.Linq;
using Inventory;
using Inventory.Item;
using MessagePipe;
using Messages;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Interactable
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class ItemHolderInteractableLogic : IStartable              
    {
        private Transform transform;
        private readonly IInventory inventory;

        public ReactiveCollection<ItemHolder> Items = new();
        private readonly CompositeDisposable disposables = new();

        public ItemHolderInteractableLogic
            (
                [Key("Scope ID")] string scopeID,
                Transform transform,
                IInventory inventory,
                ISubscriber<string, ItemHolderFoundMessage> itemHolderFoundSubscriber,
                ISubscriber<string, ItemHolderLostMessage> itemHolderLostSubscriber,
                ISubscriber<InteractableInputMessage> interactableInputSubscriber
            )
        {
            this.transform = transform;
            this.inventory = inventory;

            itemHolderFoundSubscriber.Subscribe(scopeID, Add).AddTo(disposables);
            itemHolderLostSubscriber.Subscribe(scopeID, Remove).AddTo(disposables);
            interactableInputSubscriber.Subscribe(Interact).AddTo(disposables);
        }
        
        private void Add(ItemHolderFoundMessage msg)
        {
            Items.Add(msg.ItemHolder);
            msg.ItemHolder.Destroyed += OnDestroyed;
        }
        
        private void OnDestroyed(ItemHolder itemHolder)
        {
            if (Items.Contains(itemHolder))
            {
                Items.Remove(itemHolder);
            }
            if (itemHolder)
            {
                itemHolder.Destroyed -= OnDestroyed;
            }
        }
        
        private void Remove(ItemHolderLostMessage msg)
        {
            Items.Remove(msg.ItemHolder);
            msg.ItemHolder.Destroyed -= OnDestroyed;
        }

        private void Interact(InteractableInputMessage msg)
        {
            var closestItem = Items
                             .Where(i => i.CanInteractable)
                             .OrderBy(i => Vector3.Distance(transform.position, i.transform.position))
                             .FirstOrDefault();
            if (closestItem && inventory.TryAdd(closestItem.Config))
            {
                Object.Destroy(closestItem.gameObject);
            }
        }

        public void Start() { } 
    }
}