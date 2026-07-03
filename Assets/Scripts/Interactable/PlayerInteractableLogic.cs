using System.Collections.Generic;
using System.Linq;
using Container;
using GameModes;
using MessagePipe;
using Messages;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

using Combat;

namespace Interactable
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class PlayerInteractableLogic : IFixedTickable
    {
        private readonly InteractableConfig config;
        private readonly PlayerLifetimeScope scope;
        private readonly CharacterActionState actionState;

        private float t;
        private float manualT;
        private GameMode currentGameMode = GameMode.Game;

        // ReSharper disable once IdentifierTypo
        public readonly ReactiveCollection<Interactable> Interactables = new();
        private readonly List<Interactable> nearbyInteractables = new();
        private List<Interactable> activeInteractables = new();
        private readonly CompositeDisposable disposables = new();

        public PlayerInteractableLogic
            (
                InteractableConfig config,
                PlayerLifetimeScope scope,
                CharacterActionState actionState,
                [Key("Scope ID")] string scopeID,
                ISubscriber<string, InteractableMessage> interactableSubscriber,
                ISubscriber<string, InteractableEndMessage> interactableEndSubscriber,
                ISubscriber<InteractableInputMessage> interactableInputSubscriber,
                ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber
            )
        {
            this.scope = scope;
            this.config = config;
            this.actionState = actionState;
            interactableSubscriber.Subscribe(scopeID, Add).AddTo(disposables);
            interactableEndSubscriber.Subscribe(scopeID, Remove).AddTo(disposables);
            interactableInputSubscriber.Subscribe(Interact).AddTo(disposables);
            gameModeChangedSubscriber.Subscribe(OnGameModeChanged).AddTo(disposables);
        }
        
        private void Add(InteractableMessage msg)
        {
            if (msg.Interactable == null || nearbyInteractables.Contains(msg.Interactable))
            {
                return;
            }

            nearbyInteractables.Add(msg.Interactable);
            msg.Interactable.Destroyed += OnDestroyed;
            RefreshAvailableInteractables();
        }

        private void OnDestroyed(Interactable interactable)
        {
            nearbyInteractables.Remove(interactable);
            if (Interactables.Contains(interactable))
            {
                Interactables.Remove(interactable);
            }
            activeInteractables.Remove(interactable);
            if (interactable)
            {
                interactable.Destroyed -= OnDestroyed;
            }
        }

        private void Remove(InteractableEndMessage msg)
        {
            nearbyInteractables.Remove(msg.Interactable);
            Interactables.Remove(msg.Interactable);
            msg.Interactable.Destroyed -= OnDestroyed;
            if (msg.Interactable.InteractionMode is InteractionMode.Manual)
            {
                EndManualInteraction(msg.Interactable);
            }
            else
            {
                msg.Interactable.EndInteract(scope);
            }

            if (nearbyInteractables.Count is 0)
            {
                t = .0f;
            }
        }

        private void Interact(InteractableInputMessage msg)
        {
            RefreshAvailableInteractables();
            if (actionState.IsActionBlocked)
            {
                EndAllManualInteractions();
                return;
            }

            var newActives = new List<Interactable>();
            if (manualT > config.TimeBetweenInteractions)
            {
                foreach (var interactable in Interactables.Where(i => i.InteractionMode is InteractionMode.Manual
                                                                   && IsInteractableAvailable(i)
                                                                   && !activeInteractables.Contains(i)))
                {
                    interactable.Interact(scope);
                    newActives.Add(interactable);
                }
                manualT = .0f;
            }

            var previousActives = activeInteractables;
            activeInteractables = newActives;
            foreach (var interactable in previousActives)
            {
                interactable.EndManualInteract(scope);
            }
        }

        private void EndManualInteraction(Interactable interactable)
        {
            if (!activeInteractables.Remove(interactable))
            {
                return;
            }

            interactable.EndManualInteract(scope);
        }

        private void OnGameModeChanged(GameModeChangedMessage message)
        {
            var previousMode = currentGameMode;
            currentGameMode = message.GameMode;

            if (message.GameMode == GameMode.Game && previousMode != GameMode.Game)
            {
                EndAllManualInteractions();
            }
        }

        private void EndAllManualInteractions()
        {
            if (activeInteractables.Count == 0)
            {
                return;
            }

            var interactablesToEnd = activeInteractables.ToArray();
            activeInteractables.Clear();
            foreach (var interactable in interactablesToEnd)
            {
                interactable.EndManualInteract(scope);
            }
        }

        public void FixedTick()
        {
            RefreshAvailableInteractables();
            if (actionState.IsActionBlocked)
            {
                return;
            }

            if (t > config.TimeBetweenInteractions)
            {
                foreach (var interactable in Interactables.Where(i => i.InteractionMode is InteractionMode.Automatic
                                                                   && IsInteractableAvailable(i)))
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

        private void RefreshAvailableInteractables()
        {
            for (var i = nearbyInteractables.Count - 1; i >= 0; i--)
            {
                if (nearbyInteractables[i] == null)
                {
                    nearbyInteractables.RemoveAt(i);
                }
            }

            var available = nearbyInteractables
                .Where(IsInteractableAvailable)
                .ToList();

            for (var i = Interactables.Count - 1; i >= 0; i--)
            {
                var interactable = Interactables[i];
                if (interactable == null || !available.Contains(interactable))
                {
                    Interactables.RemoveAt(i);
                }
            }

            foreach (var interactable in available)
            {
                if (!Interactables.Contains(interactable))
                {
                    Interactables.Add(interactable);
                }
            }

            foreach (var interactable in activeInteractables.ToArray())
            {
                if (interactable == null || !available.Contains(interactable))
                {
                    EndManualInteraction(interactable);
                }
            }
        }

        private bool IsInteractableAvailable(Interactable interactable)
        {
            if (interactable == null)
            {
                return false;
            }

            var availability = interactable.GetComponent<IInteractableAvailability>();
            return availability == null || availability.IsInteractableAvailable(scope);
        }
    }
}
