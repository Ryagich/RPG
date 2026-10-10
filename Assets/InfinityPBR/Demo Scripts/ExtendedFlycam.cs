using UnityEngine;

namespace InfinityPBR
{
    public class ExtendedFlycam : MonoBehaviour
    {
        public float CameraSensitivity = 90;
        public float ClimbSpeed = 4;
        public float NormalMoveSpeed = 10;
        public float SlowMoveFactor = 0.25f;
        public float FastMoveFactor = 3;

        private float _rotationX;
        private float _rotationY;

        // ReSharper disable once UnusedMember.Local
        private void Start()
        {
            _rotationX = transform.eulerAngles.y;
        }

        // ReSharper disable once UnusedMember.Local
        private void Update()
        {
            if (InfinityPBR.Demo.InfinityDemoInput.RightMouseHeld)
            {
                _rotationX += InfinityPBR.Demo.InfinityDemoInput.MouseDelta.x * CameraSensitivity * Time.deltaTime;
                _rotationY += InfinityPBR.Demo.InfinityDemoInput.MouseDelta.y * CameraSensitivity * Time.deltaTime;
                _rotationY = Mathf.Clamp(_rotationY, -90, 90);
            }

            Quaternion targetRotation = Quaternion.AngleAxis(_rotationX, Vector3.up);
            targetRotation *= Quaternion.AngleAxis(_rotationY, Vector3.left);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 4f);

            float speedFactor = 1f;
            if (InfinityPBR.Demo.InfinityDemoInput.ShiftHeld) speedFactor = FastMoveFactor;
            if (InfinityPBR.Demo.InfinityDemoInput.ControlHeld) speedFactor = SlowMoveFactor;

            transform.position += transform.forward * NormalMoveSpeed * speedFactor * InfinityPBR.Demo.InfinityDemoInput.Vertical *
                                  Time.deltaTime;
            transform.position += transform.right * NormalMoveSpeed * speedFactor * InfinityPBR.Demo.InfinityDemoInput.Horizontal *
                                  Time.deltaTime;
            float upAxis = 0;
            if (InfinityPBR.Demo.InfinityDemoInput.IsHeld(UnityEngine.InputSystem.Key.Q)) upAxis = -0.5f;
            if (InfinityPBR.Demo.InfinityDemoInput.IsHeld(UnityEngine.InputSystem.Key.E)) upAxis = 0.5f;
            transform.position += transform.up * NormalMoveSpeed * speedFactor * upAxis * Time.deltaTime;
        }
    }
}
