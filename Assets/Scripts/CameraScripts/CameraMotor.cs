using GameModes;
using Inventory;
using MessagePipe;
using Messages;
using TargetLock;
using UnityEngine;
using UniRx;
using VContainer.Unity;

namespace CameraScripts
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class CameraMotor : IStartable, ITickable, ILateTickable, System.IDisposable
    {
        private readonly CameraMovement cameraMovement;
        private readonly CameraMovementInPlace cameraMovementInPlace;
        private readonly ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber;
        private readonly ISubscriber<CameraLookInputMessage> cameraLookInputSubscriber;
        private readonly ISubscriber<CameraZoomInputMessage> cameraZoomInputSubscriber;
        private readonly ISubscriber<PlayerMoveMessage> playerMoveSubscriber;
        private readonly CompositeDisposable disposables = new();

        private CameraModes cameraMode = CameraModes.Gameplay;
        private bool gameplayCameraInputRequestedDuringReturn;

        public CameraMotor(
            CameraConfig config,
            DialogueCameraConfig dialogueCameraConfig,
            TargetLockConfig targetLockConfig,
            Camera cam,
            Transform target,
            CharacterVisualRoot characterVisualRoot,
            ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber,
            ISubscriber<CameraLookInputMessage> cameraLookInputSubscriber,
            ISubscriber<CameraZoomInputMessage> cameraZoomInputSubscriber,
            ISubscriber<PlayerMoveMessage> playerMoveSubscriber)
        {
            var camTransform = cam.transform;
            var facingTarget = characterVisualRoot != null ? characterVisualRoot.transform : target;

            cameraMovement = new CameraMovement(config, targetLockConfig, camTransform, target, facingTarget);
            cameraMovementInPlace = new CameraMovementInPlace(
                dialogueCameraConfig,
                camTransform,
                target.GetComponent<CharacterController>());
            this.gameModeChangedSubscriber = gameModeChangedSubscriber;
            this.cameraLookInputSubscriber = cameraLookInputSubscriber;
            this.cameraZoomInputSubscriber = cameraZoomInputSubscriber;
            this.playerMoveSubscriber = playerMoveSubscriber;
        }

        public void Start()
        {
            gameModeChangedSubscriber.Subscribe(OnGameModeChanged).AddTo(disposables);
            cameraLookInputSubscriber.Subscribe(OnLookInput).AddTo(disposables);
            cameraZoomInputSubscriber.Subscribe(OnZoomInput).AddTo(disposables);
            playerMoveSubscriber.Subscribe(OnPlayerMoveInput).AddTo(disposables);
            cameraMovement.SetLookInputEnabled(true);
        }

        public void Dispose()
        {
            disposables.Dispose();
        }

        public void Tick()
        {
            if (cameraMode is CameraModes.Dialogue)
            {
                cameraMovementInPlace.Tick(Time.deltaTime);
            }
        }

        /// <summary>
        /// Gameplay follow is resolved after CharacterMotor has completed its CharacterController.Move.
        /// This keeps the camera and the player root on the same simulation step at render time.
        /// </summary>
        public void LateTick()
        {
            if (cameraMode is CameraModes.Gameplay)
            {
                cameraMovement.Tick(Time.deltaTime);
                return;
            }

            if (cameraMode is CameraModes.ReturningToGameplay)
            {
                if (!cameraMovement.TryUpdateDesiredPose(Time.deltaTime, out var targetPose))
                {
                    cameraMode = CameraModes.Gameplay;
                    gameplayCameraInputRequestedDuringReturn = false;
                    return;
                }

                if (gameplayCameraInputRequestedDuringReturn)
                {
                    cameraMovementInPlace.CancelGameplayReturn();
                    cameraMovement.ApplyPose(targetPose);
                    cameraMode = CameraModes.Gameplay;
                    gameplayCameraInputRequestedDuringReturn = false;
                    return;
                }

                if (cameraMovementInPlace.TickGameplayReturn(Time.deltaTime, targetPose))
                {
                    cameraMode = CameraModes.Gameplay;
                    gameplayCameraInputRequestedDuringReturn = false;
                }
            }
        }

        public void ChangeGameplayTarget(Transform t)
        {
            cameraMovement.ChangeTarget(t);
        }

        public void ChangeDialogueTarget(Transform framingAnchor, Transform faceAnchor)
        {
            cameraMovementInPlace.ChangeTarget(framingAnchor, faceAnchor);
        }

        public void ChangeCameraMode(CameraModes mode)
        {
            if (mode is CameraModes.Gameplay && cameraMode is CameraModes.Dialogue)
            {
                RestoreGameplayCamera();
                return;
            }

            cameraMode = mode;
        }

        public void RestoreGameplayCamera()
        {
            gameplayCameraInputRequestedDuringReturn = false;
            cameraMovementInPlace.ClearTarget();
            cameraMovement.ResetToDefaultOrbit();
            if (cameraMovement.TryGetDefaultPose(out var targetPose))
            {
                cameraMovementInPlace.BeginGameplayReturn(targetPose);
                cameraMode = CameraModes.ReturningToGameplay;
                return;
            }

            cameraMode = CameraModes.Gameplay;
        }

        public void SetTargetLockTarget(Transform target)
        {
            cameraMovement.SetLockTarget(target);
        }

        public Quaternion GetGameplayPlanarRotation()
        {
            return cameraMovement.GetPlanarRotation();
        }

        /// <summary>Rotates the gameplay orbit without going through pointer sensitivity.</summary>
        public void RotateGameplayYaw(float degrees)
        {
            if (cameraMode == CameraModes.Gameplay)
            {
                cameraMovement.RotateYaw(degrees);
            }
        }

        /// <summary>True once the gameplay camera has reached its current follow pose.</summary>
        public bool IsGameplaySettled => cameraMovement.IsSettled();

        /// <summary>True when the returning camera is close enough to gameplay for player input to resume.</summary>
        public bool IsGameplayReturnInputReady =>
            cameraMode is not CameraModes.ReturningToGameplay || cameraMovementInPlace.IsReturnInputReady;

        private void OnGameModeChanged(GameModeChangedMessage msg)
        {
            cameraMovement.SetLookInputEnabled(msg.GameMode is GameMode.Game or GameMode.Death);
        }

        private void OnLookInput(CameraLookInputMessage message)
        {
            cameraMovement.AddLookInput(message.Delta);
            if (cameraMode is CameraModes.ReturningToGameplay && message.Delta.sqrMagnitude > Mathf.Epsilon)
            {
                gameplayCameraInputRequestedDuringReturn = true;
            }
        }

        private void OnZoomInput(CameraZoomInputMessage message)
        {
            if (message.Direction == CameraZoomDirection.In)
            {
                cameraMovement.ZoomIn();
                gameplayCameraInputRequestedDuringReturn |= cameraMode is CameraModes.ReturningToGameplay;
                return;
            }

            cameraMovement.ZoomOut();
            gameplayCameraInputRequestedDuringReturn |= cameraMode is CameraModes.ReturningToGameplay;
        }

        private void OnPlayerMoveInput(PlayerMoveMessage message)
        {
            if (cameraMode is CameraModes.ReturningToGameplay && message.Direction.sqrMagnitude > Mathf.Epsilon)
            {
                gameplayCameraInputRequestedDuringReturn = true;
            }
        }
    }

    public enum CameraModes
    {
        Gameplay,
        Dialogue,
        ReturningToGameplay
    }
}
