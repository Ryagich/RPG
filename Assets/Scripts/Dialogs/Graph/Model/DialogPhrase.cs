using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace Dialogs.Graph.Model
{
    [CreateAssetMenu(fileName = "DialogPhrase", menuName = "configs/Dialogs/Phrase")]
    public class DialogPhrase : ScriptableObject
    {
        [SerializeField] private LocalizedString text = new();
        [SerializeField] private bool isForcedDialoguePhrase;
        [SerializeField] private bool restoresExitAbility;
        [SerializeField] private bool isQuestPhrase;
        [SerializeField] private DialogAnswer questAnswer = new();
        [SerializeField] private List<DialogAnswer> answers = new();

        public LocalizedString Text => text;
        public bool IsForcedDialoguePhrase => isForcedDialoguePhrase;
        public bool RestoresExitAbility => restoresExitAbility;
        public bool IsQuestPhrase => isQuestPhrase;
        public DialogAnswer QuestAnswer => questAnswer;
        public List<DialogAnswer> Answers => answers;
    }
}
