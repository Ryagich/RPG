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
        private readonly InventoryPage inventoryPage;

        private BasePage currentPage;
        
        public PagesController 
            (
                MainPage mainPage,
                InventoryPage inventoryPage, 
                ISubscriber<GameModeChangedMessage> gameModeChangeSubscriber
            )
        {
            this.mainPage = mainPage;
            this.inventoryPage = inventoryPage;

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
                case GameMode.Inventory:
                    currentPage = inventoryPage;
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
        Inventory,
    }
}