
using Dialogue;
using GameModes;
 using Inventory.Slot;
 using MessagePipe;
using Messages;
using UI.Configs;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
 using VContainer.Unity;

 namespace UI.Pages
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class TradePage : BasePage
    {
        public override PageType Type { get; } = PageType.Trade;

        private readonly UIConfig uiConfig;
        private readonly DialogueContext dialogueContext;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;

        private RectTransform contentRect;
        private RectTransform leftRect;
        private RectTransform rightRect;
        private SlotsViewContainer centerSection;
        private Button tradingExitButton;

        public TradePage(
            UIConfig uiConfig,
            DialogueContext dialogueContext,
            Canvas canvas,
            IObjectResolver resolver,
            IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher)
        {
            this.uiConfig = uiConfig;
            this.dialogueContext = dialogueContext;
            this.resolver = resolver;
            this.changeGameModeRequestPublisher = changeGameModeRequestPublisher;

            canvasRect = canvas.GetComponent<RectTransform>();
        }

        public override void Draw()
        {
            if (dialogueContext.CurrentTarget == null)
            {
                changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
                return;
            }

            contentRect = resolver.Instantiate(uiConfig.ContentPref, canvasRect);
            contentRect.name = $"{uiConfig.ContentPref.name} | {Type}";

            leftRect = resolver.Instantiate(uiConfig.LeftSection, contentRect);
            centerSection = resolver.Instantiate(uiConfig.CenterSection, contentRect);
            rightRect = resolver.Instantiate(uiConfig.RightSection, contentRect);

            resolver.Instantiate(uiConfig.InfoAboutPlayer, leftRect);
            resolver.Instantiate(uiConfig.InfoAboutInventory, leftRect);
            resolver.Instantiate(uiConfig.InventoryInTrading, leftRect);

            resolver.Instantiate(uiConfig.InfoAboutPlayer, rightRect);
            resolver.Instantiate(uiConfig.InfoAboutInventory, rightRect);
            resolver.Instantiate(uiConfig.SellInfo, rightRect);
            resolver.Instantiate(uiConfig.SellInventory, rightRect);

            tradingExitButton = resolver.Instantiate(uiConfig.TradingExitButton, centerSection.transform);
            tradingExitButton.onClick.AddListener(ReturnToDialogue);
        }

        public override void Hide()
        {
            if (tradingExitButton)
            {
                tradingExitButton.onClick.RemoveListener(ReturnToDialogue);
                tradingExitButton = null;
            }

            if (contentRect)
            {
                Object.Destroy(contentRect.gameObject);
            }
        }

        private void ReturnToDialogue()
        {
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Dialogue));
        }
    }
}