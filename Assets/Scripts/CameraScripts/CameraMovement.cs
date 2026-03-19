using UnityEngine;

namespace CameraScripts
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class CameraMovement
    {
        private readonly CameraConfig config;
        private readonly Transform cameraTransform;
        
        private Transform target;

        public CameraMovement
            (
                CameraConfig config,
                Transform cameraTransform,
                Transform target
            )
        {
            this.config = config;
            this.cameraTransform = cameraTransform;
            this.target = target;
        }

        public void Tick(float t)
        {
            cameraTransform.position = Vector3.Lerp(
                                                    cameraTransform.position,
                                                    target.position + config.CameraPosition,
                                                    config.Smoothing * t
                                                   );
            cameraTransform.rotation = Quaternion.Lerp(
                                                       cameraTransform.rotation,
                                                       Quaternion.Euler(config.CameraRotation),
                                                       config.Smoothing * t
                                                      );
        }

        public void ChangeTarget(Transform t)
        {
            target = t;
        }
    }
}