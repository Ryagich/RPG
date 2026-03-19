using GameModes;

namespace UI.Pages
{
    public abstract class BasePage
    {
        public abstract PageType Type { get; }

        public abstract void Draw();

        public abstract void Hide();
    }
}