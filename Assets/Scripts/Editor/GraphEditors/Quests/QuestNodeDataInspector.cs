using Quests.Graph.Model;
using UnityEditor;

namespace Quests.Editor
{
    [CustomEditor(typeof(QuestNodeData))]
    public class QuestNodeDataInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            QuestPreviewUtility.DrawQuestNodePreview((QuestNodeData)target, "Quest Node");
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "Map target selection is edited in Quest Editor. " +
                "Scene objects only declare available targets through QuestMapTarget for a specific quest.",
                MessageType.Info);
            EditorGUILayout.Space(4f);
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
        }
    }
}
