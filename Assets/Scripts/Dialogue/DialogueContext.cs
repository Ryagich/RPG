using Character;
using Dialogs.Graph;
using Dialogs.Graph.Model;
using Factions;
using Interactable;
using Inventory.Inventories;
using Money;
using Quests;

namespace Dialogue
{
    public class DialogueContext
    {
        public Interactable.Interactable CurrentTarget { get; private set; }
        public CharacterInfo CurrentTargetCharacterInfo { get; private set; }
        public FactionConfig CurrentTargetFaction { get; private set; }
        public DialogGraph CurrentDialog { get; private set; }
        public DialogPhrase CurrentPhrase { get; private set; }
        public IInventory CurrentTargetInventory { get; private set; }
        public MoneyStorage CurrentTargetMoneyStorage { get; private set; }
        public QuestController PlayerQuestController { get; private set; }
        public bool IsForcedDialogue { get; private set; }
        public bool CanExitDialogue { get; private set; } = true;
        public bool ContinueForcedDialogueAfterExit { get; private set; } = true;

        public void SetTarget(
            Interactable.Interactable target,
            CharacterInfo characterInfo = null,
            DialogGraph dialog = null,
            IInventory inventory = null,
            MoneyStorage moneyStorage = null,
            DialogPhrase initialPhrase = null,
            bool isForcedDialogue = false,
            FactionConfig faction = null)
        {
            CurrentTarget = target;
            CurrentTargetCharacterInfo = characterInfo;
            CurrentTargetFaction = faction;
            CurrentDialog = dialog;
            CurrentPhrase = initialPhrase ?? dialog?.EntryPhrase;
            CurrentTargetInventory = inventory;
            CurrentTargetMoneyStorage = moneyStorage;
            IsForcedDialogue = isForcedDialogue;
            CanExitDialogue = !isForcedDialogue;
            DialogueFlowTrace.ContextOpened(CurrentTarget, CurrentDialog, CurrentPhrase, IsForcedDialogue);
        }

        public void SetPlayerQuestController(QuestController questController)
        {
            PlayerQuestController = questController;
        }

        public void SetCurrentPhrase(DialogPhrase phrase)
        {
            DialogPhrase previousPhrase = CurrentPhrase;
            CurrentPhrase = phrase;
            if (phrase != null && phrase.RestoresExitAbility)
            {
                CanExitDialogue = true;
            }

            DialogueFlowTrace.PhraseChanged(previousPhrase, CurrentPhrase, CanExitDialogue);
        }

        public bool TryForceExit(bool continueForcedDialogueAfterExit)
        {
            if (CurrentTarget == null)
            {
                return false;
            }

            CanExitDialogue = true;
            ContinueForcedDialogueAfterExit = continueForcedDialogueAfterExit;
            DialogueFlowTrace.ExitRequested(continueForcedDialogueAfterExit);
            return true;
        }

        public void Clear()
        {
            DialogueFlowTrace.ContextCleared(
                CurrentTarget,
                CurrentDialog,
                CurrentPhrase,
                IsForcedDialogue,
                CanExitDialogue,
                "DialogueContext.Clear");
            CurrentTarget = null;
            CurrentTargetCharacterInfo = null;
            CurrentTargetFaction = null;
            CurrentDialog = null;
            CurrentPhrase = null;
            CurrentTargetInventory = null;
            CurrentTargetMoneyStorage = null;
            IsForcedDialogue = false;
            CanExitDialogue = true;
            ContinueForcedDialogueAfterExit = true;
        }
    }
}
