using UnityEngine;

namespace Combat
{
    public interface ICharacterHitReactionController
    {
        bool IsReacting { get; }
        void RegisterDamage(float damage, Vector3 hitPoint, Transform attackerTransform = null);
        void CancelReaction();
    }
}
