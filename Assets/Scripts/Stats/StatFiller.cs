using UI;
using UniRx;
using UnityEngine;

namespace Stats
{
    public class StatFiller : System.IDisposable
    {
        public StatType StatType { get; }
        public ReactiveProperty<float> Current { get; private set; } = new();
        public float NormalizedCurrent => Mathf.Approximately(stat.Max, 0f) ? 0f : Current.Value / stat.Max;

        private readonly CompositeDisposable disposables = new();
        private readonly StatsConfig config;
        private readonly Stat stat;

        private float target;

        public StatFiller(StatType statType, StatsConfig config, StatsController statsController)
        {
            StatType = statType;
            this.config = config;
            stat = statsController.GetStat(statType);

            Current.Value = stat.Value.Value;
            target = Current.Value;

            stat.Value.Subscribe(OnTargetChanged).AddTo(disposables);
            Observable.EveryUpdate().Subscribe(_ => Tick()).AddTo(disposables);
        }

        public void Dispose()
        {
            disposables.Dispose();
        }

        private void OnTargetChanged(float value)
        {
            target = value;
        }

        private void Tick()
        {
            if (Mathf.Approximately(Current.Value, target))
            {
                return;
            }

            Current.Value = Mathf.Lerp(Current.Value, target, config.FllLerpSpeed);
            Current.Value = Mathf.MoveTowards(Current.Value, target, config.FillSpeed * Time.deltaTime);
        }
    }
}
