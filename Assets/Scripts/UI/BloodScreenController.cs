using System;
using Stats;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public sealed class BloodScreenController : IDisposable
    {
        private const float MaxBeatScaleOffset = 0.035f;

        private readonly CompositeDisposable disposables = new();
        private readonly StatsConfig config;
        private readonly Stat stat;
        private readonly StatFiller filler;
        private readonly HeartbeatPulse heartbeatPulse;
        private readonly Image bloodScreen;
        private readonly Color baseColor;

        public BloodScreenController(
            StatsConfig config,
            Stat stat,
            StatFiller filler,
            HeartbeatPulse heartbeatPulse,
            Image bloodScreen)
        {
            this.config = config;
            this.stat = stat;
            this.filler = filler;
            this.heartbeatPulse = heartbeatPulse;
            this.bloodScreen = bloodScreen;
            baseColor = config != null ? config.HpDecreaseColor : Color.white;

            if (bloodScreen == null)
            {
                return;
            }

            bloodScreen.raycastTarget = false;

            SetAlpha(0f);
            SetScale(1f);

            Observable.EveryUpdate().Subscribe(_ => Tick()).AddTo(disposables);
        }

        public void Dispose()
        {
            if (bloodScreen != null)
            {
                SetAlpha(0f);
                SetScale(1f);
            }

            disposables.Dispose();
        }

        private void Tick()
        {
            if (bloodScreen == null)
            {
                return;
            }

            var alpha = GetBloodAlpha();
            SetAlpha(alpha);

            if (alpha <= 0f || heartbeatPulse == null)
            {
                SetScale(1f);
                return;
            }

            var scaleOffset = Mathf.Lerp(0f, MaxBeatScaleOffset, alpha);
            var scale = Mathf.Lerp(1f + scaleOffset, 1f, heartbeatPulse.NormalizedPulse);
            SetScale(scale);
        }

        private float GetBloodAlpha()
        {
            if (Mathf.Approximately(stat.Max, 0f))
            {
                return 0f;
            }

            var criticalHp = stat.Max * config.HpStat.MinSafePercent;
            var currentHp = Mathf.Clamp(filler.Current.Value, 0f, stat.Max);

            if (criticalHp <= 0f)
            {
                return Mathf.Approximately(currentHp, 0f)
                    ? Mathf.Clamp01(config.BloodScreenAlphaMultiplier)
                    : 0f;
            }

            if (currentHp >= criticalHp)
            {
                return 0f;
            }

            var normalizedAlpha = 1f - Mathf.Clamp01(currentHp / criticalHp);
            return Mathf.Clamp01(normalizedAlpha * config.BloodScreenAlphaMultiplier);
        }

        private void SetAlpha(float alpha)
        {
            var color = baseColor;
            color.a = alpha;
            bloodScreen.color = color;
        }

        private void SetScale(float scale)
        {
            bloodScreen.transform.localScale = Vector3.one * scale;
        }
    }
}
