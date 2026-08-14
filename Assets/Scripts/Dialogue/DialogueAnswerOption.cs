using System.Collections.Generic;
using Dialogs.Graph.Model;

namespace Dialogue
{
    /// <summary>
    /// A resolved player choice, ready for presentation by the dialogue UI.
    /// </summary>
    public readonly struct DialogueAnswerOption
    {
        public readonly string Text;
        public readonly DialogPhrase NextPhrase;
        public readonly bool ForceExitAfterAnswer;
        public readonly bool ContinueForcedDialogueAfterExit;
        public readonly IReadOnlyList<DialogueGameplayEvent> GameplayEvents;
        public readonly bool HasConditions;
        public readonly IReadOnlyList<DialogAnswerCondition> Conditions;

        public DialogueAnswerOption(
            string text,
            DialogPhrase nextPhrase,
            bool forceExitAfterAnswer,
            bool continueForcedDialogueAfterExit,
            IReadOnlyList<DialogueGameplayEvent> gameplayEvents,
            bool hasConditions,
            IReadOnlyList<DialogAnswerCondition> conditions)
        {
            Text = text ?? string.Empty;
            NextPhrase = nextPhrase;
            ForceExitAfterAnswer = forceExitAfterAnswer && nextPhrase == null;
            ContinueForcedDialogueAfterExit = continueForcedDialogueAfterExit;
            GameplayEvents = gameplayEvents;
            HasConditions = hasConditions;
            Conditions = conditions;
        }
    }
}
