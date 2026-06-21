using GameModes;
using MessagePipe;
using Messages;
using UI.Configs;
using UI.UIElements;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace UI.Pages
{
    public class PausePage : BasePage
    {
        public override PageType Type { get; } = PageType.Pause;

        private readonly UIConfig uiConfig;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;

        private RectTransform contentRect;
        private PauseMenu pauseMenu;

        public PausePage(
            UIConfig uiConfig,
            Canvas canvas,
            IObjectResolver resolver,
            IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher)
        {
            this.uiConfig = uiConfig;
            this.resolver = resolver;
            this.changeGameModeRequestPublisher = changeGameModeRequestPublisher;

            canvasRect = canvas.GetComponent<RectTransform>();
        }

        public override void Draw()
        {
            contentRect = resolver.Instantiate(uiConfig.ContentPref, canvasRect);
            contentRect.name = $"{uiConfig.ContentPref.name} | {Type}";

            if (uiConfig.PauseMenu == null)
            {
                return;
            }

            pauseMenu = resolver.Instantiate(uiConfig.PauseMenu, contentRect);
            pauseMenu.name = $"{uiConfig.PauseMenu.name} | {Type}";

            if (pauseMenu.ContinueButton != null)
            {
                pauseMenu.ContinueButton.onClick.AddListener(ContinueGame);
            }
        }

        public override void Hide()
        {
            if (pauseMenu != null)
            {
                if (pauseMenu.ContinueButton != null)
                {
                    pauseMenu.ContinueButton.onClick.RemoveListener(ContinueGame);
                }

                pauseMenu = null;
            }

            if (contentRect)
            {
                Object.Destroy(contentRect.gameObject);
            }
        }

        private void ContinueGame()
        {
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
        }
    }
}
