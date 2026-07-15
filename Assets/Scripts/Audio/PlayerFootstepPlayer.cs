using Movement;
using UnityEngine;
using VContainer.Unity;

namespace GameAudio
{
    /// <summary>Distance-based footsteps stay in sync with the actual CharacterController movement.</summary>
    public sealed class PlayerFootstepPlayer : IStartable, ITickable
    {
        private readonly Transform characterTransform;
        private readonly CharacterController characterController;
        private readonly PlayerMovement playerMovement;
        private readonly AudioConfig config;
        private readonly IAudioService audioService;
        private Vector3 lastPosition;
        private float traveledDistance;

        public PlayerFootstepPlayer(
            Transform characterTransform,
            CharacterController characterController,
            PlayerMovement playerMovement,
            AudioConfig config,
            IAudioService audioService)
        {
            this.characterTransform = characterTransform;
            this.characterController = characterController;
            this.playerMovement = playerMovement;
            this.config = config;
            this.audioService = audioService;
        }

        public void Start() => lastPosition = characterTransform.position;

        public void Tick()
        {
            if (characterTransform == null || config == null)
            {
                return;
            }

            var position = characterTransform.position;
            var delta = position - lastPosition;
            delta.y = 0f;
            lastPosition = position;

            if (playerMovement == null || !playerMovement.IsMoving || characterController == null || !characterController.isGrounded)
            {
                traveledDistance = 0f;
                return;
            }

            traveledDistance += delta.magnitude;
            var stepDistance = playerMovement.IsRunning ? config.RunStepDistance : config.WalkStepDistance;
            while (traveledDistance >= stepDistance)
            {
                traveledDistance -= stepDistance;
                audioService.PlayFootstep(position);
            }
        }
    }
}
