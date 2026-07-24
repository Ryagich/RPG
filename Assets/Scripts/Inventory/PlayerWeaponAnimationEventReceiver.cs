using GameAudio;
using Sounds;
using UnityEngine;
using VContainer;

namespace Inventory
{
    [DisallowMultipleComponent]
    public sealed class PlayerWeaponAnimationEventReceiver : MonoBehaviour
    {
        // Animation event contract used on attack clips:
        // - ResetAnimationRequests: clears every action-request bool (Attack, HeavyAttack and Dodge).
        // - LockMovement: blocks player movement/rotation at an arbitrary moment in an attack clip.
        // - UnlockMovement: restores player movement/rotation at an arbitrary moment in an attack clip.
        // AttackStarted/AttackFinished remain available as optional hooks, but they are not the core
        // events relied on by the current attack clips.
        private IWeaponAnimationEventHandler weaponInHandController;
        private AnimationEventSoundConfig animationEventSoundConfig;
        private IAudioService audioService;

        [Inject]
        public void Construct(AnimationEventSoundConfig animationEventSoundConfig, IAudioService audioService)
        {
            this.animationEventSoundConfig = animationEventSoundConfig;
            this.audioService = audioService;
        }

        public void Bind(IWeaponAnimationEventHandler weaponInHandController)
        {
            this.weaponInHandController = weaponInHandController;
        }

        public void TakeWeaponInHand()
        {
            weaponInHandController?.TakeWeaponInHandFromAnimationEvent();
        }

        public void BeginMoveWeaponToRightHand()
        {
            weaponInHandController?.BeginMoveWeaponToRightHandFromAnimationEvent();
        }

        public void PutWeaponOnBelt()
        {
            weaponInHandController?.PutWeaponOnBeltFromAnimationEvent();
        }

        public void BeginMoveWeaponToBelt()
        {
            weaponInHandController?.BeginMoveWeaponToBeltFromAnimationEvent();
        }

        public void HoldAttackReady()
        {
            weaponInHandController?.HoldAttackReadyFromAnimationEvent();
        }

        public void AttackStarted()
        {
            weaponInHandController?.AttackStartedFromAnimationEvent();
        }

        public void BeginDamageWindow()
        {
            weaponInHandController?.BeginDamageWindowFromAnimationEvent();
        }

        public void EndDamageWindow()
        {
            weaponInHandController?.EndDamageWindowFromAnimationEvent();
        }

        // Dodge animation events. They affect only WeaponHit processing; hunger, thirst and
        // other periodic HP changes do not go through the weapon-damage pipeline.
        public void EnableDamageImmunity()
        {
            weaponInHandController?.EnableDamageImmunityFromAnimationEvent();
        }

        public void DisableDamageImmunity()
        {
            weaponInHandController?.DisableDamageImmunityFromAnimationEvent();
        }

        public void LockMovement()
        {
            weaponInHandController?.LockMovementFromAnimationEvent();
        }

        public void UnlockMovement()
        {
            weaponInHandController?.UnlockMovementFromAnimationEvent();
        }

        public void AttackFinished()
        {
            weaponInHandController?.AttackFinishedFromAnimationEvent();
        }

        public void ResetAnimationRequests()
        {
            weaponInHandController?.ResetAttackRequestFromAnimationEvent();
        }

        // Keeps existing clips working while their Animation Event is renamed.
        public void ResetAttackRequest() => ResetAnimationRequests();

        // Parameterless methods are intentionally kept as the public animation-event contract.
        // Each clip can select its sound without serializing object references into the clip itself.
        public void PlayFirstWeaponAttackHitSound()
        {
            PlaySound(animationEventSoundConfig?.FirstWeaponAttackHitSound);
        }

        public void PlaySecondWeaponAttackHitSound()
        {
            PlaySound(animationEventSoundConfig?.SecondWeaponAttackHitSound);
        }

        public void PlayThirdWeaponAttackHitSound()
        {
            PlaySound(animationEventSoundConfig?.ThirdWeaponAttackHitSound);
        }

        public void PlayDrawWeaponSound()
        {
            PlaySound(animationEventSoundConfig?.DrawWeaponSound);
        }

        public void PlayHideWeaponSound()
        {
            PlaySound(animationEventSoundConfig?.HideWeaponSound);
        }

        private void PlaySound(SoundConfig soundConfig)
        {
            var settings = soundConfig != null ? soundConfig.SoundSettings : null;
            if (audioService == null || settings == null)
            {
                return;
            }

            audioService.Play(settings, transform.position, transform);
        }
    }
}
