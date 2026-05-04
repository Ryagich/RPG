using UnityEngine;
using UnityEngine.UI;

public class Title : MonoBehaviour
{
    [field: SerializeField] public Button ExitButton { get; private set; }
    [SerializeField] private Button questButton;

    public Button QuestButton
    {
        get
        {
            if (questButton == null)
            {
                questButton = FindButton("Quest Button") ?? FindButton("QuestButton");
            }

            return questButton;
        }
    }

    private Button FindButton(string buttonName)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button != null && button.name == buttonName)
            {
                return button;
            }
        }

        return null;
    }
}
