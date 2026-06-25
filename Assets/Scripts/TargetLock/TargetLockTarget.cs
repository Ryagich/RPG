using UnityEngine;

namespace TargetLock
{
    public sealed class TargetLockTarget : MonoBehaviour
    {
        [SerializeField] private Transform aimPoint;
        [SerializeField] private Vector3 aimOffset = new(0f, 1.35f, 0f);

        public Transform AimTransform => aimPoint != null ? aimPoint : transform;
        public Vector3 AimPosition => aimPoint != null ? aimPoint.position : transform.position + aimOffset;
        public bool IsTargetable => isActiveAndEnabled && gameObject.activeInHierarchy;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(AimPosition, 0.35f);
        }
    }
}
