using UnityEngine;

namespace Inventory
{
    [DisallowMultipleComponent]
    public sealed class PlayerWeaponAnimationEventReceiver : MonoBehaviour
    {
        // Animation event contract used on attack clips:
        // - ResetAttackRequest: clears the Animator Attack bool so combo transitions do not loop forever.
        // - LockMovement: blocks player movement/rotation at an arbitrary moment in an attack clip.
        // - UnlockMovement: restores player movement/rotation at an arbitrary moment in an attack clip.
        // AttackStarted/AttackFinished remain available as optional hooks, but they are not the core
        // events relied on by the current attack clips.
        private PlayerWeaponInHandController weaponInHandController;

        public void Bind(PlayerWeaponInHandController weaponInHandController)
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
        }

        public void AttackStarted()
        {
            weaponInHandController?.AttackStartedFromAnimationEvent();
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

        public void ResetAttackRequest()
        {
            weaponInHandController?.ResetAttackRequestFromAnimationEvent();
        }
    }
}
