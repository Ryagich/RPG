using System.Collections.Generic;
using System.Linq;
using Quests.Graph.Model;
using UnityEngine;
using UnityEngine.Localization;

namespace Quests.Graph
{
    [CreateAssetMenu(fileName = "QuestGraph", menuName = "configs/Quests/Graph")]
    public class QuestGraph : ScriptableObject
    {
        [SerializeField] private LocalizedString title = new();
        [SerializeField] private LocalizedString description = new();
        [SerializeField] private Sprite icon;
        [SerializeField] private bool keepCompletedInJournal = true;

        public List<QuestNode> Nodes = new();
        public LocalizedString Title => title;
        public LocalizedString Description => description;
        public Sprite Icon => icon;
        public bool KeepCompletedInJournal => keepCompletedInJournal;

        public QuestNodeData GetEntryNode() => Nodes.FirstOrDefault()?.NodeData;

        public bool ContainsNode(QuestNodeData nodeData)
        {
            return nodeData != null && Nodes.Any(node => node?.NodeData == nodeData);
        }

        public bool IsTerminalNode(QuestNodeData nodeData)
        {
            return ContainsNode(nodeData) && !nodeData.HasOutgoingTransitions();
        }
    }
}
