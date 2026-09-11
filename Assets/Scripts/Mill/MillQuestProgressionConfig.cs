using Dialogue;
using Quests.Graph;
using Quests.Graph.Model;
using UnityEngine;

namespace Mill
{
    /// <summary>
    /// Authored cross-location contract for the mill investigation. It keeps dialogue event
    /// identities and quest nodes in data, while the runtime coordinator owns progression.
    /// </summary>
    [CreateAssetMenu(fileName = "Mill Quest Progression", menuName = "configs/Mill/Quest Progression")]
    public sealed class MillQuestProgressionConfig : ScriptableObject
    {
        [Header("Quests")]
        [SerializeField] private QuestGraph guardInvestigationQuest;
        [SerializeField] private QuestNodeData checkMillNode;
        [SerializeField] private QuestNodeData reportToGuardNode;
        [SerializeField] private QuestNodeData guardSquadAwaitingNode;
        [SerializeField] private QuestNodeData helpRetakeMillNode;
        [SerializeField] private QuestNodeData guardAssaultFailedNode;
        [SerializeField] private QuestNodeData askAroundNode;
        [SerializeField] private QuestNodeData visitTavernNode;
        [SerializeField] private QuestNodeData reportToGuardFromRumourNode;

        [Header("Legacy save migration")]
        [SerializeField] private QuestGraph legacyTellMillerFateQuest;
        [SerializeField] private QuestNodeData legacyAskAroundNode;
        [SerializeField] private QuestNodeData legacyVisitTavernNode;
        [SerializeField] private QuestNodeData legacyReportToGuardNode;
        [SerializeField] private QuestNodeData legacySquadAwaitingNode;
        [SerializeField] private QuestNodeData legacyHelpRetakeMillNode;

        [Header("Dialogue events")]
        [SerializeField] private DialogueGameplayEvent learnedMillerFateEvent;
        [SerializeField] private DialogueGameplayEvent askedBlacksmithEvent;
        [SerializeField] private DialogueGameplayEvent askedHalvarEvent;
        [SerializeField] private DialogueGameplayEvent reportedToGuardEvent;
        [SerializeField] private DialogueGameplayEvent requestedRansomEvent;
        [SerializeField] private DialogueGameplayEvent paidRansomPrincipalEvent;
        [SerializeField] private DialogueGameplayEvent paidRansomInterestEvent;

        [Header("Dialogue state flags")]
        [SerializeField] private DialogueRuntimeFlag millerFateKnownFlag;
        [SerializeField] private DialogueRuntimeFlag ransomPrincipalDueFlag;
        [SerializeField] private DialogueRuntimeFlag ransomInterestDueFlag;

        public QuestGraph GuardInvestigationQuest => guardInvestigationQuest;
        public QuestNodeData CheckMillNode => checkMillNode;
        public QuestNodeData ReportToGuardNode => reportToGuardNode;
        public QuestNodeData GuardSquadAwaitingNode => guardSquadAwaitingNode;
        public QuestNodeData HelpRetakeMillNode => helpRetakeMillNode;
        public QuestNodeData GuardAssaultFailedNode => guardAssaultFailedNode;
        public QuestNodeData AskAroundNode => askAroundNode;
        public QuestNodeData VisitTavernNode => visitTavernNode;
        public QuestNodeData ReportToGuardFromRumourNode => reportToGuardFromRumourNode;
        public QuestGraph LegacyTellMillerFateQuest => legacyTellMillerFateQuest;
        public QuestNodeData LegacyAskAroundNode => legacyAskAroundNode;
        public QuestNodeData LegacyVisitTavernNode => legacyVisitTavernNode;
        public QuestNodeData LegacyReportToGuardNode => legacyReportToGuardNode;
        public QuestNodeData LegacySquadAwaitingNode => legacySquadAwaitingNode;
        public QuestNodeData LegacyHelpRetakeMillNode => legacyHelpRetakeMillNode;
        public DialogueGameplayEvent LearnedMillerFateEvent => learnedMillerFateEvent;
        public DialogueGameplayEvent AskedBlacksmithEvent => askedBlacksmithEvent;
        public DialogueGameplayEvent AskedHalvarEvent => askedHalvarEvent;
        public DialogueGameplayEvent ReportedToGuardEvent => reportedToGuardEvent;
        public DialogueGameplayEvent RequestedRansomEvent => requestedRansomEvent;
        public DialogueGameplayEvent PaidRansomPrincipalEvent => paidRansomPrincipalEvent;
        public DialogueGameplayEvent PaidRansomInterestEvent => paidRansomInterestEvent;
        public DialogueRuntimeFlag MillerFateKnownFlag => millerFateKnownFlag;
        public DialogueRuntimeFlag RansomPrincipalDueFlag => ransomPrincipalDueFlag;
        public DialogueRuntimeFlag RansomInterestDueFlag => ransomInterestDueFlag;
    }
}
