using System.Collections.Generic;
using System.Linq;
using Dialogs.Graph;
using Dialogs.Graph.Model;
using EditorTools;
using UnityEditor;

namespace Dialogs.Graph.Editor
{
    /// <summary>Read-only validation of dialogue entry, authored links and reachability.</summary>
    internal static class DialogGraphValidator
    {
        public static IReadOnlyList<GraphEditorValidationIssue> Validate(DialogGraph graph)
        {
            var issues = new List<GraphEditorValidationIssue>();
            if (graph?.Nodes == null || graph.Nodes.Count == 0)
            {
                issues.Add(Error("Graph has no entry phrase."));
                return issues;
            }

            var phrases = new HashSet<DialogPhrase>();
            foreach (DialogNode node in graph.Nodes)
            {
                if (node?.Phrase == null || !AssetDatabase.Contains(node.Phrase))
                    issues.Add(Error("Graph contains a node with a missing DialogPhrase asset."));
                else if (!phrases.Add(node.Phrase))
                    issues.Add(Error($"Phrase \"{node.Phrase.name}\" is assigned more than once."));
            }

            if (graph.EntryPhrase == null || !phrases.Contains(graph.EntryPhrase))
            {
                issues.Add(Error("Entry phrase is missing or is outside this graph."));
                return issues;
            }

            foreach (DialogPhrase phrase in phrases)
                foreach (DialogAnswer answer in phrase.GetAuthoredAnswers())
                    if (answer.NextPhrase != null && !phrases.Contains(answer.NextPhrase))
                        issues.Add(Error($"Phrase \"{phrase.name}\" links to a phrase outside this graph."));

            var reachable = new HashSet<DialogPhrase> { graph.EntryPhrase };
            var pending = new Queue<DialogPhrase>();
            pending.Enqueue(graph.EntryPhrase);
            while (pending.Count > 0)
                foreach (DialogAnswer answer in pending.Dequeue().GetAuthoredAnswers())
                    if (answer.NextPhrase != null && phrases.Contains(answer.NextPhrase) && reachable.Add(answer.NextPhrase))
                        pending.Enqueue(answer.NextPhrase);

            foreach (DialogPhrase phrase in phrases.Where(phrase => !reachable.Contains(phrase)))
                issues.Add(new GraphEditorValidationIssue(GraphEditorValidationSeverity.Warning, $"Phrase \"{phrase.name}\" is unreachable from the entry phrase."));
            return issues;
        }

        private static GraphEditorValidationIssue Error(string message) => new(GraphEditorValidationSeverity.Error, message);
    }
}
