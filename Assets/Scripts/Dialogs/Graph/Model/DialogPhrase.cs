using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace Dialogs.Graph.Model
{
    [CreateAssetMenu(fileName = "DialogPhrase", menuName = "configs/Dialogs/Phrase")]
    public class DialogPhrase : ScriptableObject
    {
        [field: SerializeField] public LocalizedString Text { get; private set; } = new();
        [field: SerializeField] public List<DialogAnswer> Answers { get; private set; } = new();
    }
}
