using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Dialogs.Graph;
using Dialogs.Graph.Model;
using Localization;
using UnityEngine;

namespace Dialogue
{
    /// <summary>
    /// Development-only trace of the dialogue lifecycle. Calls to this class are omitted from
    /// non-development player builds, so it cannot affect dialogue behaviour or release performance.
    /// </summary>
    public static class DialogueFlowTrace
    {
        private const string Prefix = "[DialogueTrace]";

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void ContextOpened(
            Interactable.Interactable target,
            DialogGraph dialog,
            DialogPhrase phrase,
            string phraseText,
            bool isForcedDialogue)
        {
            Log($"OPEN mode={(isForcedDialogue ? "forced" : "normal")}; target='{ObjectName(target)}'; " +
                $"dialog='{ObjectName(dialog)}'; {DescribePhrase(phrase, phraseText)}");
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void PhraseChanged(
            DialogPhrase previous,
            string previousPhraseText,
            DialogPhrase current,
            string currentPhraseText,
            bool canExitDialogue)
        {
            Log($"PHRASE previous={DescribePhrase(previous, previousPhraseText)}; " +
                $"current={DescribePhrase(current, currentPhraseText)}; " +
                $"canExit={canExitDialogue}");
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void ForcedPhraseSelected(DialogPhrase phrase, string phraseText)
        {
            Log($"FORCED_SELECTED {DescribePhrase(phrase, phraseText)}; " +
                $"entryConditions={DescribeConditions(phrase?.QuestAnswer?.Conditions)}");
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void NormalDialogueInteraction(string state, Interactable.Interactable target)
        {
            Log($"NORMAL_INTERACTION state={state}; target='{ObjectName(target)}'");
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void AnswerEvaluated(
            DialogPhrase phrase,
            string source,
            string answerText,
            DialogPhrase nextPhrase,
            bool isAvailable,
            bool forceExit,
            IReadOnlyList<DialogAnswerCondition> conditions)
        {
            Log($"ANSWER source={source}; available={isAvailable}; from='{ObjectName(phrase)}'; " +
                $"text=\"{answerText ?? string.Empty}\"; next='{ObjectName(nextPhrase)}'; forceExit={forceExit}; " +
                $"conditions={DescribeConditions(conditions)}");
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void AnswerSelected(
            DialogPhrase phrase,
            string answerText,
            DialogPhrase nextPhrase,
            bool forceExit,
            bool continueForcedDialogueAfterExit,
            IReadOnlyList<DialogAnswerCondition> conditions)
        {
            Log($"ANSWER_SELECTED from='{ObjectName(phrase)}'; text=\"{answerText ?? string.Empty}\"; " +
                $"next='{ObjectName(nextPhrase)}'; forceExit={forceExit}; " +
                $"continueForced={continueForcedDialogueAfterExit}; conditions={DescribeConditions(conditions)}");
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void AnswerRejected(string answerText, IReadOnlyList<DialogAnswerCondition> conditions)
        {
            Log($"ANSWER_REJECTED text=\"{answerText ?? string.Empty}\"; conditions={DescribeConditions(conditions)}");
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void ConditionApplied(DialogAnswerCondition condition)
        {
            Log($"CONDITION_APPLIED {DescribeCondition(condition)}");
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void RuntimeFlagChanged(string operation, DialogueRuntimeFlag flag)
        {
            Log($"RUNTIME_FLAG operation={operation}; flag='{ObjectName(flag)}'");
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void GameplayEventPublished(DialogueGameplayEvent gameplayEvent, string source)
        {
            Log($"GAMEPLAY_EVENT source={source}; event='{ObjectName(gameplayEvent)}'");
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void ExitRequested(bool continueForcedDialogueAfterExit)
        {
            Log($"EXIT_REQUESTED continueForced={continueForcedDialogueAfterExit}");
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void ForcedZoneState(string state, Interactable.Interactable target = null)
        {
            Log($"FORCED_ZONE state={state}; target='{ObjectName(target)}'");
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void ContextCleared(
            Interactable.Interactable target,
            DialogGraph dialog,
            DialogPhrase phrase,
            string phraseText,
            bool isForcedDialogue,
            bool canExitDialogue,
            string reason)
        {
            Log($"CLEAR reason={reason}; mode={(isForcedDialogue ? "forced" : "normal")}; " +
                $"target='{ObjectName(target)}'; dialog='{ObjectName(dialog)}'; {DescribePhrase(phrase, phraseText)}; " +
                $"canExit={canExitDialogue}");
        }

        private static void Log(string message)
        {
            UnityEngine.Debug.Log($"{Prefix} {message}");
        }

        private static string DescribePhrase(DialogPhrase phrase, string resolvedText = null)
        {
            if (phrase == null)
            {
                return "phrase=<none>";
            }

            string phraseText = resolvedText ?? phrase.Text.GetLocalizedStringCached();
            return $"phrase='{phrase.name}' text=\"{phraseText}\" " +
                   $"forced={phrase.IsForcedDialoguePhrase} priority={phrase.ForcedDialoguePriority} " +
                   $"questPhrase={phrase.IsQuestPhrase} conversationTopic={phrase.IsConversationTopic} " +
                   $"restoresExit={phrase.RestoresExitAbility}";
        }

        private static string DescribeConditions(IReadOnlyList<DialogAnswerCondition> conditions)
        {
            if (conditions == null || conditions.Count == 0)
            {
                return "[]";
            }

            var builder = new StringBuilder("[");
            for (var i = 0; i < conditions.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(DescribeCondition(conditions[i]));
            }

            return builder.Append(']').ToString();
        }

        private static string DescribeCondition(DialogAnswerCondition condition)
        {
            if (condition == null)
            {
                return "<null>";
            }

            return condition.Type switch
            {
                DialogAnswerConditionType.GiveMoney or DialogAnswerConditionType.TakeMoney or
                    DialogAnswerConditionType.TakeMoneyMax => $"{condition.Type}(money={condition.MoneyAmount})",
                DialogAnswerConditionType.TakeItemIfHas =>
                    $"{condition.Type}(item={ObjectName(condition.ItemConfig)}, count={condition.ItemCount})",
                DialogAnswerConditionType.AddQuest or DialogAnswerConditionType.CheckQuestStep or
                    DialogAnswerConditionType.DoQuestStep or DialogAnswerConditionType.DoQuestEnd =>
                    $"{condition.Type}(quest={ObjectName(condition.QuestGraph)}, node={ObjectName(condition.QuestNode)}, " +
                    $"sourceNode={ObjectName(condition.QuestSourceNode)}, transition={ObjectName(condition.QuestTransition)})",
                DialogAnswerConditionType.RequireRuntimeFlag or DialogAnswerConditionType.ClearRuntimeFlag or
                    DialogAnswerConditionType.RequireInactiveRuntimeFlag or DialogAnswerConditionType.SetRuntimeFlag =>
                    $"{condition.Type}(flag={ObjectName(condition.RuntimeFlag)})",
                _ => condition.Type.ToString()
            };
        }

        private static string ObjectName(Object value) => value == null ? "<none>" : value.name;
    }
}
