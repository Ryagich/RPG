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
        [field: SerializeField] public FloatReactiveProperty Value { get; private set; }

        public Stat(float max, float min, float value)
        {
            Max = max;
            Min = min;
            Value = new FloatReactiveProperty(Mathf.Clamp(value, Min, Max));
        }

        public Stat(Stat oldStat) : this(oldStat.Max, oldStat.Min, oldStat.Value.Value) { }

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
