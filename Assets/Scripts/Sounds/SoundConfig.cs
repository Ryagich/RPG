using UnityEngine;

namespace Sounds
{
    [CreateAssetMenu(fileName = "Sound Config", menuName = "configs/Sounds/Sound")]
    public class SoundConfig : ScriptableObject
    {
        [field: SerializeField] public SoundSettings SoundSettings { get; private set; }
    }
}