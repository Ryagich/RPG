using Combat;
using Movement;
using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace NPC
{
    public sealed class NpcDialogueController
    {
        private readonly Transform ownerTransform;
        private readonly NpcNavMeshController navMeshController;
        private readonly NpcCombatService combatService;
        private readonly CharacterDamageReceiver ownerDamageReceiver;
        private readonly PlayerMovementConfig playerMovementConfig;
        private readonly bool canTalk;

        public NpcDialogueController(
            Transform ownerTransform,
            NpcNavMeshController navMeshController,
            NpcCombatService combatService,
            CharacterDamageReceiver ownerDamageReceiver,
            PlayerMovementConfig playerMovementConfig,
            [Key("Can Talk")] bool canTalk)
        {
            this.ownerTransform = ownerTransform;
            this.navMeshController = navMeshController;
            this.combatService = combatService;
            this.ownerDamageReceiver = ownerDamageReceiver;
            this.playerMovementConfig = playerMovementConfig;
            this.canTalk = canTalk;
        }

        public bool IsDialogueRequested { get; private set; }
        public bool IsInDialogueState { get; private set; }
        public Transform InteractorTransform { get; private set; }
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

            InteractorTransform = interactorScope != null ? interactorScope.transform : null;
            IsDialogueRequested = true;
            return true;
        }

        public void EndDialogue()
        {
            IsDialogueRequested = false;
            InteractorTransform = null;
        }

        public void EnterDialogueState()
        {
            IsInDialogueState = true;
            navMeshController?.Stop();
            navMeshController?.SetFacingLocked(true);
        }

        public void TickDialogue()
        {
            navMeshController?.Stop();
            FaceInteractor();
        }

        public void ExitDialogueState()
        {
            IsInDialogueState = false;
            navMeshController?.SetFacingLocked(false);

            if (!IsDialogueRequested)
            {
                return;
            }

            EndDialogue();
            DialogueInterrupted?.Invoke();
        }

        private void FaceInteractor()
        {
            if (ownerTransform == null || InteractorTransform == null)
            {
                return;
            }

            var direction = InteractorTransform.position - ownerTransform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            ownerTransform.rotation = Quaternion.RotateTowards(
                ownerTransform.rotation,
                Quaternion.LookRotation(direction.normalized, Vector3.up),
                GetDialogueRotationSpeed() * Time.deltaTime);
        }

        private float GetDialogueRotationSpeed()
        {
            return playerMovementConfig != null
                ? playerMovementConfig.WalkRotationSpeed
                : 215f;
        }
    }
}
