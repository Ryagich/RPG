using UnityEngine;
using UnityEngine.UI;

namespace UI.UIElements
{
    public class PauseMenu : MonoBehaviour
    {
        [field: SerializeField] public Button ContinueButton { get; private set; }
        [field: SerializeField] public Button MenuButton { get; private set; }
    }
}
