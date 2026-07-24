namespace Inventory
{
    public interface IWeaponAnimationEventHandler
    {
        void TakeWeaponInHandFromAnimationEvent();
        void BeginMoveWeaponToRightHandFromAnimationEvent();
        void PutWeaponOnBeltFromAnimationEvent();
        void BeginMoveWeaponToBeltFromAnimationEvent();
        void HoldAttackReadyFromAnimationEvent();
        void AttackStartedFromAnimationEvent();
        void BeginDamageWindowFromAnimationEvent();
        void EndDamageWindowFromAnimationEvent();
        void EnableDamageImmunityFromAnimationEvent();
        void DisableDamageImmunityFromAnimationEvent();
        void LockMovementFromAnimationEvent();
        void UnlockMovementFromAnimationEvent();
        void AttackFinishedFromAnimationEvent();
        void ResetAttackRequestFromAnimationEvent();
    }
}
