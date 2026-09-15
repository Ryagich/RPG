using System;
using Dialogue;
using Locations;
using UnityEngine;
using VContainer.Unity;

namespace Saves
{
    /// <summary>Coordinates one complete checkpoint for every completed dialogue lifecycle.</summary>
    public sealed class DialogueSaveCoordinator : IStartable, IDisposable
    {
        private readonly DialogueContext dialogueContext;
        private readonly LocationTransitionService locationTransitions;
        private readonly GameSaveController saveController;
        private bool hasCheckpointForCurrentDialogue;
        private bool suppressCheckpointForCurrentDialogue;

        public DialogueSaveCoordinator(
            DialogueContext dialogueContext,
            LocationTransitionService locationTransitions,
            GameSaveController saveController)
        {
            this.dialogueContext = dialogueContext;
            this.locationTransitions = locationTransitions;
            this.saveController = saveController;
        }

        public void Start()
        {
            dialogueContext.Opened += OnDialogueOpened;
            dialogueContext.Closed += SaveFullCheckpoint;
        }

        public void Dispose()
        {
            dialogueContext.Opened -= OnDialogueOpened;
            dialogueContext.Closed -= SaveFullCheckpoint;
        }

        public bool SaveBeforeExit()
        {
            return TrySaveCurrentDialogue();
        }

        private void OnDialogueOpened()
        {
            hasCheckpointForCurrentDialogue = false;
            suppressCheckpointForCurrentDialogue = dialogueContext.CurrentDialog?.SuppressCheckpointOnClose == true;
        }

        private void SaveFullCheckpoint()
        {
            TrySaveCurrentDialogue();
        }

        private bool TrySaveCurrentDialogue()
        {
            if (suppressCheckpointForCurrentDialogue)
            {
                return true;
            }

            if (hasCheckpointForCurrentDialogue)
            {
                return true;
            }

            bool saved = saveController.SaveFullAtCurrentPlayerPosition(
                locationTransitions.CurrentLocation?.Id,
                locationTransitions.CurrentEntrance?.Id);
            if (!saved)
            {
                Debug.LogError("Dialogue checkpoint was skipped because player save state is not registered.");
            }

            hasCheckpointForCurrentDialogue = saved;
            return saved;
        }
    }
}
