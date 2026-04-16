using Stats;
using UnityEngine;

namespace UI
{
    [CreateAssetMenu(fileName = "StatsConfig", menuName = "configs/Stats/StatsConfig")]
    public class StatsConfig : ScriptableObject
    {
        [Header("Stats Settings")] 
        [field: SerializeField] public Stat HpStat { get; private set; }
        
        [field: Space, Header("Bar")]
        [field: SerializeField, Min(0)] public float FillSpeed  { get; private set; } = .5f;
        [field: SerializeField, Min(0)] public float FllLerpSpeed { get; private set; } = .1f;
        [field: SerializeField, Min(0)] public float OffsetBarSize { get; private set; } = 3.0f;
        
        [field: Space, Header("Heart")]
        [field: SerializeField, Min(0)] public int MinHeartbeat { get; private set; } = 80;
        [field: SerializeField, Min(0)] public int MaxHeartbeat { get; private set; } = 190;
        [field: SerializeField] public int Sharpness { get; private set; } = 3;
        [field: SerializeField] public float HeartDefSize { get; private set; } = .9f;
        [field: SerializeField] public float HeartMaxSize { get; private set; } = 1f;
        
        [field: Space, Header("Bars Colors")]
        [field: SerializeField] public Color HpFullColor { get; private set; } = Color.white;
        [field: SerializeField] public Color HpRecoveryColor { get; private set; } = Color.green;
        [field: SerializeField] public Color HpDecreaseColor { get; private set; } = Color.red;
        [field: SerializeField] public Color HpTakenAwayColor { get; private set; } = Color.yellow;
        [field: SerializeField] public Color HpEmptyColor { get; private set; } = Color.black;
    }
}
