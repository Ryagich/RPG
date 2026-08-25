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
        private GameMode pauseResumeMode = GameMode.Game;

        public GameModesController(
            IPublisher<GameModeChangedMessage> gameModeChangedPublisher,
            DialogueContext dialogueContext,
            ISubscriber<ChangeGameModeRequest> openPageRequestSubscriber,
            ISubscriber<InventoryInputMessage> inventoryInputSubscriber,
            ISubscriber<MapInputMessage> mapInputSubscriber,
            ISubscriber<QuestLogInputMessage> questLogInputSubscriber,
            ISubscriber<PauseInputMessage> pauseInputSubscriber)
        {
            this.gameModeChangedPublisher = gameModeChangedPublisher;
            this.dialogueContext = dialogueContext;

            openPageRequestSubscriber.Subscribe(ChangeGameMode);
            inventoryInputSubscriber.Subscribe(OnInventoryInput);
            mapInputSubscriber.Subscribe(OnMapInput);
            questLogInputSubscriber.Subscribe(OnQuestLogInput);
            pauseInputSubscriber.Subscribe(OnPauseInput);
        }

        public void Start()
        {
            pauseResumeMode = GameMode.Game;
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
                pauseResumeMode = GameMode.Game;
                ApplyGameMode(GameMode.Death);
                return;
            }

            if (msg.Mode == GameMode.Pause)
            {
                if (GameMode is GameMode.Game or GameMode.Lesson)
                {
                    EnterPauseMode(GameMode);
                }
                else if (GameMode == GameMode.PauseSettings)
                {
                    ApplyGameMode(GameMode.Pause);
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
                case GameMode.SwitchLocation:
                    return;
                default:
                    EnterMainGameMode();
                    return;
            }
        }

        private void OnInventoryInput(InventoryInputMessage _)
        {
            if (GameMode == GameMode.Death)
            {
                return;
            }

            if (!IsNavigationPageMode(GameMode))
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

            if (!IsNavigationPageMode(GameMode))
            {
                return;
            }

            ChangeGameMode(new ChangeGameModeRequest(GameMode == GameMode.Map ? GameMode.Game : GameMode.Map));
        }

        private void OnQuestLogInput(QuestLogInputMessage _)
        {
            if (GameMode == GameMode.Death)
            {
                return;
            }

            if (!IsNavigationPageMode(GameMode))
            {
                return;
            }

            ChangeGameMode(new ChangeGameModeRequest(GameMode == GameMode.Quest ? GameMode.Game : GameMode.Quest));
        }

        private void EnterMainGameMode()
        {
            pauseResumeMode = GameMode.Game;
            ApplyGameMode(GameMode.Game);
        }

        private void EnterPauseMode(GameMode resumeMode)
        {
            pauseResumeMode = resumeMode;
            ApplyGameMode(GameMode.Pause);
        }

        private void ExitPauseMode()
        {
            var resumeMode = pauseResumeMode;
            pauseResumeMode = GameMode.Game;
            ApplyGameMode(resumeMode);
        }

        private static bool IsNavigationPageMode(GameMode mode)
        {
            return mode is GameMode.Game or GameMode.Inventory or GameMode.Map or GameMode.Quest;
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
