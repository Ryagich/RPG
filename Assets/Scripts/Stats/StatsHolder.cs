using UnityEngine;

namespace Stats
{
    public class StatsHolder : MonoBehaviour
    {
        [field: SerializeField] public StatHolder HPHolder { get; private set; }
    }
}