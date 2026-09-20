using System;
using UnityEngine;

namespace Advertising
{
    [CreateAssetMenu(fileName = "Advertising Config", menuName = "Configs/Advertising Config")]
    public sealed class AdvertisingConfig : ScriptableObject
    {
        [SerializeField] private bool enabledForBuild;
        [SerializeField] private UtcDate advertisingStartDate = new(2026, 9, 20);
        [SerializeField, Min(0f)] private float interstitialCooldownSeconds = 60f;
        [SerializeField, Min(0f)] private float rewardedCooldownSeconds = 300f;
        [SerializeField, Min(0f)] private float newGameGracePeriodSeconds = 300f;

        public float InterstitialCooldownSeconds => interstitialCooldownSeconds;
        public float RewardedCooldownSeconds => rewardedCooldownSeconds;
        public float NewGameGracePeriodSeconds => newGameGracePeriodSeconds;

        public bool IsAvailableAt(DateTimeOffset utcNow)
        {
            return enabledForBuild &&
                   advertisingStartDate.TryGetStartOfDay(out DateTimeOffset startUtc) &&
                   utcNow >= startUtc;
        }
    }

    [Serializable]
    public struct UtcDate
    {
        [SerializeField, Min(1)] private int year;
        [SerializeField, Range(1, 12)] private int month;
        [SerializeField, Range(1, 31)] private int day;

        public UtcDate(int year, int month, int day)
        {
            this.year = year;
            this.month = month;
            this.day = day;
        }

        public bool TryGetStartOfDay(out DateTimeOffset result)
        {
            try
            {
                result = new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                result = default;
                return false;
            }
        }
    }
}
