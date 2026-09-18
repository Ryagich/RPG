using System;
using System.Collections.Generic;
using System.Linq;
using YG;

namespace Saves
{
    internal interface INpcCharacterSaveParticipant
    {
        string CharacterId { get; }
        bool IsAvailableForSaving { get; }
        SavedCharacterState CaptureSaveState();
    }

    /// <summary>
    /// Holds only NPCs that are currently instantiated. GameSaveService merges their snapshots
    /// into the long-lived save payload, so state for NPCs in other locations is preserved.
    /// </summary>
    public sealed class NpcCharacterSaveRegistry
    {
        private readonly Dictionary<string, INpcCharacterSaveParticipant> participants = new();

        internal bool Register(INpcCharacterSaveParticipant participant)
        {
            if (participant == null || string.IsNullOrWhiteSpace(participant.CharacterId))
            {
                return false;
            }

            if (participants.TryGetValue(participant.CharacterId, out INpcCharacterSaveParticipant existing) &&
                !ReferenceEquals(existing, participant))
            {
                // Location loads can overlap Unity object destruction by one frame. The project
                // registry must never let a coordinator from an already-disposed NPC scope block
                // the newly-created scene instance with the same stable identity.
                if (!existing.IsAvailableForSaving)
                {
                    participants[participant.CharacterId] = participant;
                    return true;
                }

                UnityEngine.Debug.LogError(
                    $"Several NPC instances use persistent character id '{participant.CharacterId}'. " +
                    "Each scene NPC must have its own id.");
                return false;
            }

            participants[participant.CharacterId] = participant;
            return true;
        }

        internal void Unregister(INpcCharacterSaveParticipant participant)
        {
            if (participant == null || string.IsNullOrWhiteSpace(participant.CharacterId))
            {
                return;
            }

            if (participants.TryGetValue(participant.CharacterId, out INpcCharacterSaveParticipant existing) &&
                ReferenceEquals(existing, participant))
            {
                participants.Remove(participant.CharacterId);
            }
        }

        /// <summary>
        /// A new game starts a new world timeline. Any scopes that may still be completing
        /// teardown must not contribute state to the first checkpoint of that timeline.
        /// </summary>
        public void Clear()
        {
            participants.Clear();
        }

        public SavedCharacterState[] CaptureStates()
        {
            return participants.Values
                .Where(participant => participant.IsAvailableForSaving)
                .Select(participant => participant.CaptureSaveState())
                .Where(state => state != null && !string.IsNullOrWhiteSpace(state.characterId))
                .ToArray();
        }
    }
}
