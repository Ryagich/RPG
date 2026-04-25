using System;
using Stats;
using UniRx;
using UnityEngine;

namespace UI
{
    public sealed class HeartbeatPulse : IDisposable
    {
        public float Bpm { get; private set; }
        public float NormalizedPulse { get; private set; } = 0.5f;

        private readonly CompositeDisposable disposables = new();
        private readonly StatsConfig config;
        private readonly Stat stat;
        private readonly StatFiller filler;

        private float t;

        public HeartbeatPulse(StatsConfig config, Stat stat, StatFiller filler)
        {
            this.config = config;
            this.stat = stat;
            this.filler = filler;

            Observable.EveryUpdate().Subscribe(_ => Tick()).AddTo(disposables);
        }

        public void Dispose()
        {
            disposables.Dispose();
        }

        private void Tick()
        {
            if (Mathf.Approximately(stat.Max, 0f))
            {
                Bpm = 0f;
                NormalizedPulse = 0.5f;
                t = 0f;
                return;
            }

            var missingHealthNormalized = 1f - Mathf.Clamp01(filler.Current.Value / stat.Max);
            var baseBpm = Mathf.Lerp(config.MinHeartbeat, config.MaxHeartbeat, missingHealthNormalized);

            Bpm = baseBpm * config.HeartbeatTempoMultiplier;
            t += (Bpm / 60f) * Time.deltaTime * Mathf.PI * 2f;
            NormalizedPulse = (Mathf.Pow(Mathf.Sin(t), config.Sharpness) + 1f) * 0.5f;
        }
    }
}
