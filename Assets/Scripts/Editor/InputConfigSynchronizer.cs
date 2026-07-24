using Input;
using System.Linq;
using UnityEditor;
using UnityEngine.InputSystem;

namespace Rpg.EditorTools
{
    [InitializeOnLoad]
    internal static class InputConfigSynchronizer
    {
        private const string ConfigPath = "Assets/Configs/Input/InputConfig.asset";
        private const string ActionsPath = "Assets/Input/AI_Player.inputactions";

        static InputConfigSynchronizer()
        {
            EditorApplication.delayCall += Synchronize;
        }

        [MenuItem("Tools/RPG/Sync Input Config")]
        public static void Synchronize()
        {
            var config = AssetDatabase.LoadAssetAtPath<InputConfig>(ConfigPath);
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            if (config == null || actions == null)
            {
                return;
            }

            var serializedConfig = new SerializedObject(config);
            var changed = false;

            foreach (var actionName in new[]
                     {
                         "Movement", "Interactable", "Inventory", "LeftClick", "RightClick", "Dodge", "Run", "ShowStats",
                         "Pause", "Map", "TargetLock", "TargetLockNext", "TargetLockPrevious",
                         "FastSlot1", "FastSlot2", "FastSlot3", "FastSlot4", "WeaponSlot1", "WeaponSlot2",
                         "CameraZoomIn", "CameraZoomOut",
                     })
            {
                var property = serializedConfig.FindProperty($"<{actionName}>k__BackingField");
                var action = actions.FindAction(actionName is "LeftClick" ? "Left Mouse" : actionName is "RightClick" ? "Right Mouse" : actionName);
                if (property == null || action == null)
                {
                    continue;
                }

                var reference = AssetDatabase.LoadAllAssetsAtPath(ActionsPath)
                    .OfType<InputActionReference>()
                    .FirstOrDefault(candidate => candidate.action?.id == action.id);
                if (reference == null)
                {
                    continue;
                }
                if (property.objectReferenceValue == reference)
                {
                    continue;
                }

                property.objectReferenceValue = reference;
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            serializedConfig.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
    }
}
