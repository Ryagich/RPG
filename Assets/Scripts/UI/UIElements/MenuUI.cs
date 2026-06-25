using UnityEngine;
using UnityEngine.UI;

namespace UI.UIElements
{
    public sealed class MenuUI : MonoBehaviour
    {
        [field: SerializeField] public Button ToGameButton { get; private set; }
        [field: SerializeField] public Button ToDevelopButton { get; private set; }
    }
}
