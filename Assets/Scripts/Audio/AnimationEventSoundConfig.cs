using Sounds;
using UnityEngine;

namespace GameAudio
{
    /// <summary>
    /// Project-wide catalogue of sounds triggered directly by animation events.
    /// Add a dedicated field here when a new animation-event sound is introduced,
    /// then expose it through a parameterless method on its event receiver.
    /// </summary>
    [CreateAssetMenu(fileName = "Animation Event Sound Config", menuName = "configs/Audio/Animation Event Sound Config")]
    public sealed class AnimationEventSoundConfig : ScriptableObject
    {
        [Header("Weapon / attack")]
        [SerializeField] private SoundConfig firstWeaponAttackHitSound;
        [SerializeField] private SoundConfig secondWeaponAttackHitSound;
        [SerializeField] private SoundConfig thirdWeaponAttackHitSound;

        [Header("Weapon / draw-hide")]
        [SerializeField] private SoundConfig drawWeaponSound;
        [SerializeField] private SoundConfig hideWeaponSound;

        public SoundConfig FirstWeaponAttackHitSound => firstWeaponAttackHitSound;
        public SoundConfig SecondWeaponAttackHitSound => secondWeaponAttackHitSound;
        public SoundConfig ThirdWeaponAttackHitSound => thirdWeaponAttackHitSound;
        public SoundConfig DrawWeaponSound => drawWeaponSound;
        public SoundConfig HideWeaponSound => hideWeaponSound;
    }
}
