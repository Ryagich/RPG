using NPC;
using UnityEngine;
using VContainer.Unity;

namespace GameAudio
{
    /// <summary>Uses NPC NavMesh movement, so every bot gets footsteps without animation-event wiring.</summary>
    public sealed class NpcFootstepPlayer : IStartable, ITickable
    {
        private readonly Transform characterTransform;
        private readonly CharacterController characterController;
        private readonly NpcNavMeshController navigation;
        private readonly AudioConfig config;
        private readonly IAudioService audioService;
        private Vector3 lastPosition;
        private float traveledDistance;

        public NpcFootstepPlayer(
            Transform characterTransform,
            CharacterController characterController,
            NpcNavMeshController navigation,
            AudioConfig config,
            IAudioService audioService)
        {
            this.characterTransform = characterTransform;
            this.characterController = characterController;
            this.navigation = navigation;
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

            if (navigation == null || !navigation.IsMoving || characterController == null || !characterController.isGrounded)
            {
                traveledDistance = 0f;
                return;
            }

            traveledDistance += delta.magnitude;
            while (traveledDistance >= config.NpcStepDistance)
            {
                traveledDistance -= config.NpcStepDistance;
                audioService.PlayFootstep(position);
            }
        }
    }
}
