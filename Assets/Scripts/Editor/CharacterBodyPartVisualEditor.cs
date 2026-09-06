using Inventory;
using Inventory.Item;
using UnityEditor;
using BodyPart = Inventory.Item.BodyPart;

namespace EditorScripts
{
    [CustomEditor(typeof(CharacterBodyPartVisual))]
    public sealed class CharacterBodyPartVisualEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var bodyPartProperty = serializedObject.FindProperty("bodyPart");
            var visualNameProperty = serializedObject.FindProperty("visualName");
            var genderProperty = serializedObject.FindProperty("gender");

            EditorGUILayout.PropertyField(bodyPartProperty);
            EditorGUILayout.PropertyField(visualNameProperty);
            if ((BodyPart)bodyPartProperty.enumValueIndex != BodyPart.Hair)
            {
                EditorGUILayout.PropertyField(genderProperty);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
