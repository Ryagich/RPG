using UnityEngine;
using UnityEngine.Audio;

namespace GameAudio
{
    [CreateAssetMenu(fileName = "Weapon Audio Profile", menuName = "configs/Audio/Weapon Audio Profile")]
    public sealed class WeaponAudioProfile : ScriptableObject
    {
        [SerializeField] private AudioResource swing;
        [SerializeField] private AudioResource impact;

        public AudioResource Swing => swing;
        public AudioResource Impact => impact;
    }
}
