using System.Collections.Generic;
using MessagePipe;
using Messages;
using UnityEngine;
using VContainer.Unity;

namespace GameModes
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class GameModesController : IStartable
    {
        public GameMode GameMode { get; private set; } = GameMode.Game;

        private readonly IPublisher<GameModeChangedMessage> gameModeChangedPublisher;
        private readonly Stack<GameMode> navigationHistory = new();

        public GameModesController(
            IPublisher<GameModeChangedMessage> gameModeChangedPublisher,
            ISubscriber<ChangeGameModeRequest> openPageRequestSubscriber,
            ISubscriber<InventoryInputMessage> inventoryInputSubscriber,
            ISubscriber<MapInputMessage> mapInputSubscriber,
            ISubscriber<PauseInputMessage> pauseInputSubscriber)
        {
            this.gameModeChangedPublisher = gameModeChangedPublisher;

            openPageRequestSubscriber.Subscribe(ChangeGameMode);
            inventoryInputSubscriber.Subscribe(OnInventoryInput);
            mapInputSubscriber.Subscribe(OnMapInput);
            pauseInputSubscriber.Subscribe(OnPauseInput);
        }

        public void Start()
        {
            navigationHistory.Clear();
            ApplyGameMode(GameMode.Game);
        }

        private void ChangeGameMode(ChangeGameModeRequest msg)
        {
            if (GameMode == GameMode.Death)
            {
                return;
            }

            if (msg.Mode == GameMode.Death)
            {
                navigationHistory.Clear();
                ApplyGameMode(GameMode.Death);
                return;
            }

            if (msg.Mode == GameMode.Pause)
            {
                if (GameMode is GameMode.Game)
                {
                    EnterPauseMode();
                }

                return;
            }

            if (msg.Mode == GameMode.Game)
            {
                EnterMainGameMode();
                return;
            }

            if (msg.Mode == GameMode)
            {
                return;
            }

            if (navigationHistory.Count > 0 && navigationHistory.Peek() == msg.Mode)
            {
                navigationHistory.Pop();
                ApplyGameMode(msg.Mode);
                return;
            }

            navigationHistory.Push(GameMode);
            ApplyGameMode(msg.Mode);
        }

        private void OnPauseInput(PauseInputMessage _)
        {
            if (GameMode == GameMode.Death)
            {
                return;
            }

            switch (GameMode)
            {
                case GameMode.Game:
                    EnterPauseMode();
                    return;
                case GameMode.Pause:
                    ExitPauseMode();
                    return;
                default:
                    ReturnToPreviousMode();
                    return;
            }
        }

        private void OnInventoryInput(InventoryInputMessage _)
        {
            if (GameMode == GameMode.Death)
            {
                return;
            }

            ChangeGameMode(new ChangeGameModeRequest(GameMode == GameMode.Inventory ? GameMode.Game : GameMode.Inventory));
        }

        private void OnMapInput(MapInputMessage _)
        {
            if (GameMode == GameMode.Death)
            {
                return;
            }

            if (GameMode is GameMode.Game or GameMode.Inventory)
            {
                ChangeGameMode(new ChangeGameModeRequest(GameMode.Map));
            }
            else if (GameMode == GameMode.Map)
            {
                ChangeGameMode(new ChangeGameModeRequest(GameMode.Game));
            }
        }

        private void EnterMainGameMode()
        {
            navigationHistory.Clear();
            ApplyGameMode(GameMode.Game);
        }

        private void EnterPauseMode()
        {
            navigationHistory.Push(GameMode.Game);
            ApplyGameMode(GameMode.Pause);
        }

        private void ExitPauseMode()
        {
            if (navigationHistory.Count > 0 && navigationHistory.Peek() == GameMode.Game)
            {
                navigationHistory.Pop();
            }

            ApplyGameMode(GameMode.Game);
        }

        private void ReturnToPreviousMode()
        {
            if (navigationHistory.Count == 0)
            {
                EnterMainGameMode();
                return;
            }

            var previousMode = navigationHistory.Pop();
            ApplyGameMode(previousMode);
        }

        private void ApplyGameMode(GameMode mode)
        {
            GameMode = mode;
            ApplyCursorState(mode);
            ApplyTimeScale(mode);
            gameModeChangedPublisher.Publish(new GameModeChangedMessage(mode));
        }

        private static void ApplyCursorState(GameMode mode)
        {
            var isGameplayMode = mode is GameMode.Game or GameMode.Death;
            Cursor.lockState = isGameplayMode ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !isGameplayMode;
        }

        private static void ApplyTimeScale(GameMode mode)
        {
            Time.timeScale = mode == GameMode.Pause ? 0f : 1f;
        }
    }

    public enum GameMode
    {
        Game,
        Pause,
        Inventory,
        Looting,
        Dialogue,
        Trade,
        Map,
        Death,
    }
}
