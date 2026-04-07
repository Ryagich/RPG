using UniRx;

namespace Money
{
    public class MoneyStorage
    {
        public IReadOnlyReactiveProperty<int> CurrentMoney => currentMoney;

        private readonly ReactiveProperty<int> currentMoney;

        public MoneyStorage(int startMoney)
        {
            currentMoney = new ReactiveProperty<int>(ClampMoney(startMoney));
        }

        public bool CanSpend(int amount)
        {
            return amount >= 0 && currentMoney.Value >= amount;
        }

        public bool TrySpend(int amount)
        {
            if (!CanSpend(amount))
            {
                return false;
            }

            currentMoney.Value -= amount;
            return true;
        }

        public void Add(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            currentMoney.Value += amount;
        }

        private static int ClampMoney(int value)
        {
            return value < 0 ? 0 : value;
        }
    }
}