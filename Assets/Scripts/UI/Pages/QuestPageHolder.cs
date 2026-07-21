using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Pages
{
    public sealed class QuestPageHolder : MonoBehaviour
    {
        [SerializeField] private Title title;
        [SerializeField] private ScrollRect questionsScroll;
        [SerializeField] private ScrollRect tasksScroll;
        [SerializeField] private ScrollRect descriptionScroll;
        [SerializeField] private TMP_Text descriptionText;

        public Title Title => title != null ? title : title = GetComponentInChildren<Title>(true);

        public ScrollRect QuestionsScroll => questionsScroll != null ? questionsScroll : GetScrollRect(0);
        public ScrollRect TasksScroll => tasksScroll != null ? tasksScroll : GetScrollRect(1);
        public ScrollRect DescriptionScroll => descriptionScroll != null ? descriptionScroll : GetScrollRect(2);

        public TMP_Text DescriptionText => descriptionText;

        private ScrollRect GetScrollRect(int index)
        {
            ScrollRect[] scrollRects = GetComponentsInChildren<ScrollRect>(true);
            if (scrollRects.Length <= index)
            {
                return null;
            }

            return index switch
            {
                0 => questionsScroll = scrollRects[index],
                1 => tasksScroll = scrollRects[index],
                2 => descriptionScroll = scrollRects[index],
                _ => null
            };
        }
    }
}
