using System.Collections.Generic;
using System.Linq;
using Container;
using MessagePipe;
using Messages;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Interactable
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class PlayerInteractableLogic : IFixedTickable
    {
        private readonly InteractableConfig config;
        private readonly PlayerLifetimeScope scope;

        private float t;
        private float manualT;

        // ReSharper disable once IdentifierTypo
        public readonly ReactiveCollection<Interactable> Interactables = new();
        private List<Interactable> activeInteractables = new();
        private readonly CompositeDisposable disposables = new();

        public PlayerInteractableLogic
            (
                InteractableConfig config,
                PlayerLifetimeScope scope,
                [Key("Scope ID")] string scopeID,
                ISubscriber<string, InteractableMessage> interactableSubscriber,
                ISubscriber<string, InteractableEndMessage> interactableEndSubscriber,
                ISubscriber<InteractableInputMessage> interactableInputSubscriber
            )
        {
            this.scope = scope;
            this.config = config;
            interactableSubscriber.Subscribe(scopeID, Add).AddTo(disposables);
            interactableEndSubscriber.Subscribe(scopeID, Remove).AddTo(disposables);
            interactableInputSubscriber.Subscribe(Interact).AddTo(disposables);
        }
        
        private void Add(InteractableMessage msg)
        {
            Interactables.Add(msg.Interactable);
            msg.Interactable.Destroyed += OnDestroyed;
        }

        private void OnDestroyed(Interactable interactable)
        {
            if (Interactables.Contains(interactable))
            {
                Interactables.Remove(interactable);
            }
            if (interactable)
            {
                interactable.Destroyed -= OnDestroyed;
            }
        }

        private void InteractableEndedInteract(Interactable interactable)
        {
            activeInteractables.Remove(interactable);
            interactable.ObjectEndManualInteracted -= InteractableEndedInteract;
        }

        private void Remove(InteractableEndMessage msg)
        {
            Debug.Log($"Interactable Remove");
            Interactables.Remove(msg.Interactable);
            msg.Interactable.Destroyed -= OnDestroyed;
            msg.Interactable.EndInteract(scope);
            if (Interactables.Count is 0)
            {
                t = .0f;
            }
        }

        private void Interact(InteractableInputMessage msg)
        {
            var newActives = new List<Interactable>();
            if (manualT > config.TimeBetweenInteractions)
            {
                foreach (var interactable in Interactables.Where(i => i.InteractionMode is InteractionMode.Manual
                                                                   && !activeInteractables.Contains(i)))
                {
                    interactable.Interact(scope);
                    interactable.ObjectEndManualInteracted += InteractableEndedInteract;
                    newActives.Add(interactable);
                }
                manualT = .0f;
            }
            foreach (var interactable in activeInteractables)
            {
                interactable.EndManualInteract(scope);
            }
            activeInteractables = newActives;
        }

        public void FixedTick()
        {
            if (t > config.TimeBetweenInteractions)
            {
                foreach (var interactable in Interactables.Where(i => i.InteractionMode is InteractionMode.Automatic))
                {
                    interactable.Interact(scope);
                }
                t = .0f;
            }
            if (Interactables.Count > 0)
            {
                t += Time.fixedDeltaTime;
                manualT += Time.fixedDeltaTime;
            }
        }
    }
}