using System;

namespace Training
{
    public sealed class LessonPresentationContext
    {
        public LessonDefinition CurrentLesson { get; private set; }
        public bool HasLesson => CurrentLesson != null;

        public event Action Changed;

        public void Show(LessonDefinition lesson)
        {
            CurrentLesson = lesson;
            Changed?.Invoke();
        }

        public void Clear()
        {
            if (CurrentLesson == null)
            {
                return;
            }

            CurrentLesson = null;
            Changed?.Invoke();
        }
    }
}
