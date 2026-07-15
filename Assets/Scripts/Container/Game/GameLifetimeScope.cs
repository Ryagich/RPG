using Dialogue;
using GameModes;
using Input;
using MessagePipe;
using Messages;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Inventory.Looting;
using GameAudio;

namespace Container.Game
{
    public class GameLifetimeScope : LifetimeScope
    {
        [field: SerializeField] public PlayerLifetimeScope PlayerPrefab { get; private set; } = null!;

        private PlayerLifetimeScope playerScope;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(Camera.main).AsSelf();

            // === MessagePipe ===
            var options = builder.RegisterMessagePipe();
            builder.RegisterMessageBroker<PlayerMoveMessage>(options);
            builder.RegisterMessageBroker<InteractableInputMessage>(options);
            builder.RegisterMessageBroker<InventoryInputMessage>(options);
            builder.RegisterMessageBroker<MapInputMessage>(options);
            builder.RegisterMessageBroker<PauseInputMessage>(options);
            builder.RegisterMessageBroker<TargetLockInputMessage>(options);
            builder.RegisterMessageBroker<MouseDown>(options);
            builder.RegisterMessageBroker<MouseUp>(options);
            builder.RegisterMessageBroker<ShowStatsInputMessage>(options);
            builder.RegisterMessageBroker<FastSlotInputMessage>(options);
            builder.RegisterMessageBroker<WeaponSlotInputMessage>(options);
            builder.RegisterMessageBroker<ChangeGameModeRequest>(options);
            builder.RegisterMessageBroker<GameModeChangedMessage>(options);
            builder.RegisterMessageBroker<PlayerDiedMessage>(options);
            builder.RegisterMessageBroker<CharacterDamagedMessage>(options);
            builder.RegisterMessageBroker<InteractableMessage>(options);
            builder.RegisterMessageBroker<InteractableEndMessage>(options);
            builder.RegisterMessageBroker<ItemHolderFoundMessage>(options);
            builder.RegisterMessageBroker<ItemHolderLostMessage>(options);
            builder.RegisterMessageBroker<PlaySoundMessage>(options);
             
            // === InputHandler ===
            builder.Register<InputHandler>(Lifetime.Singleton).AsSelf().As<IStartable>();

            builder.RegisterBuildCallback(container =>
                                          {
                                              playerScope = CreateChildFromPrefab(PlayerPrefab, _ => { });
                                          });
            builder.Register<LootingContext>(Lifetime.Singleton).AsSelf();
            builder.Register<DialogueContext>(Lifetime.Singleton).AsSelf();
            builder.Register<Player.PlayerDeathState>(Lifetime.Singleton).AsSelf();
            builder.RegisterEntryPoint<GameModesController>().AsSelf();
            builder.RegisterEntryPoint<SoundMessagePlayer>().AsSelf();
        }
    }
}
