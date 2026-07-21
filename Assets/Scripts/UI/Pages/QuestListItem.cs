using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Pages
{
    public sealed class QuestListItem : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text text;

        public Image Icon => icon;
        public TMP_Text Text => text;
    }
}
