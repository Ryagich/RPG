using MessagePipe;
using Messages;
using VContainer.Unity;

namespace GameModes
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class GameModesController : IStartable
    {
        public GameMode GameMode { get; private set; } = GameMode.Game;
      
        private readonly IPublisher<GameModeChangedMessage> gameModeChangedPublisher;

        public GameModesController
            (
                IPublisher<GameModeChangedMessage> gameModeChangedPublisher,
                ISubscriber<ChangeGameModeRequest> openPageRequestSubscriber
            )
        {
            this.gameModeChangedPublisher = gameModeChangedPublisher;

            openPageRequestSubscriber.Subscribe(ChangeGameMode);
        }

        public void Start()
        {
            EnterMainGameMode();
            gameModeChangedPublisher.Publish(new GameModeChangedMessage(GameMode.Game));
        }

        private void EnterMainGameMode()
        {
            if (GameMode is GameMode.Game)
            {
                return;
            }
            GameMode = GameMode.Game;
            gameModeChangedPublisher.Publish(new GameModeChangedMessage(GameMode));
        }

        private void ChangeGameMode(ChangeGameModeRequest msg)
        {
            if (GameMode == msg.Mode)
            {
                if (GameMode is GameMode.Game)
                {
                    EnterMainGameMode();
                }
                else
                {
                    EnterMainGameMode();
                    return;
                } 
            }
            GameMode = msg.Mode;
            gameModeChangedPublisher.Publish(new GameModeChangedMessage(GameMode));
        }
    }
    
    public enum GameMode
    {
        Game,
        Inventory,
        Looting,
    }
}