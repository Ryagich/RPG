using UnityEngine;
using UnityEngine.Audio;

namespace GameAudio
{
    [CreateAssetMenu(fileName = "Character Combat Audio Profile", menuName = "configs/Audio/Character Combat Audio Profile")]
    public sealed class CharacterCombatAudioProfile : ScriptableObject
    {
        [SerializeField] private AudioResource damageGrunt;

        public AudioResource DamageGrunt => damageGrunt;
    }
}
