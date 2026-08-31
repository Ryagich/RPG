using System;
using Inventory.Grid;
using Inventory.Inventories;
using Inventory.Item;
using UniRx;
using UnityEngine;

namespace Inventory.Looting
{
    [DisallowMultipleComponent]
    public sealed class CorpseLootController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float followDeadZone = 0.05f;

        private IEquipmentInventory sourceInventory;
        private Player.PlayerRagdollController ragdollController;
        private CharacterVisualRoot visualRoot;
        private GridOnlyInventory lootInventory;

        public bool IsLootable { get; private set; }
        public IInventory LootInventory => lootInventory;

        [VContainer.Inject]
        public void Construct(
            IEquipmentInventory sourceInventory,
            Player.PlayerRagdollController ragdollController,
            CharacterVisualRoot visualRoot)
        {
            this.sourceInventory = sourceInventory;
            this.ragdollController = ragdollController;
            this.visualRoot = visualRoot;
            lootInventory = new GridOnlyInventory(sourceInventory);
        }

        public void ActivateCorpse()
        {
            IsLootable = sourceInventory != null;
        }

        private void LateUpdate()
        {
            if (!IsLootable
             || ragdollController == null
             || !ragdollController.TryGetRagdollCenter(out var center))
            {
                return;
            }

            var delta = center - transform.position;
            if (delta.sqrMagnitude <= followDeadZone * followDeadZone)
            {
                return;
            }

            var preservedRoot = GetPreservedRagdollRoot();
            transform.position += delta;

            if (preservedRoot != null && preservedRoot != transform)
            {
                preservedRoot.position -= delta;
            }
        }

        private Transform GetPreservedRagdollRoot()
        {
            if (visualRoot != null && visualRoot.transform != transform)
            {
                return visualRoot.transform;
            }

            return ragdollController != null
                ? ragdollController.GetTopRagdollRootUnder(transform)
                : null;
        }

        private sealed class GridOnlyInventory : ITiledInventory
        {
            private readonly IEquipmentInventory source;

            public GridOnlyInventory(IEquipmentInventory source)
            {
                this.source = source;
            }

            public ReactiveCollection<ItemInInventory> Items => source.Items;
            public IObservable<Unit> Changed => source.Changed;
            public Tiles Tiles => source.Tiles;

            public float MaxWeight
            {
                get => source.MaxWeight;
                set => source.MaxWeight = value;
            }

            public bool CanAdd(ItemConfig config, Tile tile)
            {
                return source.CanAdd(config, tile);
            }

            public bool TryAdd(ItemConfig config)
            {
                return source.TryAddToGrid(new ItemStack(config)) == null;
            }

            public ItemStack TryAdd(ItemStack itemStack)
            {
                return source.TryAddToGrid(itemStack);
            }

            public bool TryAdd(ItemConfig config, Tile tile)
            {
                return source.TryAdd(config, tile);
            }

            public ItemStack TryAdd(ItemStack itemStack, Tile tile)
            {
                return source.TryAdd(itemStack, tile);
            }

            public void Add(ItemConfig config, Matrix4x4 position)
            {
                source.Add(config, position);
            }

            public void Add(ItemStack itemStack, Matrix4x4 position)
            {
                source.Add(itemStack, position);
            }

            public bool CanGet(ItemInInventory itemInInventory)
            {
                return source.CanGet(itemInInventory);
            }

            public bool TryGet(Tile tile, out ItemInInventory itemInInventory)
            {
                return source.TryGet(tile, out itemInInventory);
            }

            public void Remove(ItemInInventory itemInInventory)
            {
                source.Remove(itemInInventory);
            }
        }
    }
}
