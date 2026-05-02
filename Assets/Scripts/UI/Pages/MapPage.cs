using GameModes;
using MessagePipe;
using Messages;
using UI.Configs;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace UI.Pages
{
    public class MapPage : BasePage
    {
        public override PageType Type { get; } = PageType.Map;

        private readonly UIConfig uiConfig;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;

        private RectTransform contentRect;
        private Title title;
        private ScrollRect questionsScrollView;

        public MapPage(
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

            title = resolver.Instantiate(uiConfig.Title, contentRect);
            title.name = $"{uiConfig.Title.name} | {Type}";
            title.ExitButton.onClick.AddListener(CloseMap);

            questionsScrollView = resolver.Instantiate(uiConfig.QuestionsScrollView, contentRect);
            questionsScrollView.name = $"{uiConfig.QuestionsScrollView.name} | {Type}";
        }

        public override void Hide()
        {
            if (title != null)
            {
                title.ExitButton.onClick.RemoveListener(CloseMap);
                title = null;
            }

            questionsScrollView = null;

            if (contentRect)
            {
                Object.Destroy(contentRect.gameObject);
            }
        }

        private void CloseMap()
        {
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
        }
    }
}
