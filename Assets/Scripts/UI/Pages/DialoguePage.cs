using Dialogue;
using GameModes;
using MessagePipe;
using Messages;
using UI.Configs;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace UI.Pages
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class DialoguePage : BasePage
    {
        public override PageType Type { get; } = PageType.Dialogue;

        private readonly UIConfig uiConfig;
        private readonly DialogueContext dialogueContext;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;

        private RectTransform contentRect;
        private DialogueContainer dialogueContainer;

        public DialoguePage(
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

            dialogueContainer = resolver.Instantiate(uiConfig.DialogueContainer, contentRect);
            dialogueContainer.TradeButton.onClick.AddListener(OpenTradePage);
        }

        public override void Hide()
        {
            if (dialogueContainer)
            {
                dialogueContainer.TradeButton.onClick.RemoveListener(OpenTradePage);
                dialogueContainer = null;
            }

            if (contentRect)
            {
                Object.Destroy(contentRect.gameObject);
            }
        }

        private void OpenTradePage()
        {
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Trade));
        }
    }
}