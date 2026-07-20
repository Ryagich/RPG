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
        private readonly DeathPage deathPage;

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
                DeathPage deathPage,
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
            this.deathPage = deathPage;

            gameModeChangeSubscriber.Subscribe(OnGameModeChanged);
        }
        
        private void OnGameModeChanged(GameModeChangedMessage msg)
        {
            Update(msg.GameMode);
        }
        
        private void Update(GameMode gameMode)
        {
            Debug.Log($"Update Page {gameMode}");
            HideCurrentPage();
            switch (gameMode)
            {
                case GameMode.Game:
                    currentPage = mainPage;
                    break;
                case GameMode.Pause:
                    currentPage = pausePage;
                    break;
                case GameMode.PauseSettings:
                    currentPage = pauseSettingsPage;
                    break;
                case GameMode.Inventory:
                    currentPage = inventoryPage;
                    break;
                case GameMode.Looting:
                    currentPage = lootingPage;
                    break;
                case GameMode.Dialogue:
                    currentPage = dialoguePage;
                    break;
                case GameMode.Trade:
                    currentPage = tradePage;
                    break;
                case GameMode.Map:
                    currentPage = mapPage;
                    break;
                case GameMode.Quest:
                    currentPage = questPage;
                    break;
                case GameMode.Death:
                    currentPage = deathPage;
                    break;
                default:
                    currentPage = mainPage;
                    break;
            }
            if (currentPage is not null)
                currentPage.Draw();
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
        Death,
        MenuMain,
        MenuSettings,
        MenuSoundsSettings,
        MenuGameplaySettings,
    }
}
