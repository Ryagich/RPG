using Interactable;
using UI.Configs;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace UI.Pages
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class MainPage : BasePage
    {
        private readonly UIConfig uiConfig;
        private readonly PlayerInteractableLogic playerInteractableLogic;
        private readonly ItemHolderInteractableLogic itemHolderInteractableLogic;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        public override PageType Type { get; } = PageType.MainGame;

        private RectTransform contentRect;
        private InteractableInterface interactableInterface;
        
        public MainPage
            (
                UIConfig uiConfig,
                Canvas canvas,
                PlayerInteractableLogic playerInteractableLogic,
                ItemHolderInteractableLogic itemHolderInteractableLogic,
                IObjectResolver resolver
            )
        {
            this.resolver = resolver;
            this.uiConfig = uiConfig;
            this.playerInteractableLogic = playerInteractableLogic;
            this.itemHolderInteractableLogic = itemHolderInteractableLogic;

            canvasRect = canvas.GetComponent<RectTransform>();
        }

        public override void Draw()
        {
            contentRect = resolver.Instantiate(uiConfig.ContentPref, canvasRect);
            contentRect.name = $"{uiConfig.ContentPref.name} | {Type}";
            
            interactableInterface = new InteractableInterface(uiConfig, 
                                                              contentRect,
                                                              playerInteractableLogic, 
                                                              itemHolderInteractableLogic);
        }

        public override void Hide()
        {
            if (contentRect)
            {
                Object.Destroy(contentRect.gameObject);
            }
            interactableInterface?.Dispose();
            interactableInterface = null;
        }
    }
}