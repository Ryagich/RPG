using Quests.Graph.Model;
using UnityEngine;

namespace Quests.Graph
{
    [System.Serializable]
    public class QuestNode
    {
        public Vector2 Position;
        public QuestNodeData NodeData;

        public QuestNode(QuestNodeData nodeData)
        {
            NodeData = nodeData;
        }
    }
}
