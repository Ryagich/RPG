using System;
using Inventory.Inventories;
using Inventory.Storage;
using Factions;
using Money;
using Locations;
using Quests;
using Stats;
using VContainer.Unity;

namespace Saves
{
    /// <summary>Restores a recreated player scope and checkpoints it after quest changes.</summary>
    public sealed class PlayerSaveCoordinator : IStartable, IDisposable
    {
        private readonly GameSaveService saveService;
        private readonly ItemStorage itemStorage;
        private readonly VContainer.IObjectResolver resolver;
        private readonly LocationTransitionService locationTransitions;
        private readonly PlayerInventory inventory;
        private readonly MoneyStorage money;
        private readonly StatsController stats;
        private readonly QuestController quests;
        private readonly IFactionRelations factionRelations;

        public PlayerSaveCoordinator(
            GameSaveService saveService,
            ItemStorage itemStorage,
            VContainer.IObjectResolver resolver,
            LocationTransitionService locationTransitions,
            PlayerInventory inventory,
            MoneyStorage money,
            StatsController stats,
            QuestController quests,
            IFactionRelations factionRelations)
        {
            this.saveService = saveService;
            this.itemStorage = itemStorage;
            this.resolver = resolver;
            this.locationTransitions = locationTransitions;
            this.inventory = inventory;
            this.money = money;
            this.stats = stats;
            this.quests = quests;
            this.factionRelations = factionRelations;
        }

        public async void Start()
        {
            await saveService.Ready;
            if (!await itemStorage.Ready)
            {
                return;
            }
            QuestCatalog questCatalog = resolver.TryResolve(typeof(QuestCatalog), out object catalog, null)
                ? catalog as QuestCatalog
                : null;
            if (saveService.TryRestorePlayer(inventory, money, stats, quests, itemStorage, questCatalog))
            {
                while (quests.TryExecuteAvailableAutomaticTransition())
                {
                }
            }
            quests.Changed += OnQuestChanged;
            factionRelations.Changed += OnFactionRelationsChanged;
        }

        public void Dispose()
        {
            quests.Changed -= OnQuestChanged;
            factionRelations.Changed -= OnFactionRelationsChanged;
        }

        public void Save(string locationId, string entranceId)
        {
            saveService.SavePlayer(locationId, entranceId, inventory, money, stats, quests);
        }

        private void OnQuestChanged(QuestChangeInfo _)
        {
            // A quest mutation is meaningful progress, so preserve a complete player snapshot.
            Save(locationTransitions?.CurrentLocation?.Id, locationTransitions?.CurrentEntrance?.Id);
        }

        private void OnFactionRelationsChanged()
        {
            Save(locationTransitions?.CurrentLocation?.Id, locationTransitions?.CurrentEntrance?.Id);
        }
    }
}
