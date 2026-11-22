using UnityEngine;

namespace Promo
{
    public class RotateAlongMovement : MonoBehaviour
    {
        [Header("Rotation")]
        [SerializeField] float smoothSpeed = 10f;
        [SerializeField] float minMovement = 0.001f;

        [Header("Axis Locks")]
        [SerializeField] bool lockX = true;
        [SerializeField] bool lockY = false;
        [SerializeField] bool lockZ = true;

        private Vector3 lastPosition;

        void Start()
        {
            lastPosition = transform.position;
        }

        void LateUpdate()
        {
            var movement = transform.position - lastPosition;
            lastPosition = transform.position;

            if (movement.sqrMagnitude < minMovement * minMovement)
                return;

            var targetRotation = Quaternion.LookRotation(movement.normalized);

            var targetEuler = targetRotation.eulerAngles;
            var currentEuler = transform.rotation.eulerAngles;

            if (lockX) targetEuler.x = currentEuler.x;
            if (lockY) targetEuler.y = currentEuler.y;
            if (lockZ) targetEuler.z = currentEuler.z;

            targetRotation = Quaternion.Euler(targetEuler);

            var t = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                t
            );
        }
    }
}