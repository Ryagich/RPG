namespace Inventory
{
    public interface IWeaponAnimationEventHandler
    {
        void TakeWeaponInHandFromAnimationEvent();
        void BeginMoveWeaponToRightHandFromAnimationEvent();
        void PutWeaponOnBeltFromAnimationEvent();
        void BeginMoveWeaponToBeltFromAnimationEvent();
        void AttackStartedFromAnimationEvent();
        void BeginDamageWindowFromAnimationEvent();
        void EndDamageWindowFromAnimationEvent();
        void LockMovementFromAnimationEvent();
        void UnlockMovementFromAnimationEvent();
        void AttackFinishedFromAnimationEvent();
        void ResetAttackRequestFromAnimationEvent();
    }
}
