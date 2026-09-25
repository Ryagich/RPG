using System.Collections.Generic;
using Dialogs.Graph.Model;

namespace Dialogs.Graph.Editor
{
    internal sealed class DialogExitAbilityCache
    {
        private readonly Dictionary<DialogPhrase, bool> values = new();

        public bool Get(DialogGraph graph, DialogPhrase phrase)
        {
            if (graph == null || phrase == null) return false;
            if (!values.TryGetValue(phrase, out bool value))
            {
                value = graph.CanRestoreExitAbility(phrase);
                values[phrase] = value;
            }
            return value;
        }

        public void Clear() => values.Clear();
    }
}
