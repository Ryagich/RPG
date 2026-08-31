using UniRx;

namespace Money
{
    public class MoneyStorage
    {
        public IReadOnlyReactiveProperty<int> CurrentMoney => currentMoney;
        public bool HasUnlimitedFunds { get; }

        private readonly ReactiveProperty<int> currentMoney;

        public MoneyStorage(int startMoney)
            : this(startMoney, false)
        {
        }

        private MoneyStorage(int startMoney, bool hasUnlimitedFunds)
        {
            currentMoney = new ReactiveProperty<int>(ClampMoney(startMoney));
            HasUnlimitedFunds = hasUnlimitedFunds;
        }

        public static MoneyStorage CreateUnlimited()
        {
            return new MoneyStorage(0, true);
        }

        public bool CanSpend(int amount)
        {
            return amount >= 0 && (HasUnlimitedFunds || currentMoney.Value >= amount);
        }

        public bool TrySpend(int amount)
        {
            if (!CanSpend(amount))
            {
                return false;
            }

            if (!HasUnlimitedFunds)
            {
                currentMoney.Value -= amount;
            }

            return true;
        }

        public int SpendUpTo(int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            if (HasUnlimitedFunds)
            {
                return amount;
            }

            int spentAmount = currentMoney.Value >= amount ? amount : currentMoney.Value;
            currentMoney.Value -= spentAmount;
            return spentAmount;
        }

        public void Add(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            if (!HasUnlimitedFunds)
            {
                currentMoney.Value += amount;
            }
        }

        public int GetAffordableItemCount(int itemPrice, int requestedCount)
        {
            if (requestedCount <= 0)
            {
                return 0;
            }

            if (itemPrice <= 0 || HasUnlimitedFunds)
            {
                return requestedCount;
            }

            int affordableCount = currentMoney.Value / itemPrice;
            return affordableCount >= requestedCount ? requestedCount : affordableCount;
        }

        private static int ClampMoney(int value)
        {
            return value < 0 ? 0 : value;
        }
    }
}
