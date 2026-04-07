using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.UIElements
{
    public class InfoAboutPlayer : MonoBehaviour
    {
        [field: SerializeField] public Image Photo { get; private set; }
        [field: SerializeField] public TMP_Text Name { get; private set; }
        [field: SerializeField] public TMP_Text Group { get; private set; }
        [field: SerializeField] public TMP_Text Money { get; private set; }
    }
}