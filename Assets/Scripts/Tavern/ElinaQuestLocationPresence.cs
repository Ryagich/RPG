using Dialogue;
using Quests;
using Quests.Graph;
using Quests.Graph.Model;
using UnityEngine;
using VContainer;

namespace Tavern
{
    /// <summary>
    /// Presents one authored appearance of Elina. The quest owns the stage; this component
    /// only applies that stage when a location is loaded, so a conversation never makes its
    /// speaker vanish in front of the player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ElinaQuestLocationPresence : MonoBehaviour
    {
        [SerializeField] private QuestGraph quest;
        [SerializeField] private QuestNodeData tavernStageNode;
        [SerializeField] private DialogueRuntimeFlag relocationPendingFlag;
        [SerializeField] private bool isTavernAppearance;

        private DialogueContext dialogueContext;
        private DialogueRuntimeFlagRegistry runtimeFlags;
        private bool isSubscribed;

        [Inject]
        public void Construct(DialogueContext context, DialogueRuntimeFlagRegistry flags)
        {
            dialogueContext = context;
            runtimeFlags = flags;
        }

        private void Start()
        {
            if (dialogueContext == null)
            {
                return;
            }

            if (dialogueContext.IsPlayerQuestControllerReady)
            {
                ApplyQuestStage(dialogueContext.PlayerQuestController);
                return;
            }

            dialogueContext.PlayerQuestControllerReady += ApplyQuestStage;
            isSubscribed = true;
        }

        private void OnDestroy()
        {
            if (isSubscribed && dialogueContext != null)
            {
                dialogueContext.PlayerQuestControllerReady -= ApplyQuestStage;
            }
        }

        private void ApplyQuestStage(QuestController quests)
        {
            if (isSubscribed)
            {
                dialogueContext.PlayerQuestControllerReady -= ApplyQuestStage;
                isSubscribed = false;
            }

            bool isAtTavernStage = quests != null && quest != null && tavernStageNode != null &&
                quests.IsAtNode(quest, tavernStageNode);

            if (isTavernAppearance && isAtTavernStage)
            {
                runtimeFlags?.Deactivate(relocationPendingFlag);
            }

            gameObject.SetActive(isTavernAppearance == isAtTavernStage);
        }
    }
}
