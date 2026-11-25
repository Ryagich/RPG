using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;
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
            Invoke(offerInventory, "TryAdd", Create("Inventory.Item.ItemStack", itemConfig, 2, false, null));
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
            Invoke(targetOfferInventory, "TryAdd", Create("Inventory.Item.ItemStack", itemConfig, 2, false, null));
            var service = Create("Trade.TradeOfferService", playerInventory, null);

            Invoke(service, "ReturnAllOfferItems", session, null);

            Assert.That(GetTotalItemCount(targetOfferInventory), Is.Zero);
            Assert.That(GetTotalItemCount(targetInventory), Is.EqualTo(2));
        }

        [Test]
        public void TradeDoubleClickMovesMerchantItemToOfferAndBack()
        {
            var playerInventory = Create(
                "Inventory.Inventories.PlayerInventory",
                CreateScriptableObject("Inventory.InventoryConfig"));
            var merchantInventory = Create("Inventory.Inventories.TradeSellInventory");
            var itemConfig = CreateScriptableObject("Inventory.Item.ItemConfig");
            var session = Create("Trade.TradeSession", playerInventory, merchantInventory);
            var offerService = Create("Trade.TradeOfferService", playerInventory, null);
            var actionService = Create("Trade.TradeDoubleClickActionService", playerInventory, offerService);
            Invoke(merchantInventory, "TryAdd", Create("Inventory.Item.ItemStack", itemConfig, 1, false, null));

            var movedToOffer = (bool)Invoke(
                actionService,
                "TryMoveInventoryItem",
                session,
                merchantInventory,
                GetFirstItem(merchantInventory),
                null);

            var targetOfferInventory = GetProperty(GetProperty(session, "TargetOffer"), "Inventory");
            Assert.That(movedToOffer, Is.True);
            Assert.That(GetTotalItemCount(merchantInventory), Is.Zero);
            Assert.That(GetTotalItemCount(targetOfferInventory), Is.EqualTo(1));

            var returnedToMerchant = (bool)Invoke(
                actionService,
                "TryMoveInventoryItem",
                session,
                targetOfferInventory,
                GetFirstItem(targetOfferInventory),
                null);

            Assert.That(returnedToMerchant, Is.True);
            Assert.That(GetTotalItemCount(targetOfferInventory), Is.Zero);
            Assert.That(GetTotalItemCount(merchantInventory), Is.EqualTo(1));
        }

        [Test]
        public void TradeDoubleClickMovesPlayerGridItemToPlayerOffer()
        {
            var playerInventory = Create(
                "Inventory.Inventories.PlayerInventory",
                CreateScriptableObject("Inventory.InventoryConfig"));
            var merchantInventory = Create("Inventory.Inventories.TradeSellInventory");
            var itemConfig = CreateScriptableObject("Inventory.Item.ItemConfig");
            var session = Create("Trade.TradeSession", playerInventory, merchantInventory);
            var offerService = Create("Trade.TradeOfferService", playerInventory, null);
            var actionService = Create("Trade.TradeDoubleClickActionService", playerInventory, offerService);
            Invoke(playerInventory, "TryAddToGrid", Create("Inventory.Item.ItemStack", itemConfig, 1, false, null));

            var moved = (bool)Invoke(
                actionService,
                "TryMoveInventoryItem",
                session,
                playerInventory,
                GetFirstItem(playerInventory),
                null);

            var playerOfferInventory = GetProperty(GetProperty(session, "PlayerOffer"), "Inventory");
            Assert.That(moved, Is.True);
            Assert.That(GetTotalItemCount(playerInventory), Is.Zero);
            Assert.That(GetTotalItemCount(playerOfferInventory), Is.EqualTo(1));
        }

        [Test]
        public void TradeDoubleClickReturnsPlayerOfferToEmptyCompatibleSlotBeforeGrid()
        {
            var playerInventory = Create(
                "Inventory.Inventories.PlayerInventory",
                CreateScriptableObject("Inventory.InventoryConfig"));
            var merchantInventory = Create("Inventory.Inventories.TradeSellInventory");
            var helmConfig = CreateScriptableObject("Inventory.Item.ItemConfig");
            SetAutoProperty(helmConfig, "ItemType", Enum.Parse(GetRuntimeType("Inventory.Item.ItemType"), "Helm"));
            var session = Create("Trade.TradeSession", playerInventory, merchantInventory);
            var offerService = Create("Trade.TradeOfferService", playerInventory, null);
            var actionService = Create("Trade.TradeDoubleClickActionService", playerInventory, offerService);
            var playerOfferInventory = GetProperty(GetProperty(session, "PlayerOffer"), "Inventory");
            Invoke(playerOfferInventory, "TryAdd", Create("Inventory.Item.ItemStack", helmConfig, 1, false, null));

            var returned = (bool)Invoke(
                actionService,
                "TryMoveInventoryItem",
                session,
                playerOfferInventory,
                GetFirstItem(playerOfferInventory),
                null);

            var helmSlot = GetField(playerInventory, "HelmSlot");
            Assert.That(returned, Is.True);
            Assert.That(GetField(helmSlot, "ItemStack"), Is.Not.Null);
            Assert.That(GetTotalItemCount(playerInventory), Is.Zero);
            Assert.That(GetTotalItemCount(playerOfferInventory), Is.Zero);
        }

        [Test]
        public void TradeDoubleClickReturnsPlayerOfferToGridWhenCompatibleSlotIsOccupied()
        {
            var playerInventory = Create(
                "Inventory.Inventories.PlayerInventory",
                CreateScriptableObject("Inventory.InventoryConfig"));
            var merchantInventory = Create("Inventory.Inventories.TradeSellInventory");
            var helmConfig = CreateScriptableObject("Inventory.Item.ItemConfig");
            SetAutoProperty(helmConfig, "ItemType", Enum.Parse(GetRuntimeType("Inventory.Item.ItemType"), "Helm"));
            var helmSlot = GetField(playerInventory, "HelmSlot");
            SetField(helmSlot, "ItemStack", Create("Inventory.Item.ItemStack", helmConfig, 1, false, null));
            var session = Create("Trade.TradeSession", playerInventory, merchantInventory);
            var offerService = Create("Trade.TradeOfferService", playerInventory, null);
            var actionService = Create("Trade.TradeDoubleClickActionService", playerInventory, offerService);
            var playerOfferInventory = GetProperty(GetProperty(session, "PlayerOffer"), "Inventory");
            Invoke(playerOfferInventory, "TryAdd", Create("Inventory.Item.ItemStack", helmConfig, 1, false, null));

            var returned = (bool)Invoke(
                actionService,
                "TryMoveInventoryItem",
                session,
                playerOfferInventory,
                GetFirstItem(playerOfferInventory),
                null);

            Assert.That(returned, Is.True);
            Assert.That(GetField(helmSlot, "ItemStack"), Is.Not.Null);
            Assert.That(GetTotalItemCount(playerInventory), Is.EqualTo(1));
            Assert.That(GetTotalItemCount(playerOfferInventory), Is.Zero);
        }

        [Test]
        public void TradeDoubleClickKeepsPlayerOfferItemWhenSlotAndGridCannotAcceptIt()
        {
            var playerInventory = Create(
                "Inventory.Inventories.PlayerInventory",
                CreateScriptableObject("Inventory.InventoryConfig"));
            var merchantInventory = Create("Inventory.Inventories.TradeSellInventory");
            var helmConfig = CreateScriptableObject("Inventory.Item.ItemConfig");
            SetAutoProperty(helmConfig, "ItemType", Enum.Parse(GetRuntimeType("Inventory.Item.ItemType"), "Helm"));
            var blockingConfig = CreateScriptableObject("Inventory.Item.ItemConfig");
            SetAutoProperty(blockingConfig, "Size", new Vector2Int(7, 2));
            Invoke(playerInventory, "TryAddToGrid", Create("Inventory.Item.ItemStack", blockingConfig, 1, false, null));
            var helmSlot = GetField(playerInventory, "HelmSlot");
            SetField(helmSlot, "ItemStack", Create("Inventory.Item.ItemStack", helmConfig, 1, false, null));
            var session = Create("Trade.TradeSession", playerInventory, merchantInventory);
            var offerService = Create("Trade.TradeOfferService", playerInventory, null);
            var actionService = Create("Trade.TradeDoubleClickActionService", playerInventory, offerService);
            var playerOfferInventory = GetProperty(GetProperty(session, "PlayerOffer"), "Inventory");
            Invoke(playerOfferInventory, "TryAdd", Create("Inventory.Item.ItemStack", helmConfig, 1, false, null));

            var returned = (bool)Invoke(
                actionService,
                "TryMoveInventoryItem",
                session,
                playerOfferInventory,
                GetFirstItem(playerOfferInventory),
                null);

            Assert.That(returned, Is.False);
            Assert.That(GetField(helmSlot, "ItemStack"), Is.Not.Null);
            Assert.That(GetTotalItemCount(playerInventory), Is.EqualTo(1));
            Assert.That(GetTotalItemCount(playerOfferInventory), Is.EqualTo(1));
        }

        [Test]
        public void TradeDoubleClickMovesPlayerSlotToGridThenFallsBackToPlayerOffer()
        {
            var playerInventory = Create(
                "Inventory.Inventories.PlayerInventory",
                CreateScriptableObject("Inventory.InventoryConfig"));
            var merchantInventory = Create("Inventory.Inventories.TradeSellInventory");
            var helmConfig = CreateScriptableObject("Inventory.Item.ItemConfig");
            SetAutoProperty(helmConfig, "ItemType", Enum.Parse(GetRuntimeType("Inventory.Item.ItemType"), "Helm"));
            var session = Create("Trade.TradeSession", playerInventory, merchantInventory);
            var offerService = Create("Trade.TradeOfferService", playerInventory, null);
            var actionService = Create("Trade.TradeDoubleClickActionService", playerInventory, offerService);
            var helmSlot = GetField(playerInventory, "HelmSlot");
            SetField(helmSlot, "ItemStack", Create("Inventory.Item.ItemStack", helmConfig, 1, false, null));

            var movedToGrid = (bool)Invoke(actionService, "TryMovePlayerSlot", session, helmSlot);

            Assert.That(movedToGrid, Is.True);
            Assert.That(GetField(helmSlot, "ItemStack"), Is.Null);
            Assert.That(GetTotalItemCount(playerInventory), Is.EqualTo(1));

            var blockingConfig = CreateScriptableObject("Inventory.Item.ItemConfig");
            SetAutoProperty(blockingConfig, "Size", new Vector2Int(7, 2));
            var fullPlayerInventory = Create(
                "Inventory.Inventories.PlayerInventory",
                CreateScriptableObject("Inventory.InventoryConfig"));
            var fullSession = Create("Trade.TradeSession", fullPlayerInventory, merchantInventory);
            var fullOfferService = Create("Trade.TradeOfferService", fullPlayerInventory, null);
            var fullActionService = Create("Trade.TradeDoubleClickActionService", fullPlayerInventory, fullOfferService);
            Invoke(fullPlayerInventory, "TryAddToGrid", Create("Inventory.Item.ItemStack", blockingConfig, 1, false, null));
            var fullHelmSlot = GetField(fullPlayerInventory, "HelmSlot");
            SetField(fullHelmSlot, "ItemStack", Create("Inventory.Item.ItemStack", helmConfig, 1, false, null));

            var movedToOffer = (bool)Invoke(fullActionService, "TryMovePlayerSlot", fullSession, fullHelmSlot);

            var fullPlayerOfferInventory = GetProperty(GetProperty(fullSession, "PlayerOffer"), "Inventory");
            Assert.That(movedToOffer, Is.True);
            Assert.That(GetField(fullHelmSlot, "ItemStack"), Is.Null);
            Assert.That(GetTotalItemCount(fullPlayerOfferInventory), Is.EqualTo(1));
        }

        [Test]
        public void LootTransferMovesTargetStackIntoPlayerInventory()
        {
            var playerInventory = Create(
                "Inventory.Inventories.PlayerInventory",
                CreateScriptableObject("Inventory.InventoryConfig"));
            var targetInventory = Create("Inventory.Inventories.TradeSellInventory");
            var itemConfig = CreateScriptableObject("Inventory.Item.ItemConfig");
            Invoke(targetInventory, "TryAdd", Create("Inventory.Item.ItemStack", itemConfig, 1, false, null));
            var targetItem = GetFirstItem(targetInventory);
            var service = Create("Inventory.Looting.LootTransferService", playerInventory, null);

            var moved = (bool)Invoke(service, "TryMoveInventoryItem", targetInventory, targetInventory, targetItem, 1);

            Assert.That(moved, Is.True);
            Assert.That(GetTotalItemCount(targetInventory), Is.Zero);
            Assert.That(GetTotalItemCount(playerInventory), Is.EqualTo(1));
        }

        [Test]
        public void LootingItemActionUsesItemWhenTransferCannotMoveAnyOfItsStack()
        {
            var playerInventory = Create(
                "Inventory.Inventories.PlayerInventory",
                CreateScriptableObject("Inventory.InventoryConfig"));
            var targetInventory = Create(
                "Inventory.Inventories.ChestInventory",
                CreateScriptableObject("Inventory.InventoryConfig"));
            var blockingConfig = CreateScriptableObject("Inventory.Item.ItemConfig");
            SetAutoProperty(blockingConfig, "Size", new Vector2Int(7, 2));
            Invoke(targetInventory, "TryAdd", Create("Inventory.Item.ItemStack", blockingConfig, 1, false, null));

            var itemConfig = CreateScriptableObject("Inventory.Item.ItemConfig");
            SetAutoProperty(itemConfig, "ItemType", Enum.Parse(GetRuntimeType("Inventory.Item.ItemType"), "Helm"));
            Invoke(playerInventory, "TryAddToGrid", Create("Inventory.Item.ItemStack", itemConfig, 1, false, null));
            var itemInInventory = GetFirstItem(playerInventory);
            var lootTransferService = Create("Inventory.Looting.LootTransferService", playerInventory, null);
            var wasUseRequested = false;
            var itemActionService = Create(
                "Inventory.Looting.LootingDoubleClickActionService",
                lootTransferService,
                CreateUseCallback(() => wasUseRequested = true),
                CreateSlotMoveCallback(() => false));

            var handled = (bool)Invoke(
                itemActionService,
                "TryMoveOrUseInventoryItem",
                targetInventory,
                playerInventory,
                itemInInventory,
                1,
                null);

            Assert.That(handled, Is.True);
            Assert.That(wasUseRequested, Is.True);
            Assert.That(GetTotalItemCount(playerInventory), Is.EqualTo(1));
        }

        [Test]
        public void LootingSlotActionMovesItemToPlayerGridWhenTransferCannotMoveIt()
        {
            var playerInventory = Create(
                "Inventory.Inventories.PlayerInventory",
                CreateScriptableObject("Inventory.InventoryConfig"));
            var targetInventory = Create(
                "Inventory.Inventories.ChestInventory",
                CreateScriptableObject("Inventory.InventoryConfig"));
            var blockingConfig = CreateScriptableObject("Inventory.Item.ItemConfig");
            SetAutoProperty(blockingConfig, "Size", new Vector2Int(7, 2));
            Invoke(targetInventory, "TryAdd", Create("Inventory.Item.ItemStack", blockingConfig, 1, false, null));
            var slot = GetField(playerInventory, "HelmSlot");
            var itemConfig = CreateScriptableObject("Inventory.Item.ItemConfig");
            SetAutoProperty(itemConfig, "ItemType", Enum.Parse(GetRuntimeType("Inventory.Item.ItemType"), "Helm"));
            SetField(slot, "ItemStack", Create("Inventory.Item.ItemStack", itemConfig, 1, false, null));
            var lootTransferService = Create("Inventory.Looting.LootTransferService", playerInventory, null);
            var wasGridMoveRequested = false;
            var itemActionService = Create(
                "Inventory.Looting.LootingDoubleClickActionService",
                lootTransferService,
                CreateUseCallback(() => false),
                CreateSlotMoveCallback(() => wasGridMoveRequested = true));

            var handled = (bool)Invoke(itemActionService, "TryMoveOrStorePlayerSlot", targetInventory, slot, 1);

            Assert.That(handled, Is.True);
            Assert.That(wasGridMoveRequested, Is.True);
        }

        [Test]
        public void InventoryItemDoubleClickTrackerRecognizesRecreatedItemAtSamePosition()
        {
            var inventory = Create("Inventory.Inventories.TradeSellInventory");
            var itemConfig = CreateScriptableObject("Inventory.Item.ItemConfig");
            var firstItem = Create("Inventory.Item.ItemInInventory", Create("Inventory.Item.ItemStack", itemConfig, 1, false, null), Matrix4x4.identity);
            var recreatedItem = Create("Inventory.Item.ItemInInventory", Create("Inventory.Item.ItemStack", itemConfig, 1, false, null), Matrix4x4.identity);
            var tracker = Create("UI.Inventory.InventoryItemDoubleClickTracker");

            var firstClick = (bool)Invoke(tracker, "RegisterClick", inventory, firstItem, 1f, .3f);
            var secondClick = (bool)Invoke(tracker, "RegisterClick", inventory, recreatedItem, 1.2f, .3f);

            Assert.That(firstClick, Is.False);
            Assert.That(secondClick, Is.True);
        }

        [Test]
        public void ChestAutoPlacementRotatesItemWhenOnlyRotatedOrientationFits()
        {
            var inventory = Create(
                "Inventory.Inventories.ChestInventory",
                CreateScriptableObject("Inventory.InventoryConfig"));
            var itemConfig = CreateScriptableObject("Inventory.Item.ItemConfig");
            SetAutoProperty(itemConfig, "Size", new Vector2Int(1, 7));

            var remainder = Invoke(inventory, "TryAdd", Create("Inventory.Item.ItemStack", itemConfig, 1, false, null));

            Assert.That(remainder, Is.Null);
            var placedStack = GetField(GetFirstItem(inventory), "ItemStack");
            Assert.That(GetField(placedStack, "IsRotated"), Is.EqualTo(true));
        }

        [Test]
        public void InventorySlotDoubleClickTrackerRecognizesRepeatedClickOnSameSlot()
        {
            var playerInventory = Create(
                "Inventory.Inventories.PlayerInventory",
                CreateScriptableObject("Inventory.InventoryConfig"));
            var slot = GetField(playerInventory, "HelmSlot");
            var itemConfig = CreateScriptableObject("Inventory.Item.ItemConfig");
            SetField(slot, "ItemStack", Create("Inventory.Item.ItemStack", itemConfig, 1, false, null));
            var tracker = Create("UI.Inventory.InventorySlotDoubleClickTracker");

            var firstClick = (bool)Invoke(tracker, "RegisterClick", slot, 1f, .3f);
            var secondClick = (bool)Invoke(tracker, "RegisterClick", slot, 1.2f, .3f);

            Assert.That(firstClick, Is.False);
            Assert.That(secondClick, Is.True);
        }

        [Test]
        public void MovingSlotItemToGridRotatesItWhenOnlyRotatedOrientationFits()
        {
            var playerInventory = Create(
                "Inventory.Inventories.PlayerInventory",
                CreateScriptableObject("Inventory.InventoryConfig"));
            var slot = GetField(playerInventory, "HelmSlot");
            var itemConfig = CreateScriptableObject("Inventory.Item.ItemConfig");
            SetAutoProperty(itemConfig, "Size", new Vector2Int(1, 7));
            SetField(slot, "ItemStack", Create("Inventory.Item.ItemStack", itemConfig, 1, false, null));

            var moved = (bool)Invoke(playerInventory, "TryMoveSlotItemToGrid", slot);

            Assert.That(moved, Is.True);
            Assert.That(GetField(slot, "ItemStack"), Is.Null);
            var placedStack = GetField(GetFirstItem(playerInventory), "ItemStack");
            Assert.That(GetField(placedStack, "IsRotated"), Is.EqualTo(true));
        }

        [Test]
        public void NonInteractivePopupDisablesChildCanvasGroupsThatIgnoreTheirParent()
        {
            var root = new GameObject("Popup Root", typeof(RectTransform), typeof(CanvasGroup));
            var child = new GameObject("Popup Child", typeof(RectTransform), typeof(CanvasGroup));
            child.transform.SetParent(root.transform, false);
            var childGroup = child.GetComponent<CanvasGroup>();
            childGroup.ignoreParentGroups = true;
            childGroup.blocksRaycasts = true;
            childGroup.interactable = true;

            InvokeStatic("UI.Pages.PageUiUtilities", "SetPopupRaycastState", root.GetComponent<RectTransform>(), false);

            Assert.That(root.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(childGroup.blocksRaycasts, Is.False);
            Assert.That(childGroup.interactable, Is.False);

            UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void PlacingItemIntoSlotRotatesItToMatchSlotGeometry()
        {
            var playerInventory = Create(
                "Inventory.Inventories.PlayerInventory",
                CreateScriptableObject("Inventory.InventoryConfig"));
            var slot = GetField(playerInventory, "HelmSlot");
            var itemConfig = CreateScriptableObject("Inventory.Item.ItemConfig");
            SetAutoProperty(itemConfig, "ItemType", Enum.Parse(GetRuntimeType("Inventory.Item.ItemType"), "Helm"));
            SetAutoProperty(itemConfig, "Size", new Vector2Int(1, 7));
            var itemStack = Create("Inventory.Item.ItemStack", itemConfig, 1, false, null);
            var placementMethod = GetRuntimeType("Inventory.Inventories.PlayerInventory")
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Single(method => method.Name == "TryPlaceInSlot"
                                  && method.GetParameters().Length == 5
                                  && method.GetParameters()[0].ParameterType.FullName == "Inventory.Slot.SlotModel");

            var placed = (bool)placementMethod.Invoke(
                playerInventory,
                new object[] { slot, itemStack, (Vector2?)new Vector2(100f, 50f), null, null });

            Assert.That(placed, Is.True);
            var placedStack = GetField(slot, "ItemStack");
            Assert.That(GetField(placedStack, "IsRotated"), Is.EqualTo(true));
        }

        private static object Create(string typeName, params object[] arguments)
        {
            return Activator.CreateInstance(GetRuntimeType(typeName), arguments);
        }

        private static Delegate CreateUseCallback(Func<bool> callback)
        {
            var inventoryParameter = Expression.Parameter(GetRuntimeType("Inventory.Inventories.IInventory"));
            var itemParameter = Expression.Parameter(GetRuntimeType("Inventory.Item.ItemInInventory"));
            var callbackInvoke = Expression.Call(Expression.Constant(callback), typeof(Func<bool>).GetMethod(nameof(Func<bool>.Invoke))!);
            var callbackType = typeof(Func<,,>).MakeGenericType(
                inventoryParameter.Type,
                itemParameter.Type,
                typeof(bool));
            return Expression.Lambda(callbackType, callbackInvoke, inventoryParameter, itemParameter).Compile();
        }

        private static Delegate CreateSlotMoveCallback(Func<bool> callback)
        {
            var slotParameter = Expression.Parameter(GetRuntimeType("Inventory.Slot.SlotModel"));
            var callbackInvoke = Expression.Call(Expression.Constant(callback), typeof(Func<bool>).GetMethod(nameof(Func<bool>.Invoke))!);
            var callbackType = typeof(Func<,>).MakeGenericType(slotParameter.Type, typeof(bool));
            return Expression.Lambda(callbackType, callbackInvoke, slotParameter).Compile();
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

        private static object InvokeStatic(string typeName, string methodName, params object[] arguments)
        {
            return GetRuntimeType(typeName).InvokeMember(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.InvokeMethod,
                null,
                null,
                arguments);
        }

        private static object GetProperty(object target, string propertyName)
        {
            return target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)?.GetValue(target)
                   ?? throw new InvalidOperationException($"Property '{propertyName}' was not found.");
        }

        private static void SetAutoProperty(object target, string propertyName, object value)
        {
            var field = target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new InvalidOperationException($"Auto-property backing field for '{propertyName}' was not found.");
            }

            field.SetValue(target, value);
        }

        private static object GetField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            if (field == null)
            {
                throw new InvalidOperationException($"Field '{fieldName}' was not found.");
            }

            return field.GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            if (field == null)
            {
                throw new InvalidOperationException($"Field '{fieldName}' was not found.");
            }

            field.SetValue(target, value);
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
                var itemStack = GetField(item, "ItemStack");
                count += (int)GetField(itemStack, "Count");
            }

            return count;
        }

        private static int GetCurrentMoney(object moneyStorage)
        {
            return (int)GetProperty(GetProperty(moneyStorage, "CurrentMoney"), "Value");
        }
    }
}
