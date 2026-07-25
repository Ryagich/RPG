using System.Diagnostics.CodeAnalysis;
using Inventory.Item;
using MessagePipe;
using Messages;
using UnityEngine;
using VContainer;

namespace Interactable
{
    public class InteractableFounder : MonoBehaviour
    {
        private InteractableConfig config;
        private string scopeID;
        private IPublisher<string, InteractableMessage> interactablePublisher;
        private IPublisher<string, InteractableEndMessage> interactableEndPublisher;
        private IPublisher<string, ItemHolderFoundMessage> itemHolderFoundMessagePublisher;
        private IPublisher<string, ItemHolderLostMessage> itemHolderLostMessagePublisher;

        [Inject]
        [SuppressMessage("ReSharper", "ParameterHidesMember")]
        public void Construct
            (
                InteractableConfig config,
                [Key("Scope ID")] string scopeID,
                IPublisher<string, InteractableMessage> interactablePublisher,
                IPublisher<string, InteractableEndMessage> interactableEndPublisher,
                IPublisher<string, ItemHolderFoundMessage> itemHolderFoundMessagePublisher,
                IPublisher<string, ItemHolderLostMessage> itemHolderLostMessagePublisher
            )
        {
            this.config = config;
            this.scopeID = scopeID;
            this.interactablePublisher = interactablePublisher;
            this.interactableEndPublisher = interactableEndPublisher;
            this.itemHolderFoundMessagePublisher = itemHolderFoundMessagePublisher;
            this.itemHolderLostMessagePublisher = itemHolderLostMessagePublisher;
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (((1 << other.gameObject.layer) & config.InteractiveLayers) != 0)
            {
                var interactable = other.GetComponentInParent<Interactable>();
                if (interactable)
                {
                    interactablePublisher.Publish(scopeID, new InteractableMessage(interactable));
                    return;
                }
                var itemHolder = other.GetComponentInParent<ItemHolder>();
                if (itemHolder && itemHolder.CanInteractable)
                {
                    itemHolderFoundMessagePublisher.Publish(scopeID, new ItemHolderFoundMessage(itemHolder));
                }
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (((1 << other.gameObject.layer) & config.InteractiveLayers) == 0)
            {
                return;
            }

            var interactable = other.GetComponentInParent<Interactable>();
            if (interactable)
            {
                interactablePublisher.Publish(scopeID, new InteractableMessage(interactable));
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (((1 << other.gameObject.layer) & config.InteractiveLayers) != 0)
            {
                var interactable = other.GetComponentInParent<Interactable>();
                if (interactable)
                {
                    interactableEndPublisher.Publish(scopeID, new InteractableEndMessage(interactable));
                    return;
                }
                var itemHolder = other.GetComponentInParent<ItemHolder>();
                if (itemHolder && itemHolder.CanInteractable)
                {
                    itemHolderLostMessagePublisher.Publish(scopeID, new ItemHolderLostMessage(itemHolder));
                }
            }
        }
    }
}
