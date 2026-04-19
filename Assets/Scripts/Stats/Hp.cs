using System;
using UnityEngine;

namespace Stats
{
    [Serializable]
    public class Hp : PeriodicStat
    {
        [field: SerializeField, Min(0f)] public float RegenResumeDelayAfterDamageSeconds { get; private set; } = 0f;

        public Hp(
            float max,
            float min,
            float value,
            float periodicChange,
            float regenResumeDelayAfterDamageSeconds,
            float minSafePercent = 0.15f)
            : base(max, min, value, periodicChange, minSafePercent)
        {
            RegenResumeDelayAfterDamageSeconds = Mathf.Max(0f, regenResumeDelayAfterDamageSeconds);
        }

        public Hp(Hp oldStat)
            : this(
                oldStat.Max,
                oldStat.Min,
                oldStat.Value.Value,
                oldStat.PeriodicChange,
                oldStat.RegenResumeDelayAfterDamageSeconds,
                oldStat.MinSafePercent)
        {
        }
    }
}
