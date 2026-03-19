using System;
using System.Linq;
using Interactable;
using TMPro;
using UI.Configs;
using UnityEngine;
using UniRx;
using Object = UnityEngine.Object;

namespace UI
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class InteractableInterface : IDisposable
    {
        private readonly UIConfig uiConfig;
        private readonly RectTransform contentRect;
        private readonly PlayerInteractableLogic playerInteractableLogic;
        private readonly ItemHolderInteractableLogic itemHolderInteractableLogic;
        private readonly CompositeDisposable disposables = new();
        
        private TMP_Text interactableText;
        
        public InteractableInterface
            (
                UIConfig uiConfig,
                RectTransform contentRect,
                PlayerInteractableLogic playerInteractableLogic,
                ItemHolderInteractableLogic itemHolderInteractableLogic
            )
        {
            this.uiConfig = uiConfig;
            this.contentRect = contentRect;
            this.playerInteractableLogic = playerInteractableLogic;
            this.itemHolderInteractableLogic = itemHolderInteractableLogic;

            playerInteractableLogic.Interactables
                                   .ObserveCountChanged()
                                   .Subscribe(_ => Update())
                                   .AddTo(disposables);
            itemHolderInteractableLogic.Items
                                       .ObserveCountChanged()
                                       .Subscribe(_ => Update())
                                       .AddTo(disposables);
        }

        private void Update()
        {
            if (playerInteractableLogic.Interactables.Any(i => i.InteractionMode is InteractionMode.Manual)
             || itemHolderInteractableLogic.Items.Any(i => i.CanInteractable))
            {
                if (!interactableText)
                {
                    interactableText = Object.Instantiate(uiConfig.InteractableText, contentRect);
                }
            }
            else if (interactableText)
            {
                Object.Destroy(interactableText.gameObject);
            }
        }
        
        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}