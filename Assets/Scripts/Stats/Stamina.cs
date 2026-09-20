using System;
using UnityEngine;

namespace Stats
{
    [Serializable]
    public class Stamina : PeriodicStat
    {
        // ReSharper disable once MemberInitializerValueIgnored
        [field: SerializeField] public float MovingRecoveryPeriodicChange { get; private set; }
        [field: SerializeField, Min(0f)] public float RegenResumeDelayAfterEmptySeconds { get; private set; } = 0f;

        [field: Space, Header("Combat costs")]
        [field: SerializeField, Min(0f)] public float LightAttackCost { get; private set; } = 10f;
        [field: SerializeField, Min(0f)] public float HeavyAttackCost { get; private set; } = 20f;
        [field: SerializeField, Min(0f)] public float DodgeCost { get; private set; } = 15f;
        [field: SerializeField, Min(0f)] public float RollCost { get; private set; } = 25f;

        public Stamina
            (
                float max,
                float min,
                float value,
                float periodicChange,
                float movingRecoveryPeriodicChange,
                float regenResumeDelayAfterEmptySeconds,
                float lightAttackCost,
                float heavyAttackCost,
                float dodgeCost,
                float rollCost,
                float minSafePercent = 0.15f
            ) : base(max, min, value, periodicChange, minSafePercent)
        {
            MovingRecoveryPeriodicChange = movingRecoveryPeriodicChange;
            RegenResumeDelayAfterEmptySeconds = Mathf.Max(0f, regenResumeDelayAfterEmptySeconds);
            LightAttackCost = Mathf.Max(0f, lightAttackCost);
            HeavyAttackCost = Mathf.Max(0f, heavyAttackCost);
            DodgeCost = Mathf.Max(0f, dodgeCost);
            RollCost = Mathf.Max(0f, rollCost);
        }

        public Stamina(Stamina oldStat) : this
            (
                oldStat.Max,
                oldStat.Min,
                oldStat.Value.Value,
                oldStat.PeriodicChange,
                oldStat.MovingRecoveryPeriodicChange,
                oldStat.RegenResumeDelayAfterEmptySeconds,
                oldStat.LightAttackCost,
                oldStat.HeavyAttackCost,
                oldStat.DodgeCost,
                oldStat.RollCost,
                oldStat.MinSafePercent
            ) { }
    }
}
