using System;
using Container;
using MessagePipe;
using Messages;
using Movement;
using NPC;
using UnityEngine;
using VContainer.Unity;

namespace Dialogue
{
    /// <summary>
    /// Applies dialogue participant placement only after the screen is fully obscured.
    /// </summary>
    public sealed class DialogueParticipantPositioningCoordinator : IStartable, IDisposable
    {
        private readonly DialogueContext dialogueContext;
        private readonly Transform playerTransform;
        private readonly CharacterController playerCharacterController;
        private readonly PlayerFacingController playerFacingController;
        private readonly ISubscriber<DialogueScreenFullyObscuredMessage> fullyObscuredSubscriber;
        private IDisposable fullyObscuredSubscription;
        private Interactable.Interactable activeTarget;
        private bool hasAppliedPositioning;

        public DialogueParticipantPositioningCoordinator(
            DialogueContext dialogueContext,
            Transform playerTransform,
            CharacterController playerCharacterController,
            PlayerFacingController playerFacingController,
            ISubscriber<DialogueScreenFullyObscuredMessage> fullyObscuredSubscriber)
        {
            this.dialogueContext = dialogueContext;
            this.playerTransform = playerTransform;
            this.playerCharacterController = playerCharacterController;
            this.playerFacingController = playerFacingController;
            this.fullyObscuredSubscriber = fullyObscuredSubscriber;
        }

        public void Start()
        {
            dialogueContext.Opened += OnDialogueOpened;
            dialogueContext.Closed += OnDialogueClosed;
            fullyObscuredSubscription = fullyObscuredSubscriber.Subscribe(OnScreenFullyObscured);

            if (dialogueContext.CurrentTarget != null)
            {
                OnDialogueOpened();
            }
        }

        public void Dispose()
        {
            dialogueContext.Opened -= OnDialogueOpened;
            dialogueContext.Closed -= OnDialogueClosed;
            fullyObscuredSubscription?.Dispose();
            fullyObscuredSubscription = null;
        }

        private void OnDialogueOpened()
        {
            activeTarget = dialogueContext.CurrentTarget;
            hasAppliedPositioning = false;
        }

        private void OnDialogueClosed()
        {
            activeTarget = null;
            hasAppliedPositioning = false;
        }

        private void OnScreenFullyObscured(DialogueScreenFullyObscuredMessage _)
        {
            if (hasAppliedPositioning || activeTarget == null || dialogueContext.CurrentTarget != activeTarget)
            {
                return;
            }

            hasAppliedPositioning = true;
            var npcScope = activeTarget.GetComponentInParent<NpcLifetimeScope>();
            if (npcScope == null)
            {
                return;
            }

            if (npcScope.Container.TryResolve(typeof(NpcDialoguePositioning), out var service)
                && service is NpcDialoguePositioning positioning)
            {
                if (positioning.TeleportPlayerToDialoguePosition)
                {
                    var pose = positioning.GetPlayerDialoguePose();
                    CharacterPlacementUtility.SetPose(
                        playerTransform,
                        playerCharacterController,
                        pose);
                    playerFacingController.ForceFacing(pose.rotation);
                    return;
                }

                if (positioning.TryRequestMoveAwayFrom(playerTransform.position, out var futureNpcPosition))
                {
                    RotatePlayerToward(futureNpcPosition);
                }
            }
        }

        private void RotatePlayerToward(Vector3 targetPosition)
        {
            if (playerTransform == null)
            {
                return;
            }

            var direction = targetPosition - playerTransform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            var pose = new Pose(playerTransform.position, Quaternion.LookRotation(direction.normalized, Vector3.up));
            CharacterPlacementUtility.SetPose(playerTransform, playerCharacterController, pose);
            playerFacingController.ForceFacing(pose.rotation);
        }
    }
}
