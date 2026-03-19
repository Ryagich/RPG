using UnityEngine;

namespace CameraScripts
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class CameraMovementInPlace
    {
        private readonly CameraConfig config;
        private readonly Transform cameraTransform;
        
        private Transform target;

        public CameraMovementInPlace
            (
                CameraConfig config,
                Transform cameraTransform
            )
        {
            this.config = config;
            this.cameraTransform = cameraTransform;
        }

        public void Tick(float t)
        {
            if (!target) 
                return;
            cameraTransform.position = Vector3.Lerp(
                                                    cameraTransform.position,
                                                    target.position,
                                                    config.Smoothing * t
                                                   );
            cameraTransform.rotation = Quaternion.Lerp(
                                                       cameraTransform.rotation,
                                                       target.rotation,
                                                       config.Smoothing * t
                                                      );
        }

        public void ChangeTarget(Transform t)
        {
            target = t;
        }
    }
}