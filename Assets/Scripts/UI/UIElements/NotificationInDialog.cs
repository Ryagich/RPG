using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.UIElements
{
    public class NotificationInDialog : MonoBehaviour
    {
        [field: SerializeField] public TMP_Text Name { get; private set; }
        [field: SerializeField] public TMP_Text Phrase { get; private set; }
        [field: SerializeField] public Image Icon { get; private set; }
    }
}