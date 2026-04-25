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
        private readonly HeartbeatPulse heartbeatPulse;
        private readonly StatHolder statHolder;

        private bool isBeating;

        public BeatingHeart(StatsConfig config, HeartbeatPulse heartbeatPulse, StatHolder statHolder)
        {
            this.config = config;
            this.heartbeatPulse = heartbeatPulse;
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
            if (!isBeating || statHolder.Icon == null || heartbeatPulse == null)
            {
                return;
            }

            Bpm = heartbeatPulse.Bpm;
            var scale = Mathf.Lerp(config.HeartDefSize, config.HeartMaxSize, heartbeatPulse.NormalizedPulse);

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
