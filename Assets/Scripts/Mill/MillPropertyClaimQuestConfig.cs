using Dialogue;
using Quests.Graph;
using Quests.Graph.Model;
using UnityEngine;

namespace Mill
{
    [CreateAssetMenu(fileName = "Mill Property Claim Quest", menuName = "configs/Mill/Property Claim Quest")]
    public sealed class MillPropertyClaimQuestConfig : ScriptableObject
    {
        [SerializeField] private QuestGraph quest;
        [SerializeField] private QuestNodeData askAroundNode;
        [SerializeField] private QuestNodeData confrontCorvinNode;
        // Retained for saves created before the claim had distinct outcomes.
        [SerializeField] private QuestNodeData resolvedNode;
        [SerializeField] private QuestNodeData documentsReturnedNode;
        [SerializeField] private QuestNodeData bribeAcceptedNode;
        [SerializeField] private DialogueGameplayEvent startedEvent;
        [SerializeField] private DialogueGameplayEvent banditLeadLearnedEvent;
        [SerializeField] private DialogueGameplayEvent villageLeadLearnedEvent;
        [SerializeField] private DialogueGameplayEvent corvinIdentifiedEvent;
        [SerializeField] private DialogueGameplayEvent documentsRecoveredEvent;
        [SerializeField] private DialogueGameplayEvent bribeAcceptedEvent;
        public QuestGraph Quest => quest;
        public QuestNodeData AskAroundNode => askAroundNode;
        public QuestNodeData ConfrontCorvinNode => confrontCorvinNode;
        public QuestNodeData ResolvedNode => resolvedNode;
        public QuestNodeData DocumentsReturnedNode => documentsReturnedNode;
        public QuestNodeData BribeAcceptedNode => bribeAcceptedNode;
        public DialogueGameplayEvent StartedEvent => startedEvent;
        public DialogueGameplayEvent BanditLeadLearnedEvent => banditLeadLearnedEvent;
        public DialogueGameplayEvent VillageLeadLearnedEvent => villageLeadLearnedEvent;
        public DialogueGameplayEvent CorvinIdentifiedEvent => corvinIdentifiedEvent;
        public DialogueGameplayEvent DocumentsRecoveredEvent => documentsRecoveredEvent;
        public DialogueGameplayEvent BribeAcceptedEvent => bribeAcceptedEvent;
    }
}
