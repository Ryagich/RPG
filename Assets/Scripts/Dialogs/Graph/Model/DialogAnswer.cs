using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace Dialogs.Graph.Model
{
    [System.Serializable]
    public class DialogAnswer
    {
        [SerializeField] private LocalizedString text = new();
        [SerializeField] private DialogPhrase nextPhrase;
        [SerializeField] private bool hasConditions;
        [SerializeField] private List<DialogAnswerCondition> conditions = new();

        public LocalizedString Text => text;
        public DialogPhrase NextPhrase => nextPhrase;
        public bool HasConditions => hasConditions;
        public List<DialogAnswerCondition> Conditions => conditions;

        public void SetNextPhrase(DialogPhrase nextPhrase)
        {
            this.nextPhrase = nextPhrase;
        }
    }
}
