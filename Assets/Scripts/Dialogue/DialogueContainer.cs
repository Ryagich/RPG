using UnityEngine;
using UnityEngine.UI;

namespace Dialogue
{
    public class DialogueContainer : MonoBehaviour
    {
        [field: SerializeField] public ScrollRect DialogueScroll { get; private set; }
        [field: SerializeField] public ScrollRect DialogueContent { get; private set; }
        [field: SerializeField] public ScrollRect AnswerScroll { get; private set; }
        [field: SerializeField] public ScrollRect AnswerContent { get; private set; }
        [field: SerializeField] public Button TradeButton { get; private set; }
    }
}