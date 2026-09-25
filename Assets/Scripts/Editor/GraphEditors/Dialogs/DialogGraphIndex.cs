using System.Collections.Generic;
using Dialogs.Graph.Model;

namespace Dialogs.Graph.Editor
{
    /// <summary>
    /// Maintains derived dialog-graph topology for the editor.  It deliberately owns no
    /// Unity UI state or asset mutations: callers invalidate it after a mutation and use
    /// the index for lookups and diagnostics while rendering.
    /// </summary>
    internal sealed class DialogGraphIndex
    {
        private readonly Dictionary<DialogPhrase, DialogNode> nodesByPhrase = new();
        private readonly HashSet<DialogPhrase> orphanPhrases = new();

        public bool IsDirty { get; private set; } = true;

        public void Rebuild(DialogGraph graph)
        {
            if (!IsDirty)
            {
                return;
            }

            nodesByPhrase.Clear();
            orphanPhrases.Clear();

            if (graph?.Nodes == null)
            {
                IsDirty = false;
                return;
            }

            var reachablePhrases = new HashSet<DialogPhrase>();
            foreach (DialogNode node in graph.Nodes)
            {
                if (node?.Phrase == null)
                {
                    continue;
                }

                nodesByPhrase[node.Phrase] = node;
                foreach (DialogAnswer answer in node.Phrase.Answers)
                {
                    if (answer?.NextPhrase != null)
                    {
                        reachablePhrases.Add(answer.NextPhrase);
                    }
                }
            }

            foreach (DialogNode node in graph.Nodes)
            {
                DialogPhrase phrase = node?.Phrase;
                if (phrase == null || graph.IsEntryPhrase(phrase) || phrase.IsQuestPhrase ||
                    phrase.IsConversationTopic || phrase.IsConversationReturnAction || phrase.IsDialogueExitAction)
                {
                    continue;
                }

                if (!reachablePhrases.Contains(phrase))
                {
                    orphanPhrases.Add(phrase);
                }
            }

            IsDirty = false;
        }

        public void Invalidate()
        {
            nodesByPhrase.Clear();
            orphanPhrases.Clear();
            IsDirty = true;
        }

        public bool TryGetNode(DialogPhrase phrase, out DialogNode node) => nodesByPhrase.TryGetValue(phrase, out node);
        public bool Contains(DialogPhrase phrase) => phrase != null && nodesByPhrase.ContainsKey(phrase);
        public bool IsOrphan(DialogPhrase phrase) => phrase != null && orphanPhrases.Contains(phrase);
    }
}
