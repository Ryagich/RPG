using UnityEngine;
using TMPro;

namespace UI.UIElements
{
    public class InfoAboutInventory : MonoBehaviour
    {
        [field: SerializeField] public TMP_Text Weight { get; private set; }
    }
}