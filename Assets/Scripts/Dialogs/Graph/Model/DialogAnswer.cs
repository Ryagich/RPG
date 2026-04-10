using UnityEngine;
using UnityEngine.Localization;

namespace Dialogs.Graph.Model
{
    [System.Serializable]
    public class DialogAnswer
    {
        [field: SerializeField] public LocalizedString Text { get; private set; } = new();
        [field: SerializeField] public DialogPhrase NextPhrase { get; private set; }

        public void SetNextPhrase(DialogPhrase nextPhrase)
        {
            NextPhrase = nextPhrase;
        }
    }
}
