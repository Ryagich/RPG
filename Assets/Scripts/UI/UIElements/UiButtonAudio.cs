using GameAudio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI.UIElements
{
    /// <summary>Lightweight pointer adapter shared by every settings/menu button prefab.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class UiButtonAudio : UIBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        private Button button;

        protected override void Awake()
        {
            base.Awake();
            button = GetComponent<Button>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button != null && button.IsInteractable())
            {
                AudioService.Current?.PlayUiHover();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (button != null && button.IsInteractable())
            {
                AudioService.Current?.PlayUiClick();
            }
        }
    }
}
