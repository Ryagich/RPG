using System;
using GameModes;
using MessagePipe;
using Messages;
using VContainer.Unity;

namespace Dialogue
{
    /// <summary>
    /// Completes the active dialogue through the existing game-mode and interaction lifecycle.
    /// </summary>
    public sealed class DialogueExitController : IStartable, IDisposable
    {
        private readonly DialogueContext dialogueContext;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;
        private readonly ISubscriber<DialogueExitRequestedMessage> dialogueExitRequestedSubscriber;
        private IDisposable dialogueExitSubscription;

        public DialogueExitController(
            DialogueContext dialogueContext,
            IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher,
            ISubscriber<DialogueExitRequestedMessage> dialogueExitRequestedSubscriber)
        {
            this.dialogueContext = dialogueContext;
            this.changeGameModeRequestPublisher = changeGameModeRequestPublisher;
            this.dialogueExitRequestedSubscriber = dialogueExitRequestedSubscriber;
        }

        public void Start()
        {
            dialogueExitSubscription = dialogueExitRequestedSubscriber.Subscribe(OnDialogueExitRequested);
        }

        public void Dispose()
        {
            dialogueExitSubscription?.Dispose();
            dialogueExitSubscription = null;
        }

        private void OnDialogueExitRequested(DialogueExitRequestedMessage message)
        {
            if (!dialogueContext.TryForceExit(message.ContinueForcedDialogueAfterExit))
            {
                return;
            }

            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
        }
    }
}
