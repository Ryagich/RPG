using System;
using Stats;
using UniRx;
using UnityEngine;

namespace UI
{
    public class BeatingHeart : IDisposable
    {
        public float Bpm { get; private set; }

        private readonly CompositeDisposable disposables = new();
        private readonly StatsConfig config;
        private readonly Stat stat;
        private readonly StatFiller filler;
        private readonly StatHolder statHolder;

        private float t;
        private bool isBeating;

        public BeatingHeart(StatsConfig config, Stat stat, StatFiller filler, StatHolder statHolder)
        {
            this.config = config;
            this.stat = stat;
            this.filler = filler;
            this.statHolder = statHolder;

            SetHeartScale(config.HeartDefSize);
            StartBeating();

            Observable.EveryUpdate().Subscribe(_ => Tick()).AddTo(disposables);
        }

        public void StartBeating()
        {
            isBeating = true;
        }

        public void Dispose()
        {
            SetHeartScale(config.HeartDefSize);
            disposables.Dispose();
        }

        private void Tick()
        {
            if (!isBeating || statHolder.Icon == null || Mathf.Approximately(stat.Max, 0f))
            {
                return;
            }

            var missingHealthNormalized = 1f - Mathf.Clamp01(filler.Current.Value / stat.Max);
            Bpm = config.MinHeartbeat + (config.MaxHeartbeat - config.MinHeartbeat) * missingHealthNormalized;

            t += (Bpm / 60f) * Time.deltaTime * Mathf.PI * 2f;

            var normalized = (Mathf.Pow(Mathf.Sin(t), config.Sharpness) + 1f) * 0.5f;
            var scale = Mathf.Lerp(config.HeartDefSize, config.HeartMaxSize, normalized);

            SetHeartScale(scale);
        }

        private void SetHeartScale(float scale)
        {
            if (statHolder.Icon == null)
            {
                return;
            }

            statHolder.Icon.transform.localScale = Vector3.one * scale;
        }
    }
}
