using System;
using UnityEngine;

namespace Stats
{
    [Serializable]
    public class AdditionalPeriodicStat : PeriodicStat
    {
        [field: SerializeField, Min(0f)] public float PeriodicHpDamageWhenEmpty { get; private set; }

        public AdditionalPeriodicStat(
            float max,
            float min,
            float value,
            float periodicChange,
            float minSafePercent = 0.15f,
            float periodicHpDamageWhenEmpty = 0f)
            : base(max, min, value, periodicChange, minSafePercent)
        {
            PeriodicHpDamageWhenEmpty = Mathf.Max(0f, periodicHpDamageWhenEmpty);
        }

        public AdditionalPeriodicStat(AdditionalPeriodicStat oldStat)
            : this(
                oldStat.Max,
                oldStat.Min,
                oldStat.Value.Value,
                oldStat.PeriodicChange,
                oldStat.MinSafePercent,
                oldStat.PeriodicHpDamageWhenEmpty)
        {
        }
    }
}
