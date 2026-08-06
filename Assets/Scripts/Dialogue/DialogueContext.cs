using Character;
using Dialogs.Graph;
using Dialogs.Graph.Model;
using Interactable;
using Inventory.Inventories;
using Money;

namespace Dialogue
{
    public class DialogueContext
    {
        public Interactable.Interactable CurrentTarget { get; private set; }
        public CharacterInfo CurrentTargetCharacterInfo { get; private set; }
        public DialogGraph CurrentDialog { get; private set; }
        public DialogPhrase CurrentPhrase { get; private set; }
        public IInventory CurrentTargetInventory { get; private set; }
        public MoneyStorage CurrentTargetMoneyStorage { get; private set; }
        public bool IsForcedDialogue { get; private set; }
        public bool CanExitDialogue { get; private set; } = true;

        public void SetTarget(
            Interactable.Interactable target,
            CharacterInfo characterInfo = null,
            DialogGraph dialog = null,
            IInventory inventory = null,
            MoneyStorage moneyStorage = null,
            DialogPhrase initialPhrase = null,
            bool isForcedDialogue = false)
        {
            CurrentTarget = target;
            CurrentTargetCharacterInfo = characterInfo;
            CurrentDialog = dialog;
            CurrentPhrase = initialPhrase ?? dialog?.EntryPhrase;
            CurrentTargetInventory = inventory;
            CurrentTargetMoneyStorage = moneyStorage;
            IsForcedDialogue = isForcedDialogue;
            CanExitDialogue = !isForcedDialogue;
        }

        public void SetCurrentPhrase(DialogPhrase phrase)
        {
            CurrentPhrase = phrase;
            if (phrase != null && phrase.RestoresExitAbility)
            {
                CanExitDialogue = true;
            }
        }

        public void Clear()
        {
            CurrentTarget = null;
            CurrentTargetCharacterInfo = null;
            CurrentDialog = null;
            CurrentPhrase = null;
            CurrentTargetInventory = null;
            CurrentTargetMoneyStorage = null;
            IsForcedDialogue = false;
            CanExitDialogue = true;
        }
    }
}
