using UnityEngine;
using VContainer.Unity;

namespace GameAudio
{
    /// <summary>Plays footsteps from actual NPC displacement without animation-event wiring.</summary>
    public sealed class NpcFootstepPlayer : IStartable, ITickable
    {
        private readonly Transform characterTransform;
        private readonly FootstepConfig config;
        private readonly IAudioService audioService;
        private Vector3 lastPosition;
        private float traveledDistance;

        public NpcFootstepPlayer(
            Transform characterTransform,
            FootstepConfig config,
            IAudioService audioService)
        {
            this.characterTransform = characterTransform;
            this.config = config;
            this.audioService = audioService;
        }

        public void Start()
        {
            lastPosition = characterTransform.position;
        }

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

            // NpcNavMeshController moves a CharacterController manually. In this mode
            // NavMeshAgent.velocity can be zero while the NPC is visibly walking, so
            // derive footsteps solely from the actual world-space displacement.
            if (delta.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            traveledDistance += delta.magnitude;
            while (traveledDistance >= config.NpcStepDistance)
            {
                traveledDistance -= config.NpcStepDistance;
                audioService.PlayFootstep(position, characterTransform, isPlayerCharacter: false);
            }
        }
    }
}
