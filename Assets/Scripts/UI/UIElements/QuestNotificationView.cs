using TMPro;
using UnityEngine;

namespace UI.UIElements
{
    public sealed class QuestNotificationView : MonoBehaviour
    {
        [field: SerializeField] public CanvasGroup CanvasGroup { get; private set; }
        [field: SerializeField] public TMP_Text StateText { get; private set; }
        [field: SerializeField] public TMP_Text QuestNameText { get; private set; }

        private void Awake()
        {
            CanvasGroup ??= GetComponent<CanvasGroup>();
            StateText ??= transform.Find("Content/Objective_Header/HUD_Objective_Header/Content/Label_Objectives")?.GetComponent<TMP_Text>();
            QuestNameText ??= transform.Find("Content/Objective_List/Objective_00/Content/Text/Label_Objective")?.GetComponent<TMP_Text>();
            HideImmediately();
        }

        public void SetContent(string state, string questName)
        {
            if (StateText != null) StateText.text = state ?? string.Empty;
            if (QuestNameText != null) QuestNameText.text = questName ?? string.Empty;
        }

        public void SetAlpha(float alpha)
        {
            // The view prefab can be re-saved by Unity with an empty serialized
            // component reference. Resolve the group on its root as a safe fallback
            // so notification visibility never depends on that serialization detail.
            var canvasGroup = CanvasGroup != null ? CanvasGroup : GetComponent<CanvasGroup>();
            if (canvasGroup == null) return;

            canvasGroup.alpha = alpha;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        public void ResetForShow()
        {
            SetAlpha(0f);
        }

        public void HideImmediately()
        {
            SetAlpha(0f);
        }
    }
}
