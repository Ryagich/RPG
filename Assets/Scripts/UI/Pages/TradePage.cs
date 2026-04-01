using Dialogue;
using GameModes;
using Inventory.Slot;
using Localization;
using MessagePipe;
using Messages;
using UI.Configs;
using UI.UIElements;
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
        private readonly Character.CharacterInfo playerCharacterInfo;
        private readonly DialogueContext dialogueContext;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;

        private RectTransform contentRect;
        private RectTransform leftRect;
        private RectTransform rightRect;
        private SlotsViewContainer centerSection;
        private Button tradingExitButton;

        public TradePage
            (
                UIConfig uiConfig,
                Character.CharacterInfo playerCharacterInfo,
                DialogueContext dialogueContext,
                Canvas canvas,
                IObjectResolver resolver,
                IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher
            )
        {
            this.uiConfig = uiConfig;
            this.playerCharacterInfo = playerCharacterInfo;
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

            var leftInfoAboutPlayer = resolver.Instantiate(uiConfig.InfoAboutPlayer, leftRect);
            resolver.Instantiate(uiConfig.InfoAboutInventory, leftRect);
            resolver.Instantiate(uiConfig.SellInfo, leftRect);
            resolver.Instantiate(uiConfig.SellInventory, leftRect);
            FillInfoAboutPlayer(leftInfoAboutPlayer, dialogueContext.CurrentTargetCharacterInfo);

            var rightInfoAboutPlayer = resolver.Instantiate(uiConfig.InfoAboutPlayer, rightRect);
            resolver.Instantiate(uiConfig.InfoAboutInventory, rightRect);
            resolver.Instantiate(uiConfig.SellInfo, rightRect);
            resolver.Instantiate(uiConfig.SellInventory, rightRect);
            FillInfoAboutPlayer(rightInfoAboutPlayer, playerCharacterInfo);

            tradingExitButton = resolver.Instantiate(uiConfig.TradingExitButton, centerSection.transform);
            tradingExitButton.onClick.AddListener(ReturnToDialogue);
        }

        private static void FillInfoAboutPlayer(InfoAboutPlayer infoAboutPlayer, Character.CharacterInfo currentCharacterInfo)
        {
            if (infoAboutPlayer == null || currentCharacterInfo == null)
            {
                return;
            }

            infoAboutPlayer.Photo.sprite = currentCharacterInfo.Photo;
            infoAboutPlayer.Name.text = currentCharacterInfo.Name.GetLocalizedStringCached();
            infoAboutPlayer.Group.text = currentCharacterInfo.Fraction.GetLocalizedStringCached();
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