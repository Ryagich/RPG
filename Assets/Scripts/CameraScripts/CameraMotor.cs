using GameModes;
using Inventory;
using MessagePipe;
using Messages;
using UnityEngine;
using VContainer.Unity;

namespace CameraScripts
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class CameraMotor : IStartable, ITickable
    {
        private readonly CameraMovement cameraMovement;
        private readonly CameraMovementInPlace cameraMovementInPlace;
        private readonly ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber;

        private CameraModes cameraMode = CameraModes.Gameplay;

        public CameraMotor(
            CameraConfig config,
            Camera cam,
            Transform target,
            CharacterVisualRoot characterVisualRoot,
            ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber)
        {
            var camTransform = cam.transform;
            var facingTarget = characterVisualRoot != null ? characterVisualRoot.transform : target;

            cameraMovement = new CameraMovement(config, camTransform, target, facingTarget);
            cameraMovementInPlace = new CameraMovementInPlace(config, camTransform);
            this.gameModeChangedSubscriber = gameModeChangedSubscriber;
        }

        public void Start()
        {
            gameModeChangedSubscriber.Subscribe(OnGameModeChanged);
            cameraMovement.SetLookInputEnabled(true);
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

        public Quaternion GetGameplayPlanarRotation()
        {
            return cameraMovement.GetPlanarRotation();
        }

        private void OnGameModeChanged(GameModeChangedMessage msg)
        {
            cameraMovement.SetLookInputEnabled(msg.GameMode == GameMode.Game);
        }
    }

    public enum CameraModes
    {
        Gameplay,
        Dialogue
    }
}
