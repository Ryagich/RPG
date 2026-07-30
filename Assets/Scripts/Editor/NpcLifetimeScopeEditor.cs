using Container;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    [CustomEditor(typeof(NpcLifetimeScope))]
    public sealed class NpcLifetimeScopeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    "Script",
                    MonoScript.FromMonoBehaviour((NpcLifetimeScope)target),
                    typeof(NpcLifetimeScope),
                    false);
            }

            EditorGUILayout.Space(4f);
            var canTalk = serializedObject.FindProperty("canTalk");
            var property = serializedObject.GetIterator();
            var enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.name == "m_Script" || property.name == "dialog" && canTalk != null && !canTalk.boolValue)
                {
                    continue;
                }

                EditorGUILayout.PropertyField(property, true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
