using Sounds;
using UnityEngine;

namespace Landings.Plants
{
    [CreateAssetMenu(fileName = "PlantSoundsSettings", menuName = "configs/Plants/PlantSoundsSettings")]
    public class PlantSoundsSettings : ScriptableObject
    {
        [field: SerializeField] public SoundSettings GrownUpSoundSettings { get; private set; }
        [field: SerializeField] public SoundSettings GrownStageSoundSettings { get; private set; }
    }
}
