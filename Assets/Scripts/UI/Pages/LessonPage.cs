using Input;
using Localization;
using Training;
using UI.Configs;
using UI.UIElements;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace UI.Pages
{
    public sealed class LessonPage : BasePage, ITickable, System.IDisposable
    {
        private readonly UIConfig uiConfig;
        private readonly LessonConfig lessonConfig;
        private readonly LessonPresentationContext lessonContext;
        private readonly InputConfig inputConfig;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private LessonPageHolder holder;
        private float elapsedSinceShown;
        private bool isSkipVisible;

        public LessonPage(
            UIConfig uiConfig,
            LessonConfig lessonConfig,
            LessonPresentationContext lessonContext,
            InputConfig inputConfig,
            Canvas canvas,
            IObjectResolver resolver)
        {
            this.uiConfig = uiConfig;
            this.lessonConfig = lessonConfig;
            this.lessonContext = lessonContext;
            this.inputConfig = inputConfig;
            this.resolver = resolver;
            canvasRect = canvas.GetComponent<RectTransform>();

            lessonContext.Changed += Refresh;
        }

        public override PageType Type { get; } = PageType.Lesson;

        public override void Draw()
        {
            if (lessonContext.CurrentLesson == null || uiConfig.LessonPage == null)
            {
                return;
            }

            holder = resolver.Instantiate(uiConfig.LessonPage, canvasRect);
            holder.name = $"{uiConfig.LessonPage.name} | {Type}";
            Refresh();
        }

        public override void Hide()
        {
            if (holder != null)
            {
                Object.Destroy(holder.gameObject);
                holder = null;
            }

            elapsedSinceShown = 0f;
            isSkipVisible = false;
        }

        public void Tick()
        {
            if (holder == null || lessonContext.CurrentLesson == null || isSkipVisible)
            {
                return;
            }

            elapsedSinceShown += Time.unscaledDeltaTime;
            if (elapsedSinceShown < Mathf.Max(0f, lessonConfig.SkipTextShowDelay))
            {
                return;
            }

            isSkipVisible = true;
            holder.SetSkipVisible(lessonContext.CurrentLesson.CanSkipWithLessonInput);
        }

        public void Dispose()
        {
            lessonContext.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (holder == null)
            {
                return;
            }

            var lesson = lessonContext.CurrentLesson;
            if (lesson == null)
            {
                return;
            }

            holder.SetDescription(LessonTextFormatter.Format(
                lesson.Description.GetLocalizedStringCached(),
                inputConfig));
            elapsedSinceShown = 0f;
            isSkipVisible = false;
            holder.SetSkipVisible(false);
        }
    }
}
