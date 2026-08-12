using System.Collections.Generic;

namespace Combat
{
    public interface INonLethalCombatSessionRegistry
    {
        void Begin(CharacterDamageReceiver first, CharacterDamageReceiver second);
        void End(CharacterDamageReceiver participant);
        bool ArePaired(CharacterDamageReceiver first, CharacterDamageReceiver second);
        float ClampIncomingWeaponDamage(CharacterDamageReceiver receiver, float requestedDamage);
    }

    /// <summary>
    /// Transient, pair-scoped combat rules. It deliberately knows neither factions nor training:
    /// any future duel can use the same non-lethal contract.
    /// </summary>
    public sealed class NonLethalCombatSessionRegistry : INonLethalCombatSessionRegistry
    {
        private readonly Dictionary<CharacterDamageReceiver, CharacterDamageReceiver> opponents = new();

        public void Begin(CharacterDamageReceiver first, CharacterDamageReceiver second)
        {
            if (first == null || second == null || first == second)
            {
                return;
            }

            End(first);
            End(second);
            opponents[first] = second;
            opponents[second] = first;
        }

        public void End(CharacterDamageReceiver participant)
        {
            if (participant == null || !opponents.Remove(participant, out var opponent))
            {
                return;
            }

            if (opponent != null && opponents.TryGetValue(opponent, out var reverse) && reverse == participant)
            {
                opponents.Remove(opponent);
            }
        }

        public bool ArePaired(CharacterDamageReceiver first, CharacterDamageReceiver second)
        {
            return first != null
                   && second != null
                   && opponents.TryGetValue(first, out var opponent)
                   && opponent == second;
        }

        public float ClampIncomingWeaponDamage(CharacterDamageReceiver receiver, float requestedDamage)
        {
            if (receiver == null || requestedDamage <= 0f || !opponents.ContainsKey(receiver))
            {
                return requestedDamage;
            }

            // Health value 1 marks defeat in a non-lethal session. Never pass a larger value to
            // StatsController, so death, faction hostility and corpse lifecycles are never reached.
            return UnityEngine.Mathf.Min(requestedDamage, UnityEngine.Mathf.Max(0f, receiver.CurrentHp - 1f));
        }
    }
}
