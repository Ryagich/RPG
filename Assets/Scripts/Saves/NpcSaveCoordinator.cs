using System;
using Inventory.Inventories;
using Inventory.Storage;
using Stats;
using UniRx;
using UnityEngine;
using VContainer.Unity;
using YG;

namespace Saves
{
    /// <summary>
    /// Restores one scene NPC from its saved identity and contributes its current state to the
    /// existing checkpoint flow. It never writes a save by itself.
    /// </summary>
    public sealed class NpcSaveCoordinator : IStartable, IDisposable, INpcCharacterSaveParticipant
    {
        private readonly GameSaveController saveController;
        private readonly NpcCharacterSaveRegistry registry;
        private readonly ItemStorage itemStorage;
        private readonly IEquipmentInventory inventory;
        private readonly StatsController stats;
        private readonly Transform characterTransform;
        private readonly Container.NpcLifetimeScope npcScope;
        private readonly CompositeDisposable disposables = new();
        private bool isAlive = true;
        private SavedCharacterPose deathPose;
        private bool isRegistered;
        private bool isDisposed;

        public string CharacterId => npcScope != null ? npcScope.PersistentCharacterId : null;
        public bool IsAvailableForSaving => !isDisposed && npcScope != null && npcScope.Container != null;

        public NpcSaveCoordinator(
            GameSaveController saveController,
            NpcCharacterSaveRegistry registry,
            ItemStorage itemStorage,
            IEquipmentInventory inventory,
            StatsController stats,
            Transform characterTransform)
        {
            this.saveController = saveController;
            this.registry = registry;
            this.itemStorage = itemStorage;
            this.inventory = inventory;
            this.stats = stats;
            this.characterTransform = characterTransform;
            npcScope = characterTransform != null
                ? characterTransform.GetComponent<Container.NpcLifetimeScope>()
                : null;
        }

        public async void Start()
        {
            // Training and other runtime-spawned NPCs use the same combat scope, but they are
            // not authored world characters and must not create save records.
            if (npcScope == null || !npcScope.ParticipatesInPersistence)
            {
                return;
            }

            await saveController.Ready;
            if (isDisposed)
            {
                return;
            }

            if (!await itemStorage.Ready)
            {
                Debug.LogError($"NPC '{CharacterId ?? npcScope?.name ?? "Unknown"}' was not registered for saving because the item catalogue failed to load.");
                return;
            }

            if (isDisposed)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(CharacterId))
            {
                Debug.LogError($"NPC '{npcScope?.name ?? "Unknown"}' has no persistent character id. Open and save its scene to generate one.", npcScope);
                return;
            }

            if (saveController.TryGetSavedNpc(CharacterId, out SavedCharacterState savedState))
            {
                RestoreState(savedState);
            }

            isRegistered = registry.Register(this);
            if (!isRegistered)
            {
                return;
            }

            stats.Hp.Value.Subscribe(_ => TrackDeath()).AddTo(disposables);
            TrackDeath();
        }

        public void Dispose()
        {
            isDisposed = true;
            disposables.Dispose();
            if (isRegistered)
            {
                registry.Unregister(this);
            }
        }

        public SavedCharacterState CaptureSaveState()
        {
            // CorpseLootController follows the settled ragdoll and can move the NPC root after
            // the lethal hit. Capture at the existing checkpoint so the stored pose is the
            // actual place where the corpse ended up, not merely the pose from the hit frame.
            if (!isAlive)
            {
                CaptureDeathPose();
            }

            return new SavedCharacterState
            {
                characterId = CharacterId,
                isAlive = isAlive,
                health = stats.Hp.Value.Value,
                stamina = stats.GetStat(StatType.Stamina).Value.Value,
                water = stats.GetStat(StatType.Water).Value.Value,
                food = stats.GetStat(StatType.Food).Value.Value,
                inventory = NpcInventorySaveMapper.Capture(inventory),
                deathPose = deathPose
            };
        }

        private void RestoreState(SavedCharacterState savedState)
        {
            if (savedState == null)
            {
                return;
            }

            isAlive = savedState.isAlive;
            deathPose = savedState.deathPose;
            if (!isAlive && GameSaveService.TryToPose(deathPose, out Pose pose))
            {
                characterTransform.SetPositionAndRotation(pose.position, pose.rotation);
            }

            NpcInventorySaveMapper.Restore(inventory, itemStorage, savedState.inventory);
            stats.ChangeValue(StatType.Hp, isAlive ? savedState.health : stats.Hp.Min);
            stats.ChangeValue(StatType.Stamina, savedState.stamina);
            stats.ChangeValue(StatType.Water, savedState.water);
            stats.ChangeValue(StatType.Food, savedState.food);
        }

        private void TrackDeath()
        {
            if (!isAlive || stats.Hp.Value.Value > stats.Hp.Min)
            {
                return;
            }

            isAlive = false;
            CaptureDeathPose();
        }

        private void CaptureDeathPose()
        {
            if (characterTransform != null)
            {
                deathPose = GameSaveService.ToSavedCharacterPose(
                    new Pose(characterTransform.position, characterTransform.rotation));
            }
        }
    }
}
