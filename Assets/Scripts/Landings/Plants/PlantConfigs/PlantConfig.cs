
using System.Collections.Generic;
using Sounds;
using UnityEngine;
using UnityEngine.Localization;

namespace Landings.Plants.PlantConfigs
{
    [CreateAssetMenu(fileName = "PlantConfig", menuName = "configs/Plants/PlantConfig")]
    public class PlantConfig : ScriptableObject
    {
        [field: SerializeField] public string Id { get; private set; }
        [field: SerializeField] public int Price { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField] public LocalizedString Name { get; private set; }
        [field: SerializeField] public List<GameObject> Stages { get; private set; } = new();
        [field: SerializeField] public Vector2 GrowTime { get; private set; } = new(1.0f, 2.0f);
        [field: SerializeField] public Vector2 TimeBetweenStages { get; private set; } = new(1.5f, 2.25f);
        [field: SerializeField] public Vector3 StartPosition { get; private set; }
        [field: SerializeField] public Vector3 TargetPosition { get; private set; }
        [field: SerializeField] public PlantSoundsSettings PlantSoundsSettings { get; private set; }
        [field: SerializeField] public SoundConfig ItemGivenSound { get; private set; }
        [field: SerializeField] public PlantType Type { get; private set; }
    }

    public enum PlantType
    {
        Vegetable,
        Fruit
    }
}
