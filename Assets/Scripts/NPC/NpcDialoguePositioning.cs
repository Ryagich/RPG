using UnityEngine;

namespace NPC
{
    /// <summary>
    /// Owns the optional NPC repositioning phase inside an active dialogue state.
    /// </summary>
    public sealed class NpcDialoguePositioning
    {
        private const float StoppingDistance = 0.05f;

        private readonly Transform npcTransform;
        private readonly NpcNavMeshController navigation;
        private readonly NpcDialoguePositioningSettings settings;
        private Vector3 destination;
        private bool hasRequestedMove;
        private bool hasStartedMove;

        public NpcDialoguePositioning(
            Transform npcTransform,
            NpcNavMeshController navigation,
            NpcDialoguePositioningSettings settings)
        {
            this.npcTransform = npcTransform;
            this.navigation = navigation;
            this.settings = settings;
        }

        public bool IsActive => hasRequestedMove;
        public bool TeleportPlayerToDialoguePosition => settings.TeleportPlayerToDialoguePosition;

        public Pose GetPlayerDialoguePose()
        {
            if (npcTransform == null)
            {
                return default;
            }

            var forward = GetPlanarForward(npcTransform.forward);
            return new Pose(
                npcTransform.position + forward * settings.ParticipantDistance,
                Quaternion.LookRotation(-forward, Vector3.up));
        }

        public bool TryRequestMoveAwayFrom(Vector3 participantPosition, out Vector3 targetPosition)
        {
            targetPosition = default;
            if (npcTransform == null || settings.TeleportPlayerToDialoguePosition)
            {
                return false;
            }

            var forward = GetPlanarForward(npcTransform.forward);
            targetPosition = participantPosition - forward * settings.ParticipantDistance;
            destination = targetPosition;
            hasRequestedMove = true;
            hasStartedMove = false;
            return true;
        }

        /// <summary>Returns true while dialogue owns navigation to the conversation distance.</summary>
        public bool Tick()
        {
            if (!hasRequestedMove)
            {
                return false;
            }

            if (!hasStartedMove)
            {
                hasStartedMove = navigation != null && navigation.MoveTo(destination, stoppingDistance: StoppingDistance);
                if (!hasStartedMove)
                {
                    ClearRequest();
                }

                return hasStartedMove;
            }

            if (!navigation.HasReachedDestination)
            {
                return true;
            }

            navigation.Stop();
            ClearRequest();
            return false;
        }

        public void Cancel()
        {
            if (!hasRequestedMove)
            {
                return;
            }

            navigation?.Stop();
            ClearRequest();
        }

        private void ClearRequest()
        {
            hasRequestedMove = false;
            hasStartedMove = false;
            destination = default;
        }

        private static Vector3 GetPlanarForward(Vector3 forward)
        {
            forward.y = 0f;
            return forward.sqrMagnitude > Mathf.Epsilon ? forward.normalized : Vector3.forward;
        }
    }

    public sealed class NpcDialoguePositioningSettings
    {
        public bool TeleportPlayerToDialoguePosition { get; }
        public float ParticipantDistance { get; }

        public NpcDialoguePositioningSettings(bool teleportPlayerToDialoguePosition, float participantDistance)
        {
            TeleportPlayerToDialoguePosition = teleportPlayerToDialoguePosition;
            ParticipantDistance = Mathf.Max(0f, participantDistance);
        }
    }
}
