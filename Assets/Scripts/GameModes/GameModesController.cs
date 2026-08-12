using System.Collections.Generic;
using Dialogue;
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
        private readonly DialogueContext dialogueContext;
        private readonly Stack<GameMode> navigationHistory = new();

        public GameModesController(
            IPublisher<GameModeChangedMessage> gameModeChangedPublisher,
            DialogueContext dialogueContext,
            ISubscriber<ChangeGameModeRequest> openPageRequestSubscriber,
            ISubscriber<InventoryInputMessage> inventoryInputSubscriber,
            ISubscriber<MapInputMessage> mapInputSubscriber,
            ISubscriber<PauseInputMessage> pauseInputSubscriber)
        {
            this.gameModeChangedPublisher = gameModeChangedPublisher;
            this.dialogueContext = dialogueContext;

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

            if (msg.Mode == GameMode.Game && dialogueContext.IsForcedDialogue && !dialogueContext.CanExitDialogue)
            {
                return;
            }

            if (GameMode == GameMode.SwitchLocation && msg.Mode != GameMode.Game)
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
                if (GameMode is GameMode.Game or GameMode.Lesson)
                {
                    EnterPauseMode(GameMode);
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

            if (GameMode == GameMode.Dialogue && dialogueContext.IsForcedDialogue && !dialogueContext.CanExitDialogue)
            {
                return;
            }

            switch (GameMode)
            {
                case GameMode.Game:
                    EnterPauseMode(GameMode.Game);
                    return;
                case GameMode.Lesson:
                    EnterPauseMode(GameMode.Lesson);
                    return;
                case GameMode.Pause:
                    ExitPauseMode();
                    return;
                case GameMode.Quest:
                    EnterMainGameMode();
                    return;
                case GameMode.SwitchLocation:
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

            // A lesson owns the active gameplay flow. Its read-only navigation is handled via
            // the map/quest modes, but inventory interaction must not replace the lesson page
            // and leave the presentation context without a visible owner.
            if (GameMode == GameMode.Lesson)
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

            if (GameMode is GameMode.Game or GameMode.Inventory or GameMode.Lesson)
            {
                ChangeGameMode(new ChangeGameModeRequest(GameMode.Map));
            }
            else if (GameMode is GameMode.Map or GameMode.Quest)
            {
                // Keep the navigation stack intact: Map can be opened from a paused lesson,
                // and closing it must return to that lesson rather than terminating the flow.
                ReturnToPreviousMode();
            }
        }

        private void EnterMainGameMode()
        {
            navigationHistory.Clear();
            ApplyGameMode(GameMode.Game);
        }

        private void EnterPauseMode(GameMode resumeMode)
        {
            navigationHistory.Push(resumeMode);
            ApplyGameMode(GameMode.Pause);
        }

        private void ExitPauseMode()
        {
            if (navigationHistory.Count > 0)
            {
                ApplyGameMode(navigationHistory.Pop());
                return;
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
            Cursor.lockState = isGameplayMode ? CursorLockMode.Locked : CursorLockMode.Confined;
            Cursor.visible = !isGameplayMode;
        }

        private static void ApplyTimeScale(GameMode mode)
        {
            Time.timeScale = mode is GameMode.Pause or GameMode.PauseSettings or GameMode.Map or GameMode.Quest or GameMode.Lesson or GameMode.SwitchLocation ? 0f : 1f;
        }
    }

    public enum GameMode
    {
        Game,
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
    }
}
