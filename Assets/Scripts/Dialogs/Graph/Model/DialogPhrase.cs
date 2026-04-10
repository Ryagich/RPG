using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace Dialogs.Graph.Model
{
    [CreateAssetMenu(fileName = "DialogPhrase", menuName = "configs/Dialogs/Phrase")]
    public class DialogPhrase : ScriptableObject
    {
        [SerializeField] private LocalizedString text = new();
        [SerializeField] private List<DialogAnswer> answers = new();

        public LocalizedString Text => text;
        public List<DialogAnswer> Answers => answers;
    }
}
