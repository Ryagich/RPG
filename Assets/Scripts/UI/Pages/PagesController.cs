using GameModes;
using MessagePipe;
using Messages;
using UnityEngine;
using VContainer.Unity;

namespace UI.Pages
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class PagesController : IStartable
    {
        private readonly MainPage mainPage;
        private readonly PausePage pausePage;
        private readonly PauseSettingsPage pauseSettingsPage;
        private readonly InventoryPage inventoryPage;
        private readonly LootingPage lootingPage;
        private readonly DialoguePage dialoguePage;
        private readonly TradePage tradePage;
        private readonly MapPage mapPage;
        private readonly QuestPage questPage;
        private readonly LessonPage lessonPage;
        private readonly DeathPage deathPage;
        private readonly SwitchLocationPage switchLocationPage;

        private BasePage currentPage;
        
        public PagesController 
            (
                MainPage mainPage,
                PausePage pausePage,
                PauseSettingsPage pauseSettingsPage,
                InventoryPage inventoryPage,
                LootingPage lootingPage,
                DialoguePage dialoguePage,
                TradePage tradePage,
                MapPage mapPage,
                QuestPage questPage,
                LessonPage lessonPage,
                DeathPage deathPage,
                SwitchLocationPage switchLocationPage,
                ISubscriber<GameModeChangedMessage> gameModeChangeSubscriber
            )
        {
            this.mainPage = mainPage;
            this.pausePage = pausePage;
            this.pauseSettingsPage = pauseSettingsPage;
            this.inventoryPage = inventoryPage;
            this.lootingPage = lootingPage;
            this.dialoguePage = dialoguePage;
            this.tradePage = tradePage;
            this.mapPage = mapPage;
            this.questPage = questPage;
            this.lessonPage = lessonPage;
            this.deathPage = deathPage;
            this.switchLocationPage = switchLocationPage;

            gameModeChangeSubscriber.Subscribe(OnGameModeChanged);
        }
        
        private void OnGameModeChanged(GameModeChangedMessage msg)
        {
            Update(msg.GameMode);
        }
        
        private void Update(GameMode gameMode)
        {
            Debug.Log($"Update Page {gameMode}");
            var nextPage = ResolvePage(gameMode);
            currentPage?.PrepareForTransition(nextPage.Type);
            HideCurrentPage();
            currentPage = nextPage;
            if (currentPage is not null)
            {
                currentPage.Draw();
            }
        }

        private BasePage ResolvePage(GameMode gameMode)
        {
            switch (gameMode)
            {
                case GameMode.Game:
                    return mainPage;
                case GameMode.Pause:
                    return pausePage;
                case GameMode.PauseSettings:
                    return pauseSettingsPage;
                case GameMode.Inventory:
                    return inventoryPage;
                case GameMode.Looting:
                    return lootingPage;
                case GameMode.Dialogue:
                    return dialoguePage;
                case GameMode.Trade:
                    return tradePage;
                case GameMode.Map:
                    return mapPage;
                case GameMode.Quest:
                    return questPage;
                case GameMode.Lesson:
                    return lessonPage;
                case GameMode.Death:
                    return deathPage;
                case GameMode.SwitchLocation:
                    return switchLocationPage;
                default:
                    return mainPage;
            }
        }
        
        private void HideCurrentPage()
        {
            currentPage?.Hide();
            currentPage = null;
        }
        
        public void Start() { }
    }

    public enum PageType
    {
        MainGame,
        Pause,
        PauseSettings,
        Inventory,
        Looting,
        Dialogue,
        Trade,
        Map,
        Quest,
        Lesson,
        Death,
        SwitchLocation,
        MenuMain,
        MenuSettings,
        MenuSoundsSettings,
        MenuGameplaySettings,
        OpeningSlides,
    }
}
