using System.Collections.Generic;
using Inventory.Item;
using Sounds;
using UnityEngine;
using UnityEngine.Serialization;

namespace Landings.Plants.PlantConfigs
{
    [CreateAssetMenu(fileName = "FruitPlantConfig", menuName = "configs/Plants/FruitPlantConfig")]
    public class FruitPlantConfig : PlantConfig
    {
        [field: Header("==========")]
        [field: SerializeField] public Vector2 FruitGrowTime { get; private set; } = new(1.0f, 2.0f);
        [field: SerializeField] public List<GameObject> FruitStages { get; private set; } = new();
        [field: SerializeField] public float FruitGrowChance { get; private set; } = .9f;
        [field: SerializeField] public ItemConfig HandFruit { get; private set; }
        [field: SerializeField] public SoundConfig FruitSoundConfig { get; private set; }
    }
}