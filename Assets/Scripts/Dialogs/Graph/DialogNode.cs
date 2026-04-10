using Dialogs.Graph.Model;
using UnityEngine;

namespace Dialogs.Graph
{
    [System.Serializable]
    public class DialogNode
    {
        public Vector2 Position;
        public DialogPhrase Phrase;

        public DialogNode(DialogPhrase phrase)
        {
            Phrase = phrase;
        }
    }
}
