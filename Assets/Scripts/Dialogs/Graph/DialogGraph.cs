using System;
using System.Collections.Generic;
using Dialogs.Graph.Model;
using UnityEngine;

namespace Dialogs.Graph
{
    [CreateAssetMenu(fileName = "DialogGraph", menuName = "configs/Dialogs/Graph")]
    public class DialogGraph : ScriptableObject
    {
        public List<DialogNode> Nodes = new();

        [field: SerializeField] public DialogPhrase EntryPhrase { get; private set; }

        public void SetEntryPhrase(DialogPhrase phrase)
        {
            EntryPhrase = phrase;
        }

        public bool IsEntryPhrase(DialogPhrase phrase)
        {
            return EntryPhrase == phrase;
        }

        /// <summary>
        /// Determines whether a phrase is a regular conversation choice point. Along with the
        /// graph entry, a phrase that restores exit after a forced line returns the player to
        /// the same choice context: quest branches and authored navigation actions become available.
        /// </summary>
        public bool IsRegularChoicePoint(DialogPhrase phrase)
        {
            return IsEntryPhrase(phrase) || (phrase != null && phrase.RestoresExitAbility);
        }

        public bool TryGetActiveForcedPhrase(
            Func<DialogAnswer, bool> isAnswerAvailable,
            out DialogPhrase forcedPhrase)
        {
            forcedPhrase = null;
            int bestPriority = int.MaxValue;

            if (Nodes != null)
            {
                foreach (DialogNode node in Nodes)
                {
                    DialogPhrase phrase = node?.Phrase;
                    if (phrase == null || !phrase.IsQuestPhrase || !phrase.IsForcedDialoguePhrase)
                    {
                        continue;
                    }

                    DialogAnswer entryAnswer = phrase.QuestAnswer;
                    if (entryAnswer != null && (isAnswerAvailable == null || isAnswerAvailable(entryAnswer)))
                    {
                        // Lower values take precedence. Equal priorities deliberately keep
                        // graph order, so existing dialogs with the default priority retain
                        // their current behaviour.
                        if (forcedPhrase == null || phrase.ForcedDialoguePriority < bestPriority)
                        {
                            forcedPhrase = phrase;
                            bestPriority = phrase.ForcedDialoguePriority;
                        }
                    }
                }
            }

            return forcedPhrase != null;
        }

        public bool CanRestoreExitAbility(DialogPhrase phrase)
        {
            if (phrase == null || phrase.IsForcedDialoguePhrase || Nodes == null)
            {
                return false;
            }

            var visited = new HashSet<DialogPhrase>();
            foreach (DialogNode node in Nodes)
            {
                DialogPhrase forcedPhrase = node?.Phrase;
                if (forcedPhrase == null || !forcedPhrase.IsForcedDialoguePhrase)
                {
                    continue;
                }

                foreach (DialogAnswer answer in forcedPhrase.Answers)
                {
                    if (CanReachBeforeExitIsRestored(answer?.NextPhrase, phrase, visited))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Returns authored branches offered from a regular conversation choice point. Quest
        /// branches and ordinary topics share the same answer lifecycle but retain distinct
        /// authoring data and visual treatment in the editor.
        /// </summary>
        public IEnumerable<DialogPhrase> GetRegularChoicePhrases()
        {
            if (Nodes == null)
            {
                yield break;
            }

            foreach (DialogNode node in Nodes)
            {
                DialogPhrase phrase = node?.Phrase;
                if (phrase == null ||
                    (!phrase.IsQuestPhrase && !phrase.IsConversationTopic) ||
                    phrase.IsConversationReturnAction ||
                    phrase.IsDialogueExitAction ||
                    phrase.IsForcedDialoguePhrase ||
                    IsEntryPhrase(phrase))
                {
                    continue;
                }

                yield return phrase;
            }
        }

        /// <summary>
        /// Returns navigation actions that lead out of a reusable conversation topic branch.
        /// The graph owns both the action and its conditions; the UI only inserts it when the
        /// current phrase is reachable from a conversation topic.
        /// </summary>
        public IEnumerable<DialogPhrase> GetConversationReturnPhrases(DialogPhrase currentPhrase)
        {
            if (!IsConversationBranchPhrase(currentPhrase) || Nodes == null)
            {
                yield break;
            }

            foreach (DialogNode node in Nodes)
            {
                DialogPhrase phrase = node?.Phrase;
                if (phrase == null ||
                    !phrase.IsConversationReturnAction ||
                    phrase.IsDialogueExitAction ||
                    phrase.IsForcedDialoguePhrase ||
                    IsEntryPhrase(phrase))
                {
                    continue;
                }

                yield return phrase;
            }
        }

        /// <summary>
        /// Returns authored dialogue exit actions. Their placement is resolved by the dialogue
        /// application service, which preserves the regular-choice and terminal fallback rules.
        /// </summary>
        public IEnumerable<DialogPhrase> GetDialogueExitPhrases()
        {
            if (Nodes == null)
            {
                yield break;
            }

            foreach (DialogNode node in Nodes)
            {
                DialogPhrase phrase = node?.Phrase;
                if (phrase == null ||
                    !phrase.IsDialogueExitAction ||
                    phrase.IsConversationReturnAction ||
                    phrase.IsForcedDialoguePhrase ||
                    IsEntryPhrase(phrase))
                {
                    continue;
                }

                yield return phrase;
            }
        }

        private bool IsConversationBranchPhrase(DialogPhrase phrase)
        {
            if (phrase == null || Nodes == null)
            {
                return false;
            }

            foreach (DialogNode node in Nodes)
            {
                DialogPhrase topicPhrase = node?.Phrase;
                if (topicPhrase != null &&
                    topicPhrase.IsConversationTopic &&
                    CanReachPhrase(topicPhrase, phrase, new HashSet<DialogPhrase>()))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanReachPhrase(
            DialogPhrase currentPhrase,
            DialogPhrase targetPhrase,
            ISet<DialogPhrase> visited)
        {
            if (currentPhrase == null || !visited.Add(currentPhrase))
            {
                return false;
            }

            if (currentPhrase == targetPhrase)
            {
                return true;
            }

            foreach (DialogAnswer answer in currentPhrase.Answers)
            {
                if (CanReachPhrase(answer?.NextPhrase, targetPhrase, visited))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanReachBeforeExitIsRestored(
            DialogPhrase currentPhrase,
            DialogPhrase targetPhrase,
            ISet<DialogPhrase> visited)
        {
            if (currentPhrase == null || !visited.Add(currentPhrase))
            {
                return false;
            }

            if (currentPhrase == targetPhrase)
            {
                return true;
            }

            if (currentPhrase.RestoresExitAbility)
            {
                return false;
            }

            foreach (DialogAnswer answer in currentPhrase.Answers)
            {
                if (CanReachBeforeExitIsRestored(answer?.NextPhrase, targetPhrase, visited))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
