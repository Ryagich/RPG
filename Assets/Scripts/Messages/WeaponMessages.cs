using UnityEngine;

namespace Messages
{
    /// <summary>
    /// Published by a weapon controller only after its normal sheathing sequence has reached
    /// the belt. Consumers can coordinate a higher-level sequence without reading controller
    /// implementation state.
    /// </summary>
    public readonly struct WeaponSheathedMessage
    {
        public Transform CharacterTransform { get; }

        public WeaponSheathedMessage(Transform characterTransform)
        {
            CharacterTransform = characterTransform;
        }
    }
}
