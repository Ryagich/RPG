using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Title : MonoBehaviour
{
    [field: SerializeField] public Button ExitButton { get; private set; }
    [field: SerializeField] public Button LeftButton { get; private set; }
    [field: SerializeField] public Button RightButton { get; private set; }
    [field: SerializeField] public TMP_Text TitleName { get; private set; }
    [SerializeField] private Image showCompletedTasksBackground;
    [SerializeField] private TMP_Text showCompletedTasksText;

    public Image ShowCompletedTasksBackground => showCompletedTasksBackground != null
        ? showCompletedTasksBackground
        : showCompletedTasksBackground = GetComponentInChildren<UI.Pages.QuestTaskListItem>(true)?.Background;

    public TMP_Text ShowCompletedTasksText => showCompletedTasksText != null
        ? showCompletedTasksText
        : showCompletedTasksText = GetComponentInChildren<UI.Pages.QuestTaskListItem>(true)?.Text;
}
