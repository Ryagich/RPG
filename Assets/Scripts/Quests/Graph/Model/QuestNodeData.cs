using System.Collections.Generic;
using System.Linq;
using Quests.Graph;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Serialization;

namespace Quests.Graph.Model
{
    [CreateAssetMenu(fileName = "QuestNode", menuName = "configs/Quests/Node")]
    public class QuestNodeData : ScriptableObject
    {
        [SerializeField] private string editorTitle;
        [FormerlySerializedAs("<Name>k__BackingField")]
        [SerializeField] private LocalizedString localizedName = new();
        [SerializeField] private Sprite icon;
        [SerializeField, HideInInspector] private QuestGraph ownerGraph;
        [SerializeField] private bool hasAvailabilityRequirements;
        [SerializeField] private List<QuestResourceEntry> availabilityRequirements = new();
        [SerializeField] private bool hasCompletionResults;
        [SerializeField] private List<QuestResourceEntry> completionResults = new();
        [FormerlySerializedAs("Transitions")]
        [SerializeField] private List<QuestTransition> transitions = new();

        public string EditorTitle => string.IsNullOrWhiteSpace(editorTitle) ? name : editorTitle;
        public LocalizedString Name => localizedName;
        public Sprite Icon => icon;
        public QuestGraph OwnerGraph => ownerGraph;
        public bool HasAvailabilityRequirements => hasAvailabilityRequirements;
        public List<QuestResourceEntry> AvailabilityRequirements => availabilityRequirements;
        public bool HasCompletionResults => hasCompletionResults;
        public List<QuestResourceEntry> CompletionResults => completionResults;
        public List<QuestTransition> Transitions
        {
            get => transitions;
            set => transitions = value;
        }

        public void SetOwnerGraph(QuestGraph questGraph)
        {
            ownerGraph = questGraph;
        }

        public void ClearOwnerGraph(QuestGraph questGraph)
        {
            if (ownerGraph == questGraph)
            {
                ownerGraph = null;
            }
        }

        public void SetEditorTitle(string title)
        {
            editorTitle = title;
        }

        public bool HasOutgoingTransitions()
        {
            return transitions != null && transitions.Any(transition => transition != null);
        }
    }
}
