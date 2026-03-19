using UnityEngine;

namespace CameraScripts
{
    [CreateAssetMenu(fileName = "CameraConfig", menuName = "configs/Camera/CameraConfig")]
    public class CameraConfig : ScriptableObject
    {
        [field: SerializeField] public Vector3 CameraPosition { get; private set; } = new(-3.5f, 8.0f, -3.5f);
        [field: SerializeField] public Vector3 CameraRotation { get; private set; } = new(50.0f, 45.0f, .0f);
        [field: SerializeField] public float Smoothing { get; private set; } = 4;
    }
}