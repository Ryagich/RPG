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
    public class CameraMotor : IStartable, ITickable, System.IDisposable
    {
        private readonly CameraMovement cameraMovement;
        private readonly CameraMovementInPlace cameraMovementInPlace;
        private readonly ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber;
        private readonly ISubscriber<CameraLookInputMessage> cameraLookInputSubscriber;
        private readonly ISubscriber<CameraZoomInputMessage> cameraZoomInputSubscriber;
        private readonly CompositeDisposable disposables = new();

        private CameraModes cameraMode = CameraModes.Gameplay;

        public CameraMotor(
            CameraConfig config,
            TargetLockConfig targetLockConfig,
            Camera cam,
            Transform target,
            CharacterVisualRoot characterVisualRoot,
            ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber,
            ISubscriber<CameraLookInputMessage> cameraLookInputSubscriber,
            ISubscriber<CameraZoomInputMessage> cameraZoomInputSubscriber)
        {
            var camTransform = cam.transform;
            var facingTarget = characterVisualRoot != null ? characterVisualRoot.transform : target;

            cameraMovement = new CameraMovement(config, targetLockConfig, camTransform, target, facingTarget);
            cameraMovementInPlace = new CameraMovementInPlace(config, camTransform);
            this.gameModeChangedSubscriber = gameModeChangedSubscriber;
            this.cameraLookInputSubscriber = cameraLookInputSubscriber;
            this.cameraZoomInputSubscriber = cameraZoomInputSubscriber;
        }

        public void Start()
        {
            gameModeChangedSubscriber.Subscribe(OnGameModeChanged).AddTo(disposables);
            cameraLookInputSubscriber.Subscribe(OnLookInput).AddTo(disposables);
            cameraZoomInputSubscriber.Subscribe(OnZoomInput).AddTo(disposables);
            cameraMovement.SetLookInputEnabled(true);
        }

        public void Dispose()
        {
            disposables.Dispose();
        }

        public void Tick()
        {
            if (cameraMode is CameraModes.Gameplay)
            {
                cameraMovement.Tick(Time.deltaTime);
            }
            else
            {
                cameraMovementInPlace.Tick(Time.deltaTime);
            }
        }

        public void ChangeGameplayTarget(Transform t)
        {
            cameraMovement.ChangeTarget(t);
        }

        public void ChangeDialogueTarget(Transform t)
        {
            cameraMovementInPlace.ChangeTarget(t);
        }

        public void ChangeCameraMode(CameraModes mode)
        {
            cameraMode = mode;
        }

        public void SetTargetLockTarget(Transform target)
        {
            cameraMovement.SetLockTarget(target);
        }

        public Quaternion GetGameplayPlanarRotation()
        {
            return cameraMovement.GetPlanarRotation();
        }

        /// <summary>True once the gameplay camera has reached its current follow pose.</summary>
        public bool IsGameplaySettled => cameraMovement.IsSettled();

        private void OnGameModeChanged(GameModeChangedMessage msg)
        {
            cameraMovement.SetLookInputEnabled(msg.GameMode is GameMode.Game or GameMode.Death);
        }

        private void OnLookInput(CameraLookInputMessage message) => cameraMovement.AddLookInput(message.Delta);

        private void OnZoomInput(CameraZoomInputMessage message)
        {
            if (message.Direction == CameraZoomDirection.In)
            {
                cameraMovement.ZoomIn();
                return;
            }

            cameraMovement.ZoomOut();
        }
    }

    public enum CameraModes
    {
        Gameplay,
        Dialogue
    }
}
