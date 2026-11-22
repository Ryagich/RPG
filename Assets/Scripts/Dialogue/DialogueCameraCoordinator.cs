using System;
using CameraScripts;
using GameModes;
using Input;
using MessagePipe;
using Messages;
using TargetLock;
using UnityEngine;
using VContainer.Unity;

namespace Dialogue
{
    /// <summary>
    /// Connects the dialogue session lifetime to the player camera without giving UI ownership
    /// over either dialogue flow or camera movement.
    /// </summary>
    public sealed class DialogueCameraCoordinator : IStartable, ITickable, IDisposable
    {
        private readonly DialogueContext dialogueContext;
        private readonly CameraMotor cameraMotor;
        private readonly PlayerInputLock playerInputLock;
        private readonly ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber;
        private IDisposable gameModeChangedSubscription;
        private bool isDialogueCameraActive;
        private bool isReturnInputLocked;

        public DialogueCameraCoordinator(
            DialogueContext dialogueContext,
            CameraMotor cameraMotor,
            PlayerInputLock playerInputLock,
            ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber)
        {
            this.dialogueContext = dialogueContext;
            this.cameraMotor = cameraMotor;
            this.playerInputLock = playerInputLock;
            this.gameModeChangedSubscriber = gameModeChangedSubscriber;
        }

        public void Start()
        {
            dialogueContext.Opened += OnDialogueOpened;
            dialogueContext.Closed += OnDialogueClosed;
            gameModeChangedSubscription = gameModeChangedSubscriber.Subscribe(OnGameModeChanged);

            if (dialogueContext.CurrentTarget != null)
            {
                OnDialogueOpened();
            }
        }

        public void Dispose()
        {
            dialogueContext.Opened -= OnDialogueOpened;
            dialogueContext.Closed -= OnDialogueClosed;
            gameModeChangedSubscription?.Dispose();
            gameModeChangedSubscription = null;
            RestoreGameplayCamera();
            ReleaseReturnInputLock();
        }

        public void Tick()
        {
            if (isReturnInputLocked && cameraMotor.IsGameplayReturnInputReady)
            {
                ReleaseReturnInputLock();
            }
        }

        private void OnDialogueOpened()
        {
            ReleaseReturnInputLock();
            var target = dialogueContext.CurrentTarget;
            if (target == null)
            {
                return;
            }

            var targetLockTarget = target.GetComponentInParent<TargetLockTarget>();
            var framingAnchor = targetLockTarget != null ? targetLockTarget.transform : target.transform;
            var faceAnchor = targetLockTarget != null && targetLockTarget.AimTransform != targetLockTarget.transform
                ? targetLockTarget.AimTransform
                : null;

            cameraMotor.ChangeDialogueTarget(framingAnchor, faceAnchor);
            cameraMotor.ChangeCameraMode(CameraModes.Dialogue);
            isDialogueCameraActive = true;
        }

        private void OnDialogueClosed()
        {
            RestoreGameplayCamera();
        }

        private void OnGameModeChanged(GameModeChangedMessage message)
        {
            if (message.GameMode == GameMode.Game)
            {
                RestoreGameplayCamera();
            }
        }

        private void RestoreGameplayCamera()
        {
            if (!isDialogueCameraActive)
            {
                return;
            }

            cameraMotor.RestoreGameplayCamera();
            playerInputLock.SetLocked(this, true);
            isReturnInputLocked = true;
            isDialogueCameraActive = false;
        }

        private void ReleaseReturnInputLock()
        {
            if (!isReturnInputLocked)
            {
                return;
            }

            playerInputLock.SetLocked(this, false);
            isReturnInputLocked = false;
        }
    }
}
