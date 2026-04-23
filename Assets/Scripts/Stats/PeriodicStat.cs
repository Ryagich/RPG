using System;
using UnityEngine;

namespace Stats
{
    [Serializable]
    public class PeriodicStat : SafeStat
    {
        [field: SerializeField] public float PeriodicChange { get; private set; }

        public PeriodicStat
            (
                float max,
                float min,
                float value,
                float periodicChange,
                float minSafePercent = 0.15f
            ) : base(max, min, value, minSafePercent)
        {
            PeriodicChange = periodicChange;
        }

        public PeriodicStat(PeriodicStat oldStat) : this
            (
                oldStat.Max,
                oldStat.Min,
                oldStat.Value.Value,
                oldStat.PeriodicChange,
                oldStat.MinSafePercent
           ) { }
    }
}
