using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Receives a weapon hit without requiring character health or damage handling.
    /// </summary>
    public abstract class WeaponHitReceiver : MonoBehaviour
    {
        public abstract void ReceiveHit(in WeaponHit hit);
    }
}
