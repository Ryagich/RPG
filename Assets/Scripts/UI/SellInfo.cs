using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class SellInfo : MonoBehaviour
    {
        [field: SerializeField] public TMP_Text InfoText { get; private set; }
        [field: SerializeField] public Button TradeButton { get; private set; }
    }
}