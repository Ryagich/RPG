using GameModes;
using Localization;
using MessagePipe;
using Messages;
using UI.Configs;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace UI.Pages
{
    public sealed class QuestPage : BasePage
    {
        public override PageType Type { get; } = PageType.Quest;

        private readonly UIConfig uiConfig;
        private readonly LocalizationConfig localizationConfig;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;

        private RectTransform pageRect;
        private Title title;

        public QuestPage(
            UIConfig uiConfig,
            LocalizationConfig localizationConfig,
            Canvas canvas,
            IObjectResolver resolver,
            IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher)
        {
            this.uiConfig = uiConfig;
            this.localizationConfig = localizationConfig;
            this.resolver = resolver;
            this.changeGameModeRequestPublisher = changeGameModeRequestPublisher;
            canvasRect = canvas.GetComponent<RectTransform>();
        }

        public override void Draw()
        {
            if (uiConfig.QuestPage == null)
            {
                Debug.LogError("Quest Page prefab is not assigned in UIConfig.");
                changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
                return;
            }

            pageRect = resolver.Instantiate(uiConfig.QuestPage, canvasRect);
            pageRect.name = $"{uiConfig.QuestPage.name} | {Type}";
            ConfigureUnscaledAnimators(pageRect.gameObject);

            title = pageRect.GetComponentInChildren<Title>(true);
            if (title == null)
            {
                Debug.LogError("Quest Page prefab does not contain a Title component.");
                return;
            }

            if (title.TitleName != null)
            {
                title.TitleName.text = localizationConfig.QuestsTitle.GetLocalizedStringCached();
            }

            title.ExitButton?.onClick.AddListener(Close);
            title.LeftButton?.onClick.AddListener(OpenMap);
            title.RightButton?.onClick.AddListener(OpenMap);
        }

        public override void Hide()
        {
            if (title != null)
            {
                title.ExitButton?.onClick.RemoveListener(Close);
                title.LeftButton?.onClick.RemoveListener(OpenMap);
                title.RightButton?.onClick.RemoveListener(OpenMap);
                title = null;
            }

            if (pageRect != null)
            {
                Object.Destroy(pageRect.gameObject);
                pageRect = null;
            }
        }

        private void Close()
        {
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
        }

        private void OpenMap()
        {
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Map));
        }

        private static void ConfigureUnscaledAnimators(GameObject root)
        {
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
            {
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }
        }
    }
}
