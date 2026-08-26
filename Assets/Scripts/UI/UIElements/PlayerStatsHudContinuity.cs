using UI.Pages;

namespace UI.UIElements
{
    public sealed class PlayerStatsHudContinuity
    {
        private PlayerStatsHud.State state;
        private PageType sourcePageType;

        internal void Store(PageType source, PlayerStatsHud.State snapshot)
        {
            sourcePageType = source;
            state = snapshot;
        }

        internal PlayerStatsHud.State Consume(PageType destination)
        {
            if (state == null || !AreLinked(sourcePageType, destination))
            {
                Clear();
                return null;
            }

            var result = state;
            Clear();
            return result;
        }

        internal void Clear()
        {
            state = null;
        }

        private static bool AreLinked(PageType source, PageType destination)
        {
            return source == PageType.MainGame && destination == PageType.Dialogue
                || source == PageType.Dialogue && destination == PageType.MainGame;
        }
    }
}
