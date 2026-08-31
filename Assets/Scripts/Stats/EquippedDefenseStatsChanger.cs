using System;
using Combat;
using Inventory.Inventories;
using Inventory.Slot;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Stats
{
    public class EquippedDefenseStatsChanger : IStartable, IDisposable
    {
        private readonly IEquipmentInventory inventory;
        private readonly StatsController statsController;
        private readonly IDisposable inventoryChangedSubscription;

        private DefenseBonuses appliedBonuses;
        private bool isRefreshDelayed;

        public EquippedDefenseStatsChanger(IEquipmentInventory inventory, StatsController statsController)
        {
            this.inventory = inventory;
            this.statsController = statsController;
            inventoryChangedSubscription = inventory.Changed.Subscribe(_ => OnInventoryChanged());
        }

        public void Start()
        {
            RefreshBonuses();
        }

        public void Dispose()
        {
            inventoryChangedSubscription.Dispose();
        }

        public void BeginDelayedRefresh()
        {
            isRefreshDelayed = true;
        }

        public void ApplyDelayedRefresh()
        {
            if (!isRefreshDelayed)
            {
                return;
            }

            isRefreshDelayed = false;
            RefreshBonuses();
        }

        private void OnInventoryChanged()
        {
            if (isRefreshDelayed)
            {
                return;
            }

            RefreshBonuses();
        }

        private void RefreshBonuses()
        {
            var currentBonuses =
                GetBonuses(inventory.HelmSlot)
                + GetBonuses(inventory.FaceSlot)
                + GetBonuses(inventory.BodySlot)
                + GetBonuses(inventory.HandsSlot)
                + GetBonuses(inventory.ArmsSlot)
                + GetBonuses(inventory.LegsSlot)
                + GetBonuses(inventory.HipsSlot);

            var currentPhysicalDefense = PhysicalDefenseCalculator.CalculateEffective(inventory);
            ApplyValue(StatType.PhysicalDefense, currentPhysicalDefense);
            ApplyDelta(StatType.TemperatureDefense, currentBonuses.TemperatureDefense - appliedBonuses.TemperatureDefense);
            ApplyDelta(StatType.PsiDefense, currentBonuses.PsiDefense - appliedBonuses.PsiDefense);
            ApplyDelta(StatType.MagicDefense, currentBonuses.MagicDefense - appliedBonuses.MagicDefense);

            appliedBonuses = new DefenseBonuses(
                currentPhysicalDefense,
                currentBonuses.TemperatureDefense,
                currentBonuses.PsiDefense,
                currentBonuses.MagicDefense);
        }

        private void ApplyDelta(StatType statType, float delta)
        {
            if (Mathf.Approximately(delta, 0f))
            {
                return;
            }

            statsController.AddValue(statType, delta);
        }

        private void ApplyValue(StatType statType, float value)
        {
            var stat = statsController.GetStat(statType);
            if (Mathf.Approximately(stat.Value.Value, value))
            {
                return;
            }

            statsController.ChangeValue(statType, value);
        }

        private static DefenseBonuses GetBonuses(SlotModel slotModel)
        {
            if (slotModel?.ItemStack?.ItemConfig == null)
            {
                return default;
            }

            var itemConfig = slotModel.ItemStack.ItemConfig;
            var count = Mathf.Max(0, slotModel.ItemStack.Count);
            return new DefenseBonuses(
                0f,
                itemConfig.TemperatureDefense * count,
                itemConfig.PsiDefense * count,
                itemConfig.MagicDefense * count);
        }

        private readonly struct DefenseBonuses
        {
            public readonly float PhysicalDefense;
            public readonly float TemperatureDefense;
            public readonly float PsiDefense;
            public readonly float MagicDefense;

            public DefenseBonuses(float physicalDefense, float temperatureDefense, float psiDefense, float magicDefense)
            {
                PhysicalDefense = physicalDefense;
                TemperatureDefense = temperatureDefense;
                PsiDefense = psiDefense;
                MagicDefense = magicDefense;
            }

            public static DefenseBonuses operator +(DefenseBonuses left, DefenseBonuses right)
            {
                return new DefenseBonuses(
                    left.PhysicalDefense + right.PhysicalDefense,
                    left.TemperatureDefense + right.TemperatureDefense,
                    left.PsiDefense + right.PsiDefense,
                    left.MagicDefense + right.MagicDefense);
            }
        }
    }
}
