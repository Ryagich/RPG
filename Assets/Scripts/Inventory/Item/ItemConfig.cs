using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Localization;

namespace Inventory.Item
{
    [CreateAssetMenu(fileName = "ItemConfig", menuName = "configs/Inventory/ItemConfig")]
    public class ItemConfig : ScriptableObject
    {
        private bool HasDefenseModifiers => ItemType == ItemType.Body
                                           || ItemType == ItemType.Helm
                                           || ItemType == ItemType.Face
                                           || ItemType == ItemType.Hands
                                           || ItemType == ItemType.Arms
                                           || ItemType == ItemType.Legs
                                           || ItemType == ItemType.Hips;
        private bool HasFaceBlockingFlag => ItemType == ItemType.Helm;
        private bool HasUsableStats => ItemType == ItemType.Usable;
        private bool HasWeaponPrefab => ItemType == ItemType.Weapon;
        private bool HasWeaponDamage => ItemType == ItemType.Weapon;
        private bool HasEquippedVisuals => ItemType == ItemType.Backpack
                                           || ItemType == ItemType.Body
                                           || ItemType == ItemType.Helm
                                           || ItemType == ItemType.Face
                                           || ItemType == ItemType.Hands
                                           || ItemType == ItemType.Arms
                                           || ItemType == ItemType.Legs
                                           || ItemType == ItemType.Hips;

        [field: SerializeField] public string Id { get; private set; } = "Item Config ID";
        [field: SerializeField, Min(1)] public int Price { get; private set; } = 1;
        [field: SerializeField, Min(0f)] public float Weight { get; private set; } = 2.1f;
        [field: SerializeField, Min(1)] public int MaxStack { get; private set; } = 1;
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField] public LocalizedString Name { get; private set; }
        [field: SerializeField] public LocalizedString Description { get; private set; } = new("Tables", "Null String");
        [field: SerializeField] public ItemHolder HandPrefab { get; private set; }
        [field: SerializeField] public Vector2Int Size { get; private set; } = new(1, 1);
        [field: SerializeField] public Vector2Int SizeInInventory { get; private set; } = new(50, 50);
        [field: SerializeField] public ItemType ItemType { get; private set; }
        [SerializeField, ShowIf(nameof(HasWeaponPrefab))] private GameObject weaponInHandPrefab;
        [SerializeField, ShowIf(nameof(HasWeaponDamage))] private Vector2Int weaponDamageRange = new(9, 11);
        [SerializeField, ShowIf(nameof(HasWeaponPrefab))] private WeaponAttachmentTransformData rightHandWeaponAttachment = new();
        [SerializeField, ShowIf(nameof(HasWeaponPrefab))] private WeaponAttachmentTransformData beltWeaponAttachment = new();
        [SerializeField, ShowIf(nameof(HasDefenseModifiers)), Range(0f, 1f)] private float physicalDefense;
        [SerializeField, ShowIf(nameof(HasDefenseModifiers)), Min(0f)] private float temperatureDefense;
        [SerializeField, ShowIf(nameof(HasDefenseModifiers)), Min(0f)] private float psiDefense;
        [SerializeField, ShowIf(nameof(HasDefenseModifiers)), Min(0f)] private float magicDefense;
        [SerializeField, ShowIf(nameof(HasFaceBlockingFlag))] private bool blocksFaceSlot = true;
        [SerializeField, ShowIf(nameof(HasUsableStats))] private float hpStat;
        [SerializeField, ShowIf(nameof(HasUsableStats))] private float waterStat;
        [SerializeField, ShowIf(nameof(HasUsableStats))] private float foodStat;
        [SerializeField, ShowIf(nameof(HasUsableStats))] private float chillStat;
        [SerializeField, ShowIf(nameof(HasEquippedVisuals))] private List<EquippedItemVisual> equippedVisuals = new();

        public float PhysicalDefense => NormalizeProtectionValue(physicalDefense);
        public float TemperatureDefense => temperatureDefense;
        public float PsiDefense => psiDefense;
        public float MagicDefense => magicDefense;
        public Vector2Int WeaponDamageRange => weaponDamageRange;
        public bool BlocksFaceSlot => blocksFaceSlot;
        public float HpStat => hpStat;
        public float WaterStat => waterStat;
        public float FoodStat => foodStat;
        public float ChillStat => chillStat;
        public GameObject WeaponInHandPrefab => weaponInHandPrefab;
        public WeaponAttachmentTransformData RightHandWeaponAttachment => rightHandWeaponAttachment;
        public WeaponAttachmentTransformData BeltWeaponAttachment => beltWeaponAttachment;
        public IReadOnlyList<EquippedItemVisual> EquippedVisuals => equippedVisuals;

        public int GetRandomWeaponDamage()
        {
            var min = Mathf.Min(weaponDamageRange.x, weaponDamageRange.y);
            var max = Mathf.Max(weaponDamageRange.x, weaponDamageRange.y);
            return Random.Range(min, max + 1);
        }

        private void OnValidate()
        {
            physicalDefense = NormalizeProtectionValue(physicalDefense);
        }

        private static float NormalizeProtectionValue(float value)
        {
            return value > 1f
                ? Mathf.Clamp01(value * 0.01f)
                : Mathf.Clamp01(value);
        }
    }
}
