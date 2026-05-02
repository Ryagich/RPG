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
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
        }
    }
}
