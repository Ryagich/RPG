using UnityEngine;
using UnityEngine.Audio;

namespace GameAudio
{
    [DisallowMultipleComponent]
    public sealed class CharacterCombatAudio : MonoBehaviour
    {
        [SerializeField] private CharacterCombatAudioProfile profile;

        public AudioResource DamageGrunt => profile != null ? profile.DamageGrunt : null;
    }
}
