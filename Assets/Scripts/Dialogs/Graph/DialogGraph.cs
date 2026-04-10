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
    }
}
