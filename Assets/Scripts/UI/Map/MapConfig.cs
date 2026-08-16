using UnityEngine;

namespace UI.Map
{
    [CreateAssetMenu(fileName = "Map Config", menuName = "configs/UI/Map Config")]
    public class MapConfig : ScriptableObject
    {
        [field: SerializeField, Min(0.01f)] public float ZoomSpeed { get; private set; } = 0.2f;
        [field: SerializeField, Min(0.1f)] public float MinZoom { get; private set; } = 1f;
        [field: SerializeField, Min(0.1f)] public float MaxZoom { get; private set; } = 3f;
        [field: SerializeField, Min(0.01f)] public float FocusMoveDuration { get; private set; } = 0.4f;
        [field: Header("Map screenshot projection")]
        [field: SerializeField] public Vector3 CaptureCameraPosition { get; private set; }
        [field: SerializeField] public Vector3 CaptureCameraEulerAngles { get; private set; }
        [field: SerializeField, Range(1f, 179f)] public float CaptureVerticalFieldOfView { get; private set; } = 60f;
        [field: SerializeField, Min(0.01f)] public float CaptureAspectRatio { get; private set; } = 16f / 9f;

        public bool TryProjectWorldPosition(Vector3 worldPosition, out Vector2 viewportPosition)
        {
            Matrix4x4 worldToCamera = Matrix4x4.TRS(
                CaptureCameraPosition,
                Quaternion.Euler(CaptureCameraEulerAngles),
                Vector3.one).inverse;
            Vector3 cameraPosition = worldToCamera.MultiplyPoint3x4(worldPosition);

            if (cameraPosition.z <= 0.001f)
            {
                viewportPosition = default;
                return false;
            }

            float verticalHalfSize = cameraPosition.z * Mathf.Tan(CaptureVerticalFieldOfView * Mathf.Deg2Rad * 0.5f);
            float horizontalHalfSize = verticalHalfSize * CaptureAspectRatio;
            viewportPosition = new Vector2(
                0.5f + cameraPosition.x / (horizontalHalfSize * 2f),
                0.5f + cameraPosition.y / (verticalHalfSize * 2f));
            return true;
        }
    }
}
