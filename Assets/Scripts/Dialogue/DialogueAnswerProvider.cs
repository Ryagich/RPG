using System.Collections.Generic;
using Dialogs.Graph;
using Dialogs.Graph.Model;
using Inventory.Inventories;
using Localization;
using Money;
using Quests;

namespace Dialogue
{
    /// <summary>
    /// Resolves the choices offered by the active dialogue. It owns navigation-action placement
    /// and availability checks; presentation receives only resolved choices.
    /// </summary>
    public sealed class DialogueAnswerProvider
    {
        private readonly DialogueContext dialogueContext;
        private readonly PlayerInventory playerInventory;
        private readonly MoneyStorage playerMoneyStorage;
        private readonly QuestController questController;
        private readonly DialogueRuntimeFlagRegistry runtimeFlags;
        private readonly LocalizationConfig localizationConfig;

        public DialogueAnswerProvider(
            DialogueContext dialogueContext,
            PlayerInventory playerInventory,
            MoneyStorage playerMoneyStorage,
            QuestController questController,
            DialogueRuntimeFlagRegistry runtimeFlags,
            LocalizationConfig localizationConfig)
        {
            this.dialogueContext = dialogueContext;
            this.playerInventory = playerInventory;
            this.playerMoneyStorage = playerMoneyStorage;
            this.questController = questController;
            this.runtimeFlags = runtimeFlags;
            this.localizationConfig = localizationConfig;
        }

        public IReadOnlyList<DialogueAnswerOption> GetVisibleAnswers(DialogPhrase phrase)
        {
            var visibleAnswers = new List<DialogueAnswerOption>();
            if (phrase == null)
            {
                return visibleAnswers;
            }

            AddPhraseAnswers(visibleAnswers, phrase);

            DialogGraph currentDialog = dialogueContext.CurrentDialog;
            bool isRegularChoicePoint = currentDialog != null && currentDialog.IsRegularChoicePoint(phrase);
            if (isRegularChoicePoint)
            {
                AddRegularChoiceAnswers(visibleAnswers, phrase, currentDialog);
            }

            bool hasStandardAnswers = visibleAnswers.Count != 0;
            AddConversationReturnAnswers(visibleAnswers, phrase, currentDialog);
            AddExitAnswers(visibleAnswers, phrase, currentDialog, isRegularChoicePoint, hasStandardAnswers);
            return visibleAnswers;
        }

        private void AddPhraseAnswers(List<DialogueAnswerOption> visibleAnswers, DialogPhrase phrase)
        {
            foreach (DialogAnswer answer in phrase.Answers)
            {
                AddAnswerIfAvailable(visibleAnswers, phrase, "phrase-answer", answer, answer?.NextPhrase);
            }
        }

        private void AddRegularChoiceAnswers(
            List<DialogueAnswerOption> visibleAnswers,
            DialogPhrase sourcePhrase,
            DialogGraph currentDialog)
        {
            foreach (DialogPhrase regularChoicePhrase in currentDialog.GetRegularChoicePhrases())
            {
                DialogAnswer choiceAnswer = regularChoicePhrase?.GetRegularChoiceAnswer();
                AddAnswerIfAvailable(
                    visibleAnswers,
                    sourcePhrase,
                    $"regular-choice-answer:{regularChoicePhrase?.name}",
                    choiceAnswer,
                    regularChoicePhrase);
            }
        }

        private void AddConversationReturnAnswers(
            List<DialogueAnswerOption> visibleAnswers,
            DialogPhrase sourcePhrase,
            DialogGraph currentDialog)
        {
            if (currentDialog == null)
            {
                return;
            }

            foreach (DialogPhrase returnPhrase in currentDialog.GetConversationReturnPhrases(sourcePhrase))
            {
                AddAnswerIfAvailable(
                    visibleAnswers,
                    sourcePhrase,
                    $"conversation-return-answer:{returnPhrase.name}",
                    returnPhrase.ConversationReturnAnswer,
                    returnPhrase.ConversationReturnAnswer?.NextPhrase);
            }
        }

        private void AddExitAnswers(
            List<DialogueAnswerOption> visibleAnswers,
            DialogPhrase sourcePhrase,
            DialogGraph currentDialog,
            bool isRegularChoicePoint,
            bool hasStandardAnswers)
        {
            if (!dialogueContext.CanExitDialogue || (!isRegularChoicePoint && hasStandardAnswers))
            {
                return;
            }

            bool hasAuthoredExitAction = false;
            if (currentDialog != null)
            {
                foreach (DialogPhrase exitPhrase in currentDialog.GetDialogueExitPhrases())
                {
                    hasAuthoredExitAction = true;
                    AddAnswerIfAvailable(
                        visibleAnswers,
                        sourcePhrase,
                        $"dialogue-exit-answer:{exitPhrase.name}",
                        exitPhrase.DialogueExitAnswer,
                        exitPhrase.DialogueExitAnswer?.NextPhrase);
                }
            }

            if (!hasAuthoredExitAction)
            {
                string farewellText = localizationConfig.DialogueFarewell.GetLocalizedStringCached();
                DialogueFlowTrace.AnswerEvaluated(sourcePhrase, "legacy-system-farewell", farewellText, null, true, true, null);
                visibleAnswers.Add(new DialogueAnswerOption(farewellText, null, true, true, null, false, null));
            }
        }

        private void AddAnswerIfAvailable(
            List<DialogueAnswerOption> visibleAnswers,
            DialogPhrase sourcePhrase,
            string source,
            DialogAnswer answer,
            DialogPhrase nextPhrase)
        {
            if (answer == null)
            {
                return;
            }

            bool isAvailable = DialogueAnswerAvailability.AreConditionsSatisfied(
                answer.HasConditions,
                answer.Conditions,
                playerInventory,
                playerMoneyStorage,
                questController,
                runtimeFlags);
            string text = answer.Text.GetLocalizedStringCached();
            DialogueFlowTrace.AnswerEvaluated(
                sourcePhrase,
                source,
                text,
                nextPhrase,
                isAvailable,
                answer.ForceExitAfterAnswer,
                answer.Conditions);

            if (!isAvailable || ContainsEquivalentAnswer(visibleAnswers, text, nextPhrase, answer.ForceExitAfterAnswer))
            {
                return;
            }

            visibleAnswers.Add(new DialogueAnswerOption(
                text,
                nextPhrase,
                answer.ForceExitAfterAnswer,
                answer.ContinueForcedDialogueAfterExit,
                answer.GameplayEvents,
                answer.HasConditions,
                answer.Conditions));
        }

        private static bool ContainsEquivalentAnswer(
            List<DialogueAnswerOption> answers,
            string text,
            DialogPhrase nextPhrase,
            bool forceExitAfterAnswer)
        {
            foreach (DialogueAnswerOption answer in answers)
            {
                if (answer.Text == text &&
                    answer.NextPhrase == nextPhrase &&
                    answer.ForceExitAfterAnswer == forceExitAfterAnswer)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
