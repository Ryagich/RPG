using UnityEngine;

namespace UI.Compass
{
    [CreateAssetMenu(fileName = "Compass Config", menuName = "configs/UI/Compass Config")]
    public class CompassConfig : ScriptableObject
    {
        [field: Header("Directions")]
        [field: SerializeField] public string[] Labels { get; private set; } = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        [field: SerializeField, Range(-180f, 180f)] public float NorthOffset { get; private set; }

        [field: Header("Smoothing")]
        [field: SerializeField, Range(0f, 20f)] public float SwitchDeadZone { get; private set; } = 3f;
    }
}