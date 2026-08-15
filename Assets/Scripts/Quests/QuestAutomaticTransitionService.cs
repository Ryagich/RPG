using System;
using Inventory.Inventories;
using UniRx;
using VContainer.Unity;

namespace Quests
{
    /// <summary>
    /// Re-evaluates explicitly authored automatic quest transitions when the player's
    /// inventory or quest state changes. QuestController remains the sole owner of progress.
    /// </summary>
    public sealed class QuestAutomaticTransitionService : IStartable, IDisposable
    {
        private readonly PlayerInventory playerInventory;
        private readonly QuestController questController;
        private IDisposable inventorySubscription;
        private bool isEvaluating;

        public QuestAutomaticTransitionService(PlayerInventory playerInventory, QuestController questController)
        {
            this.playerInventory = playerInventory;
            this.questController = questController;
        }

        public void Start()
        {
            inventorySubscription = playerInventory.Changed.Subscribe(_ => EvaluateTransitions());
            questController.Changed += OnQuestChanged;
            EvaluateTransitions();
        }

        public void Dispose()
        {
            inventorySubscription?.Dispose();
            inventorySubscription = null;
            questController.Changed -= OnQuestChanged;
        }

        private void OnQuestChanged(QuestChangeInfo _)
        {
            EvaluateTransitions();
        }

        private void EvaluateTransitions()
        {
            if (isEvaluating)
            {
                return;
            }

            isEvaluating = true;
            try
            {
                questController.TryExecuteAvailableAutomaticTransition();
            }
            finally
            {
                isEvaluating = false;
            }
        }
    }
}
