using GameModes;
using Inventory;
using MessagePipe;
using Messages;
using TargetLock;
using Input;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace CameraScripts
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class CameraMotor : IStartable, ITickable, System.IDisposable
    {
        private readonly CameraMovement cameraMovement;
        private readonly CameraMovementInPlace cameraMovementInPlace;
        private readonly ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber;
        private readonly InputConfig inputConfig;

        private InputAction zoomInAction;
        private InputAction zoomOutAction;

        private CameraModes cameraMode = CameraModes.Gameplay;

        public CameraMotor(
            CameraConfig config,
            TargetLockConfig targetLockConfig,
            Camera cam,
            Transform target,
            CharacterVisualRoot characterVisualRoot,
            InputConfig inputConfig,
            ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber)
        {
            var camTransform = cam.transform;
            var facingTarget = characterVisualRoot != null ? characterVisualRoot.transform : target;

            cameraMovement = new CameraMovement(config, targetLockConfig, camTransform, target, facingTarget);
            cameraMovementInPlace = new CameraMovementInPlace(config, camTransform);
            this.inputConfig = inputConfig;
            this.gameModeChangedSubscriber = gameModeChangedSubscriber;
        }

        public void Start()
        {
            gameModeChangedSubscriber.Subscribe(OnGameModeChanged);
            cameraMovement.SetLookInputEnabled(true);
            zoomInAction = inputConfig.CameraZoomIn?.action;
            zoomOutAction = inputConfig.CameraZoomOut?.action;
            zoomInAction?.Enable();
            zoomOutAction?.Enable();
            if (zoomInAction != null)
            {
                zoomInAction.performed += OnZoomIn;
            }
            if (zoomOutAction != null)
            {
                zoomOutAction.performed += OnZoomOut;
            }
        }

        public void Dispose()
        {
            if (zoomInAction != null)
            {
                zoomInAction.performed -= OnZoomIn;
            }
            if (zoomOutAction != null)
            {
                zoomOutAction.performed -= OnZoomOut;
            }
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

        private void OnZoomIn(InputAction.CallbackContext _) => cameraMovement.ZoomIn();

        private void OnZoomOut(InputAction.CallbackContext _) => cameraMovement.ZoomOut();
    }

    public enum CameraModes
    {
        Gameplay,
        Dialogue
    }
}
