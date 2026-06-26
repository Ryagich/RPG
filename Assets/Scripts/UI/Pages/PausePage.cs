using GameModes;
using Loading;
using MessagePipe;
using Messages;
using UI.Configs;
using UI.UIElements;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Utils;

namespace UI.Pages
{
    public class PausePage : BasePage
    {
        private const string MenuSceneName = "Menu";

        public override PageType Type { get; } = PageType.Pause;

        private readonly UIConfig uiConfig;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;
        private readonly SceneLoadingService sceneLoadingService;

        private RectTransform contentRect;
        private PauseMenu pauseMenu;

        public PausePage(
            UIConfig uiConfig,
            Canvas canvas,
            IObjectResolver resolver,
            IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher,
            SceneLoadingService sceneLoadingService)
        {
            this.uiConfig = uiConfig;
            this.resolver = resolver;
            this.changeGameModeRequestPublisher = changeGameModeRequestPublisher;
            this.sceneLoadingService = sceneLoadingService;

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

            if (pauseMenu.MenuButton != null)
            {
                pauseMenu.MenuButton.onClick.AddListener(LoadMenu);
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

                if (pauseMenu.MenuButton != null)
                {
                    pauseMenu.MenuButton.onClick.RemoveListener(LoadMenu);
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

        private void LoadMenu()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            sceneLoadingService.Load(MenuSceneName);
        }
    }
}
