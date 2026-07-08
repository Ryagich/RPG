using UnityEngine;

namespace World
{
    public enum RotationAxis
    {
        X,
        Y,
        Z
    }

    public sealed class WindmillRotator : MonoBehaviour
    {
        [SerializeField] private RotationAxis axis = RotationAxis.Z;
        [SerializeField] private float speed = 90f;

        private void Update()
        {
            transform.Rotate(GetAxis(axis), speed * Time.deltaTime, Space.Self);
        }

        private static Vector3 GetAxis(RotationAxis rotationAxis)
        {
            return rotationAxis switch
            {
                RotationAxis.X => Vector3.right,
                RotationAxis.Y => Vector3.up,
                _ => Vector3.forward
            };
        }
    }
}
