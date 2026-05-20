using UnityEngine;

namespace Inventory
{
    [DisallowMultipleComponent]
    public sealed class PlayerWeaponAnimationEventReceiver : MonoBehaviour
    {
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
            weaponInHandController?.HoldAttackReadyFromAnimationEvent();
        }

        public void AttackStarted()
        {
            weaponInHandController?.AttackStartedFromAnimationEvent();
        }

        public void AttackFinished()
        {
            weaponInHandController?.AttackFinishedFromAnimationEvent();
        }
    }
}
