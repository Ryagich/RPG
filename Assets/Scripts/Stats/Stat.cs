using System;
using UniRx;
using UnityEngine;

namespace Stats
{
    [Serializable]
    public class Stat
    {
        [field: SerializeField] public float Max { get; private set; }
        [field: SerializeField] public float Min { get; private set; }
        [field: SerializeField, Range(0f, 1f)] public float MinSafePercent { get; private set; } = 0.15f;
        [field: SerializeField] public FloatReactiveProperty Value { get; private set; }

        public Stat(
            float max,
            float min,
            float value,
            float minSafePercent = 0.15f)
        {
            Max = max;
            Min = min;
            MinSafePercent = Mathf.Clamp01(minSafePercent);
            Value = new FloatReactiveProperty(Mathf.Clamp(value, Min, Max));
        }

        public Stat(Stat oldStat) : this(
            oldStat.Max,
            oldStat.Min,
            oldStat.Value.Value,
            oldStat.MinSafePercent) { }

        public void AddValue(float value)
        {
            Value.Value = Mathf.Clamp(Value.Value + value, Min, Max);
        }

        public void ChangeValue(float newValue)
        {
            Value.Value = Mathf.Clamp(newValue, Min, Max);
        }

        public void ChangeMax(float newMax)
        {
            Max = newMax;
            Value.Value = Mathf.Clamp(Value.Value, Min, Max);
        }
    }
}
