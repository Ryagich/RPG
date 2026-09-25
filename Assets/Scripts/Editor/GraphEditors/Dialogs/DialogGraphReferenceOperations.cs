using System.Collections.Generic;
using Dialogs.Graph.Model;

namespace Dialogs.Graph.Editor
{
    /// <summary>
    /// Maintains authored phrase links when a dialog graph node is removed or replaced.
    /// This class owns no window state, layout cache, or AssetDatabase lifecycle.
    /// </summary>
    internal static class DialogGraphReferenceOperations
    {
        public static IReadOnlyList<DialogPhrase> RemoveIncomingReferences(DialogGraph graph, DialogPhrase target)
        {
            return UpdateIncomingReferences(graph, target, null);
        }

        public static IReadOnlyList<DialogPhrase> ReplaceIncomingReferences(
            DialogGraph graph,
            DialogPhrase oldPhrase,
            DialogPhrase newPhrase)
        {
            return UpdateIncomingReferences(graph, oldPhrase, newPhrase);
        }

        private static IReadOnlyList<DialogPhrase> UpdateIncomingReferences(
            DialogGraph graph,
            DialogPhrase oldPhrase,
            DialogPhrase newPhrase)
        {
            var changedPhrases = new List<DialogPhrase>();
            if (graph == null || oldPhrase == null || oldPhrase == newPhrase)
            {
                return changedPhrases;
            }

            if (graph.IsEntryPhrase(oldPhrase))
            {
                graph.SetEntryPhrase(newPhrase);
            }

            if (graph.Nodes == null)
            {
                return changedPhrases;
            }

            foreach (DialogNode node in graph.Nodes)
            {
                DialogPhrase phrase = node?.Phrase;
                if (phrase == null)
                {
                    continue;
                }

                bool changed = false;
                foreach (DialogAnswer answer in phrase.GetAuthoredAnswers())
                {
                    if (answer.NextPhrase == oldPhrase)
                    {
                        answer.SetNextPhrase(newPhrase);
                        changed = true;
                    }
                }

                if (changed)
                {
                    changedPhrases.Add(phrase);
                }
            }

            return changedPhrases;
        }
    }
}
