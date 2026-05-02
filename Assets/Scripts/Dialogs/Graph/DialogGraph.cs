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

        public IEnumerable<DialogPhrase> GetQuestPhrases()
        {
            if (Nodes == null)
            {
                yield break;
            }

            foreach (DialogNode node in Nodes)
            {
                DialogPhrase phrase = node?.Phrase;
                if (phrase == null || !phrase.IsQuestPhrase || IsEntryPhrase(phrase))
                {
                    continue;
                }

                yield return phrase;
            }
        }
    }
}
