using Container.Dialogue;
using Dialogs.Graph;
using Interactable;
using Inventory;
using Inventory.Inventories;
using Money;
using Quests.MapTargets;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Container
{
    public class DialogueLifetimeScope : LifetimeScope
    {
        [SerializeField] private Character.CharacterInfo characterInfo;
        [SerializeField] private DialogGraph dialog;
        [SerializeField] private InventoryConfig inventoryConfig;
        [SerializeField] private QuestMapTarget questMapTarget;

        protected override void Configure(IContainerBuilder builder)
        {
            var interactable = gameObject.AddComponent<Interactable.Interactable>();
            interactable.InteractionMode = InteractionMode.Manual;

            builder.RegisterInstance(interactable);
            builder.RegisterInstance(characterInfo).AsSelf();
            if (dialog != null)
            {
                builder.RegisterInstance(dialog).AsSelf();
            }

            if (inventoryConfig != null)
            {
                builder.RegisterInstance(inventoryConfig).AsSelf();
            }

            if (questMapTarget != null)
            {
                builder.RegisterComponent(questMapTarget);
            }

            builder.Register(_ => new MoneyStorage(100), Lifetime.Scoped).AsSelf();
            builder.RegisterEntryPoint<ChestInventory>().As<IInventory>().AsSelf();
            builder.RegisterEntryPoint<DialogueInteractableLogic>().AsSelf();
        }
    }
}
