using InfinityPBR;
using UnityEngine;
using VContainer.Unity;

namespace Monsters
{
    /// <summary>
    /// Owns PlantMonster_1 appearance changes while preserving the prefab-authored
    /// appearance until gameplay explicitly requests a different one.
    /// </summary>
    public sealed class PlantMonsterVisualController : IStartable
    {
        private readonly BlendShapesManager blendShapes;
        private readonly BlendShapesPresetManager blendShapePresets;
        private readonly PrefabAndObjectManager partGroups;
        private readonly ColorShiftRuntime colorShift;

        public PlantMonsterVisualController(
            BlendShapesManager blendShapes,
            BlendShapesPresetManager blendShapePresets,
            PrefabAndObjectManager partGroups,
            ColorShiftRuntime colorShift)
        {
            this.blendShapes = blendShapes;
            this.blendShapePresets = blendShapePresets;
            this.partGroups = partGroups;
            this.colorShift = colorShift;
        }

        public void Start()
        {
            if (blendShapes == null || blendShapePresets == null || partGroups == null || colorShift == null)
            {
                Debug.LogError("PlantMonster_1 is missing one or more required visual components.");
            }
        }

        public bool ApplyPreset(string presetName)
        {
            if (string.IsNullOrWhiteSpace(presetName) || blendShapePresets == null)
            {
                return false;
            }

            var presetIndex = blendShapePresets.presets.FindIndex(preset => preset.name == presetName);
            if (presetIndex < 0)
            {
                Debug.LogWarning($"PlantMonster_1 has no visual preset named '{presetName}'.");
                return false;
            }

            blendShapePresets.ActivatePreset(presetIndex);
            return true;
        }

        /// <summary>
        /// Uses the asset-authored random preset, child-part groups, and colour set.
        /// The caller decides when visual randomisation is appropriate.
        /// </summary>
        public void RandomizeAppearance()
        {
            ApplyPreset("Random");
            partGroups?.ActivateRandomAllGroups();
            colorShift?.SetRandomColorSet();
        }
    }
}
