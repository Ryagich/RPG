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

        public IEnumerable<DialogPhrase> GetQuestPhrases()
        {
            if (Nodes == null)
            {
                yield break;
            }

            foreach (DialogNode node in Nodes)
            {
                DialogPhrase phrase = node?.Phrase;
                if (phrase == null || !phrase.IsQuestPhrase || phrase.IsForcedDialoguePhrase || IsEntryPhrase(phrase))
                {
                    continue;
                }

                yield return phrase;
            }
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
