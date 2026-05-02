using Quests.Graph;
using UnityEditor;

namespace Quests.Editor
{
    [CustomEditor(typeof(QuestGraph))]
    public class QuestGraphInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            QuestPreviewUtility.DrawQuestGraphPreview((QuestGraph)target, "Quest");
            EditorGUILayout.Space(6f);
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
        }
    }
}
