using Combat;
using UnityEngine;

namespace Landings.Plants
{
    [RequireComponent(typeof(Collider))]
    public sealed class FruitTreeHitbox : WeaponHitReceiver
    {
        private AppleTreeFruitGrower fruitTree;

        private void Awake()
        {
            fruitTree = GetComponentInParent<AppleTreeFruitGrower>();
        }

        public override void ReceiveHit(in WeaponHit hit)
        {
            if (hit.Damage > 0f)
            {
                fruitTree?.TryDropAppleFromHit();
            }
        }
    }
}
