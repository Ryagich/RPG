using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Quests.Graph.Model
{
    [CreateAssetMenu(fileName = "QuestTransition", menuName = "configs/Quests/Transition")]
    public class QuestTransition : ScriptableObject
    {
        [FormerlySerializedAs("TargetNode")]
        [SerializeField] private QuestNodeData targetNode;
        [SerializeField] private bool hasConditions;
        [SerializeField] private List<QuestResourceEntry> conditions = new();
        [SerializeField] private bool hasResults;
        [SerializeField] private List<QuestResourceEntry> results = new();
        [SerializeField] private bool executeAutomaticallyWhenAvailable;

        public QuestNodeData TargetNode => targetNode;
        public bool HasConditions => hasConditions;
        public List<QuestResourceEntry> Conditions => conditions;
        public bool HasResults => hasResults;
        public List<QuestResourceEntry> Results => results;
        public bool ExecuteAutomaticallyWhenAvailable => executeAutomaticallyWhenAvailable;

        public void SetTargetNode(QuestNodeData nodeData)
        {
            targetNode = nodeData;
        }
    }
}
