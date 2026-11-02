using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Exercises public application-service contracts from the legacy default runtime assembly.
    /// The project has no runtime asmdef yet, so a test asmdef cannot statically reference it.
    /// </summary>
    public sealed class TradeAndLootServicesEditModeTests
    {
        [Test]
        public void SettlementTransfersAffordableOfferItemsAndPaysSeller()
        {
            var itemConfig = CreateScriptableObject("Inventory.Item.ItemConfig");
            var ownerInventory = Create("Inventory.Inventories.TradeSellInventory");
            var offer = Create("Trade.TradeOffer", ownerInventory);
            var offerInventory = GetProperty(offer, "Inventory");
            Invoke(offerInventory, "TryAdd", Create("Inventory.Item.ItemStack", itemConfig, 2));
            var destination = Create("Inventory.Inventories.TradeSellInventory");
            var sellerMoney = Create("Money.MoneyStorage", 0);
            var buyerMoney = Create("Money.MoneyStorage", 2);

            Invoke(Create("Trade.TradeSettlementService"), "Settle", offer, destination, sellerMoney, buyerMoney);

            Assert.That(GetTotalItemCount(destination), Is.EqualTo(2));
            Assert.That(GetTotalItemCount(offerInventory), Is.Zero);
            Assert.That(GetCurrentMoney(sellerMoney), Is.EqualTo(2));
            Assert.That(GetCurrentMoney(buyerMoney), Is.Zero);
        }

        [Test]
        public void CancellingTradeReturnsTargetOfferThroughTargetInventory()
        {
            var playerInventory = Create(
                "Inventory.Inventories.PlayerInventory",
                CreateScriptableObject("Inventory.InventoryConfig"));
            var targetInventory = Create("Inventory.Inventories.TradeSellInventory");
            var itemConfig = CreateScriptableObject("Inventory.Item.ItemConfig");
            var session = Create("Trade.TradeSession", playerInventory, targetInventory);
            var targetOffer = GetProperty(session, "TargetOffer");
            var targetOfferInventory = GetProperty(targetOffer, "Inventory");
            Invoke(targetOfferInventory, "TryAdd", Create("Inventory.Item.ItemStack", itemConfig, 2));
            var service = Create("Trade.TradeOfferService", playerInventory, null);

            Invoke(service, "ReturnAllOfferItems", session, null);

            Assert.That(GetTotalItemCount(targetOfferInventory), Is.Zero);
            Assert.That(GetTotalItemCount(targetInventory), Is.EqualTo(2));
        }

        [Test]
        public void LootTransferMovesTargetStackIntoPlayerInventory()
        {
            var playerInventory = Create(
                "Inventory.Inventories.PlayerInventory",
                CreateScriptableObject("Inventory.InventoryConfig"));
            var targetInventory = Create("Inventory.Inventories.TradeSellInventory");
            var itemConfig = CreateScriptableObject("Inventory.Item.ItemConfig");
            Invoke(targetInventory, "TryAdd", Create("Inventory.Item.ItemStack", itemConfig, 1));
            var targetItem = GetFirstItem(targetInventory);
            var service = Create("Inventory.Looting.LootTransferService", playerInventory, null);

            var moved = (bool)Invoke(service, "TryMoveInventoryItem", targetInventory, targetInventory, targetItem, 1);

            Assert.That(moved, Is.True);
            Assert.That(GetTotalItemCount(targetInventory), Is.Zero);
            Assert.That(GetTotalItemCount(playerInventory), Is.EqualTo(1));
        }

        private static object Create(string typeName, params object[] arguments)
        {
            return Activator.CreateInstance(GetRuntimeType(typeName), arguments);
        }

        private static ScriptableObject CreateScriptableObject(string typeName)
        {
            return ScriptableObject.CreateInstance(GetRuntimeType(typeName));
        }

        private static Type GetRuntimeType(string typeName)
        {
            return Type.GetType($"{typeName}, Assembly-CSharp")
                   ?? throw new InvalidOperationException($"Runtime type '{typeName}' was not found.");
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            return target.GetType().InvokeMember(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod,
                null,
                target,
                arguments);
        }

        private static object GetProperty(object target, string propertyName)
        {
            return target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)?.GetValue(target)
                   ?? throw new InvalidOperationException($"Property '{propertyName}' was not found.");
        }

        private static object GetFirstItem(object inventory)
        {
            foreach (var item in (IEnumerable)GetProperty(inventory, "Items"))
            {
                return item;
            }

            throw new InvalidOperationException("Inventory has no items.");
        }

        private static int GetTotalItemCount(object inventory)
        {
            var count = 0;
            foreach (var item in (IEnumerable)GetProperty(inventory, "Items"))
            {
                var itemStack = GetProperty(item, "ItemStack");
                count += (int)GetProperty(itemStack, "Count");
            }

            return count;
        }

        private static int GetCurrentMoney(object moneyStorage)
        {
            return (int)GetProperty(GetProperty(moneyStorage, "CurrentMoney"), "Value");
        }
    }
}
