using System.Collections.Generic;
using Dialogue;
using UnityEngine;
using UnityEngine.Localization;

namespace Dialogs.Graph.Model
{
    [System.Serializable]
    public class DialogAnswer
    {
        [SerializeField] private LocalizedString text = new();
        [SerializeField] private DialogPhrase nextPhrase;
        [SerializeField] private bool forceExitAfterAnswer;
        [SerializeField] private bool continueForcedDialogueAfterExit = true;
        [SerializeField] private bool hasGameplayEvents;
        [SerializeField] private List<DialogueGameplayEvent> gameplayEvents = new();
        [SerializeField] private bool hasConditions;
        [SerializeField] private List<DialogAnswerCondition> conditions = new();

        public LocalizedString Text => text;
        public DialogPhrase NextPhrase => nextPhrase;
        public bool ForceExitAfterAnswer => forceExitAfterAnswer && nextPhrase == null;
        public bool ContinueForcedDialogueAfterExit => continueForcedDialogueAfterExit;
        public bool HasGameplayEvents => hasGameplayEvents;
        public IReadOnlyList<DialogueGameplayEvent> GameplayEvents => gameplayEvents;
        public bool HasConditions => hasConditions;
        public List<DialogAnswerCondition> Conditions => conditions;

        public void SetNextPhrase(DialogPhrase nextPhrase)
        {
            this.nextPhrase = nextPhrase;
            if (nextPhrase != null)
            {
                forceExitAfterAnswer = false;
            }
        }
    }
}
