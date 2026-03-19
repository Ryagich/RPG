using UnityEngine;
using VContainer.Unity;

namespace CameraScripts
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class CameraMotor : ITickable
    {
        private readonly CameraMovement cameraMovement;
        private readonly CameraMovementInPlace cameraMovementInPlace;

        private readonly Transform target;
        
        private CameraModes cameraMode = CameraModes.Gameplay;
        
        public CameraMotor
            (
                CameraConfig config,
                Camera cam,
                Transform target
            )
        {
            var camTransform = cam.transform;
            cameraMovement = new CameraMovement(config, camTransform, target);
            cameraMovementInPlace = new CameraMovementInPlace(config, camTransform);
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
    }

    public enum CameraModes
    {
        Gameplay,
        Dialogue
    }
}