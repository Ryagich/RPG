
using System.Collections.Generic;
using NaughtyAttributes;
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

        [field: Header("Fruit tree settings")]
        [field: SerializeField, ShowIf(nameof(IsFruitTree))]
        public Vector2 TreeFruitGrowInterval { get; private set; } = new(4f, 18f);

        [field: SerializeField, ShowIf(nameof(IsFruitTree))]
        public Vector2 FruitFallCheckInterval { get; private set; } = new(45f, 90f);

        [field: SerializeField, Range(0f, 1f), ShowIf(nameof(IsFruitTree))]
        [field: Tooltip("Chance of a fruit falling during one check when every fruit point is occupied.")]
        public float FruitFallChancePerCheck { get; private set; } = 0.03f;

        [field: SerializeField, Range(0f, 1f), ShowIf(nameof(IsFruitTree))]
        [field: Tooltip("Base chance of a fruit falling after a weapon hit. It is increased by the natural fall chance.")]
        public float FruitFallChanceOnHit { get; private set; } = 0.2f;

        private bool IsFruitTree => Type == PlantType.FruitTree;
    }

    public enum PlantType
    {
        Vegetable,
        Fruit,
        FruitTree
    }
}
