using System;
using UnityEngine;

namespace Stats
{
    [Serializable]
    public class SafeStat : Stat
    {
        [field: SerializeField, Range(0f, 1f)] public float MinSafePercent { get; private set; } = 0.15f;

        public SafeStat(
            float max,
            float min,
            float value,
            float minSafePercent = 0.15f)
            : base(max, min, value)
        {
            MinSafePercent = Mathf.Clamp01(minSafePercent);
        }

        public SafeStat(SafeStat oldStat)
            : this(
                oldStat.Max,
                oldStat.Min,
                oldStat.Value.Value,
                oldStat.MinSafePercent)
        {
        }
    }
}
