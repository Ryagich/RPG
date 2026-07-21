using System;
using System.Linq;
using Input;
using Interactable;
using TMPro;
using UI.Configs;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using Object = UnityEngine.Object;

namespace UI
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class InteractableInterface : IDisposable
    {
        private readonly UIConfig uiConfig;
        private readonly InputConfig inputConfig;
        private readonly RectTransform contentRect;
        private readonly PlayerInteractableLogic playerInteractableLogic;
        private readonly ItemHolderInteractableLogic itemHolderInteractableLogic;
        private readonly CompositeDisposable disposables = new();
        
        private TMP_Text interactableText;
        private CanvasGroup interactableCanvasGroup;
        private bool isVisible;
        
        public InteractableInterface
            (
                UIConfig uiConfig,
                InputConfig inputConfig,
                RectTransform contentRect,
                PlayerInteractableLogic playerInteractableLogic,
                ItemHolderInteractableLogic itemHolderInteractableLogic
            )
        {
            this.uiConfig = uiConfig;
            this.inputConfig = inputConfig;
            this.contentRect = contentRect;
            this.playerInteractableLogic = playerInteractableLogic;
            this.itemHolderInteractableLogic = itemHolderInteractableLogic;

            CreateInteractableText();

            playerInteractableLogic.Interactables
                                   .ObserveCountChanged()
                                   .Subscribe(_ => Update())
                                   .AddTo(disposables);
            itemHolderInteractableLogic.Items
                                       .ObserveCountChanged()
                                       .Subscribe(_ => Update())
                                       .AddTo(disposables);

            Update();
        }

        private void Update()
        {
            var shouldBeVisible =
                playerInteractableLogic.Interactables.Any(i => i.InteractionMode is InteractionMode.Manual)
             || itemHolderInteractableLogic.Items.Any(i => i.CanInteractable);

            if (shouldBeVisible == isVisible || interactableCanvasGroup == null)
            {
                return;
            }

            isVisible = shouldBeVisible;
            interactableCanvasGroup.alpha = shouldBeVisible ? 1f : 0f;
            interactableCanvasGroup.blocksRaycasts = false;
        }

        private void CreateInteractableText()
        {
            interactableText = Object.Instantiate(uiConfig.InteractableText, contentRect);
            interactableText.raycastTarget = false;
            interactableText.text = $"[{GetInteractableBindingDisplayName()}]";

            // Keep the interaction prompt on a nested canvas so toggling it does not force the whole HUD to rebuild.
            if (!interactableText.TryGetComponent<Canvas>(out _))
            {
                interactableText.gameObject.AddComponent<Canvas>();
            }

            interactableCanvasGroup = interactableText.GetComponent<CanvasGroup>();
            if (interactableCanvasGroup == null)
            {
                interactableCanvasGroup = interactableText.gameObject.AddComponent<CanvasGroup>();
            }

            interactableCanvasGroup.interactable = false;
            interactableCanvasGroup.blocksRaycasts = false;
            interactableCanvasGroup.alpha = 0f;
            isVisible = false;
        }

        private string GetInteractableBindingDisplayName()
        {
            var action = inputConfig?.Interactable?.action;
            if (action == null || action.bindings.Count == 0)
            {
                return string.Empty;
            }

            return InputRebinder.GetLocalizedBindingDisplayName(action.bindings[0]);
        }
        
        public void Dispose()
        {
            disposables.Dispose();

            if (interactableText)
            {
                Object.Destroy(interactableText.gameObject);
                interactableText = null;
                interactableCanvasGroup = null;
            }
        }
    }
}
