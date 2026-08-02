using UnityEngine;
using UnityEngine.UI;

namespace UI.UIElements
{
    public sealed class SwitchMenuHolder : MonoBehaviour
    {
        [field: SerializeField] public Button YesButton { get; private set; }
        [field: SerializeField] public Button NoButton { get; private set; }
    }
}
