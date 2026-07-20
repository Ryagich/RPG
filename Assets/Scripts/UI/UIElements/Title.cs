using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Title : MonoBehaviour
{
    [field: SerializeField] public Button ExitButton { get; private set; }
    [field: SerializeField] public Button LeftButton { get; private set; }
    [field: SerializeField] public Button RightButton { get; private set; }
    [field: SerializeField] public TMP_Text TitleName { get; private set; }
}
