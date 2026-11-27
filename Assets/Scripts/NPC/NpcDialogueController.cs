using Combat;
using System;
using VContainer;
using VContainer.Unity;

namespace NPC
{
    public sealed class NpcDialogueController
    {
        private readonly NpcNavMeshController navMeshController;
        private readonly NpcCombatService combatService;
        private readonly CharacterDamageReceiver ownerDamageReceiver;
        private readonly NpcDialoguePositioning dialoguePositioning;
        private readonly bool canTalk;

        public NpcDialogueController(
            NpcNavMeshController navMeshController,
            NpcCombatService combatService,
            CharacterDamageReceiver ownerDamageReceiver,
            NpcDialoguePositioning dialoguePositioning,
            [Key("Can Talk")] bool canTalk)
        {
            this.navMeshController = navMeshController;
            this.combatService = combatService;
            this.ownerDamageReceiver = ownerDamageReceiver;
            this.dialoguePositioning = dialoguePositioning;
            this.canTalk = canTalk;
        }

        public bool IsDialogueRequested { get; private set; }
        public bool IsInDialogueState { get; private set; }
        public event Action DialogueInterrupted;

        public bool CanStartDialogue(LifetimeScope interactorScope)
        {
            if (IsDialogueRequested)
            {
                return false;
            }

            if (!canTalk)
            {
                return false;
            }

            if (ownerDamageReceiver != null && !ownerDamageReceiver.IsAlive)
            {
                return false;
            }

            if (combatService != null && combatService.HasThreat)
            {
                return false;
            }

            var interactorReceiver = interactorScope != null
                ? interactorScope.GetComponent<DamageReceiverHost>()?.Receiver
                : null;
            if (interactorReceiver != null && combatService != null && combatService.IsHostileToReceiver(interactorReceiver))
            {
                return false;
            }

            return true;
        }

        public bool TryBeginDialogue(LifetimeScope interactorScope)
        {
            if (!CanStartDialogue(interactorScope))
            {
                return false;
            }

            IsDialogueRequested = true;
            return true;
        }

        public void EndDialogue()
        {
            IsDialogueRequested = false;
        }

        public void EnterDialogueState()
        {
            IsInDialogueState = true;
            if (dialoguePositioning?.IsActive != true)
            {
                navMeshController?.Stop();
            }
            navMeshController?.SetFacingLocked(true);
        }

        public void TickDialogue()
        {
            if (dialoguePositioning?.Tick() != true)
            {
                navMeshController?.Stop();
            }
        }

        public void ExitDialogueState()
        {
            IsInDialogueState = false;
            dialoguePositioning?.Cancel();
            navMeshController?.SetFacingLocked(false);

            if (!IsDialogueRequested)
            {
                return;
            }

            EndDialogue();
            DialogueInterrupted?.Invoke();
        }

    }
}
