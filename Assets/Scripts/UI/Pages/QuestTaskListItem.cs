using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Pages
{
    public sealed class QuestTaskListItem : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text text;

        public Image Background => background;
        public TMP_Text Text => text;
    }
}
