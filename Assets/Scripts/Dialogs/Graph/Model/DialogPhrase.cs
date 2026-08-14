using System.Collections.Generic;
using Dialogue;
using UnityEngine;
using UnityEngine.Localization;

namespace Dialogs.Graph.Model
{
    [CreateAssetMenu(fileName = "DialogPhrase", menuName = "configs/Dialogs/Phrase")]
    public class DialogPhrase : ScriptableObject
    {
        [SerializeField] private LocalizedString text = new();
        [SerializeField] private List<LocalizedString> alternativeTexts = new();
        [SerializeField] private bool isForcedDialoguePhrase;
        [SerializeField, Min(0)] private int forcedDialoguePriority;
        [SerializeField] private bool restoresExitAbility;
        [SerializeField] private bool isQuestPhrase;
        [SerializeField] private DialogAnswer questAnswer = new();
        [SerializeField] private bool isConversationTopic;
        [SerializeField] private DialogAnswer conversationAnswer = new();
        [SerializeField] private bool isConversationReturnAction;
        [SerializeField] private DialogAnswer conversationReturnAnswer = new();
        [SerializeField] private bool isDialogueExitAction;
        [SerializeField] private DialogAnswer dialogueExitAnswer = new();
        [SerializeField] private bool hasGameplayEvents;
        [SerializeField] private List<DialogueGameplayEvent> gameplayEvents = new();
        [SerializeField] private List<DialogAnswer> answers = new();

        public LocalizedString Text => text;
        public IReadOnlyList<LocalizedString> AlternativeTexts => alternativeTexts;
        public bool IsForcedDialoguePhrase => isForcedDialoguePhrase;
        public int ForcedDialoguePriority => forcedDialoguePriority;
        public bool RestoresExitAbility => restoresExitAbility;
        public bool IsQuestPhrase => isQuestPhrase;
        public DialogAnswer QuestAnswer => questAnswer;
        public bool IsConversationTopic => isConversationTopic;
        public DialogAnswer ConversationAnswer => conversationAnswer;
        public bool IsConversationReturnAction => isConversationReturnAction;
        public DialogAnswer ConversationReturnAnswer => conversationReturnAnswer;
        public bool IsDialogueExitAction => isDialogueExitAction;
        public DialogAnswer DialogueExitAnswer => dialogueExitAnswer;
        public bool HasGameplayEvents => hasGameplayEvents;
        public IReadOnlyList<DialogueGameplayEvent> GameplayEvents => gameplayEvents;
        public List<DialogAnswer> Answers => answers;

        /// <summary>
        /// Chooses the text for one appearance of this phrase. The context keeps the resolved
        /// value for the lifetime of that appearance, so UI history and debug traces cannot
        /// show different variants of the same line.
        /// </summary>
        public LocalizedString GetRandomText()
        {
            int alternativeCount = alternativeTexts?.Count ?? 0;
            return alternativeCount == 0
                ? text
                : Random.Range(0, alternativeCount + 1) == 0
                    ? text
                    : alternativeTexts[Random.Range(0, alternativeCount)];
        }

        public DialogAnswer GetRegularChoiceAnswer()
        {
            return isQuestPhrase
                ? questAnswer
                : isConversationTopic
                    ? conversationAnswer
                    : null;
        }
    }
}
