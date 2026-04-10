using UnityEngine;
using UnityEngine.Localization;

namespace Dialogs.Graph.Model
{
    [System.Serializable]
    public class DialogAnswer
    {
        [SerializeField] private LocalizedString text = new();
        [SerializeField] private DialogPhrase nextPhrase;

        public LocalizedString Text => text;
        public DialogPhrase NextPhrase => nextPhrase;

        public void SetNextPhrase(DialogPhrase nextPhrase)
        {
            this.nextPhrase = nextPhrase;
        }
    }
}
