using System;
using MessagePipe;
using Messages;
using UniRx;
using VContainer.Unity;

namespace GameAudio
{
    public sealed class CombatAudioPlayer : IStartable, IDisposable
    {
        private readonly IAudioService audio;
        private readonly ISubscriber<WeaponSwingMessage> swings;
        private readonly ISubscriber<PhysicalContactMessage> contacts;
        private readonly ISubscriber<CharacterDamagedMessage> damageEvents;
        private readonly CompositeDisposable subscriptions = new();

        public CombatAudioPlayer(
            IAudioService audio,
            ISubscriber<WeaponSwingMessage> swings,
            ISubscriber<PhysicalContactMessage> contacts,
            ISubscriber<CharacterDamagedMessage> damageEvents)
        {
            this.audio = audio;
            this.swings = swings;
            this.contacts = contacts;
            this.damageEvents = damageEvents;
        }

        public void Start()
        {
            subscriptions.Add(swings.Subscribe(OnSwing));
            subscriptions.Add(contacts.Subscribe(OnContact));
            subscriptions.Add(damageEvents.Subscribe(OnDamaged));
        }

        public void Dispose() => subscriptions.Dispose();

        private void OnSwing(WeaponSwingMessage message)
        {
            audio.PlayResource(message.Weapon.WeaponAudio?.Swing, message.Position);
        }

        private void OnContact(PhysicalContactMessage message)
        {
            var armorImpact = message.Armor?.ArmorImpactSound;
            var resource = armorImpact != null ? armorImpact : message.Weapon?.WeaponAudio?.Impact;
            audio.PlayResource(resource, message.Point);
        }

        private void OnDamaged(CharacterDamagedMessage message)
        {
            if (message.FinalDamage > 0f && message.Character != null
                && message.Character.TryGetComponent<CharacterCombatAudio>(out var characterAudio)
                && characterAudio.isActiveAndEnabled)
            {
                audio.PlayResource(characterAudio.DamageGrunt, characterAudio.transform.position);
            }
        }
    }
}
