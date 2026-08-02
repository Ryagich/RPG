using GameAudio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

namespace UI.UIElements
{
    /// <summary>Lightweight pointer adapter shared by every settings/menu button prefab.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class UiButtonAudio : UIBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        private Button button;
        private IAudioService audioService;

        [Inject]
        public void Construct(IAudioService service)
        {
            audioService = service;
        }

        protected override void Awake()
        {
            base.Awake();
            button = GetComponent<Button>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button != null && button.IsInteractable())
            {
                audioService?.PlayUiHover();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (button != null && button.IsInteractable())
            {
                audioService?.PlayUiClick();
            }
        }
    }
}
