using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Stats
{
    public class StatHolderForUsable : MonoBehaviour
    {
        [field: SerializeField] public Image Icon { get; private set; }
        [field: SerializeField] public TMP_Text Name { get; private set; }
        [field: SerializeField] public TMP_Text Amount { get; private set; }
    }
}