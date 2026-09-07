using Container;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    [CustomEditor(typeof(ChestLifetimeScope))]
    public sealed class ChestLifetimeScopeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((ChestLifetimeScope)target), typeof(ChestLifetimeScope), false);
            }

            DrawProperty("characterInfo");
            DrawProperty("inventoryConfig");
            DrawProperty("hasInfiniteInventorySize");
            DrawProperty("itemLootSetConfig");
            DrawProperty("itemSetConfig");
            EditorGUILayout.Space(4f);
            var animateLid = serializedObject.FindProperty("animateLid");
            EditorGUILayout.PropertyField(animateLid);
            if (animateLid.boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("lidAnimationSettings"), true);
            }
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawProperty(string propertyName)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName), true);
        }
    }
}
