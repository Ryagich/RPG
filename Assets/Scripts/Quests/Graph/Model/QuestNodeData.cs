using System.Collections.Generic;
using System.Linq;
using Quests.MapTargets;
using Quests.Graph;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Serialization;

namespace Quests.Graph.Model
{
    public enum QuestMapTargetSourceType
    {
        None = 0,
        SceneTarget = 1,
        ScriptTarget = 2
    }

    [CreateAssetMenu(fileName = "QuestNode", menuName = "configs/Quests/Node")]
    public class QuestNodeData : ScriptableObject
    {
        [SerializeField] private string editorTitle;
        [FormerlySerializedAs("<Name>k__BackingField")]
        [SerializeField] private LocalizedString localizedName = new();
        [FormerlySerializedAs("<Description>k__BackingField")]
        [SerializeField] private LocalizedString localizedDescription = new();
        [SerializeField] private Sprite icon;
        [SerializeField, HideInInspector] private QuestMapTargetSourceType mapTargetSource;
        [SerializeField, HideInInspector] private string sceneMapTargetId;
        [SerializeField, HideInInspector] private string scriptMapTargetKey;
        [SerializeField, HideInInspector] private QuestGraph ownerGraph;
        [SerializeField] private bool hasAvailabilityRequirements;
        [SerializeField] private List<QuestResourceEntry> availabilityRequirements = new();
        [SerializeField] private bool hasCompletionResults;
        [SerializeField] private List<QuestResourceEntry> completionResults = new();
        [FormerlySerializedAs("Transitions")]
        [SerializeField] private List<QuestTransition> transitions = new();

        public string EditorTitle => string.IsNullOrWhiteSpace(editorTitle) ? name : editorTitle;
        public LocalizedString Name => localizedName;
        public LocalizedString Description => localizedDescription;
        public Sprite Icon => icon;
        public Transform MapTarget => QuestMapTargetRegistry.GetTarget(this);
        public QuestMapTargetSourceType MapTargetSource => mapTargetSource;
        public string SceneMapTargetId => sceneMapTargetId;
        public string ScriptMapTargetKey => scriptMapTargetKey;
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

        public void SetMapTargetSelection(QuestMapTargetSourceType sourceType, string targetId, string scriptTargetKey)
        {
            mapTargetSource = sourceType;

            sceneMapTargetId = sourceType == QuestMapTargetSourceType.SceneTarget
                ? targetId?.Trim() ?? string.Empty
                : string.Empty;

            scriptMapTargetKey = sourceType == QuestMapTargetSourceType.ScriptTarget
                ? scriptTargetKey?.Trim() ?? string.Empty
                : string.Empty;
        }

        public bool HasOutgoingTransitions()
        {
            return transitions != null && transitions.Any(transition => transition != null);
        }
    }
}
