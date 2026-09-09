using System;
using System.Collections.Generic;
using System.Linq;

namespace Factions
{
    /// <summary>
    /// Runtime owner of mutable faction standing. It is seeded from the immutable authored
    /// config and never writes back to that asset.
    /// </summary>
    public interface IFactionRelations
    {
        int GetRelation(FactionConfig leftFaction, FactionConfig rightFaction);
        bool IsHostile(FactionConfig leftFaction, FactionConfig rightFaction);
        bool IsFriendly(FactionConfig leftFaction, FactionConfig rightFaction);
        bool TryChangeRelation(FactionConfig leftFaction, FactionConfig rightFaction, int amount);
        event Action Changed;
    }

    public readonly struct FactionRelationState
    {
        public FactionConfig LeftFaction { get; }
        public FactionConfig RightFaction { get; }
        public int Relation { get; }

        public FactionRelationState(FactionConfig leftFaction, FactionConfig rightFaction, int relation)
        {
            LeftFaction = leftFaction;
            RightFaction = rightFaction;
            Relation = relation;
        }
    }

    public sealed class RuntimeFactionRelations : IFactionRelations
    {
        private readonly FactionRelationsConfig defaults;
        private readonly Dictionary<FactionPair, int> relations = new();

        public event Action Changed;

        public RuntimeFactionRelations(FactionRelationsConfig defaults)
        {
            this.defaults = defaults;
            ResetToDefaults();
        }

        public void ResetToDefaults()
        {
            relations.Clear();
            if (defaults?.Relations == null)
            {
                Changed?.Invoke();
                return;
            }

            foreach (FactionRelationEntry entry in defaults.Relations)
            {
                if (entry is { LeftFaction: not null, RightFaction: not null })
                {
                    relations[new FactionPair(entry.LeftFaction, entry.RightFaction)] = entry.Relation;
                }
            }

            Changed?.Invoke();
        }

        public int GetRelation(FactionConfig leftFaction, FactionConfig rightFaction)
        {
            if (leftFaction == null || rightFaction == null || leftFaction == rightFaction)
            {
                return 0;
            }

            return relations.TryGetValue(new FactionPair(leftFaction, rightFaction), out int value)
                ? value
                : 0;
        }

        public bool IsHostile(FactionConfig leftFaction, FactionConfig rightFaction) =>
            defaults != null && GetRelation(leftFaction, rightFaction) < defaults.HostileBelowRelation;

        public bool IsFriendly(FactionConfig leftFaction, FactionConfig rightFaction) =>
            defaults != null && GetRelation(leftFaction, rightFaction) > defaults.FriendlyAboveRelation;

        public bool TryChangeRelation(FactionConfig leftFaction, FactionConfig rightFaction, int amount)
        {
            if (amount == 0 || leftFaction == null || rightFaction == null || leftFaction == rightFaction)
            {
                return false;
            }

            var pair = new FactionPair(leftFaction, rightFaction);
            relations[pair] = GetRelation(leftFaction, rightFaction) + amount;
            Changed?.Invoke();
            return true;
        }

        public IReadOnlyList<FactionRelationState> GetPersistenceSnapshot()
        {
            return relations
                .Select(entry => new FactionRelationState(entry.Key.Left, entry.Key.Right, entry.Value))
                .Where(entry => entry.LeftFaction != null && entry.RightFaction != null)
                .ToArray();
        }

        public void Restore(IEnumerable<FactionRelationState> savedRelations)
        {
            if (savedRelations == null)
            {
                return;
            }

            foreach (FactionRelationState state in savedRelations)
            {
                if (state.LeftFaction == null || state.RightFaction == null || state.LeftFaction == state.RightFaction)
                {
                    continue;
                }

                relations[new FactionPair(state.LeftFaction, state.RightFaction)] = state.Relation;
            }
        }

        public bool TryGetFaction(string persistentId, out FactionConfig faction)
        {
            faction = null;
            if (string.IsNullOrWhiteSpace(persistentId))
            {
                return false;
            }

            foreach (FactionPair pair in relations.Keys)
            {
                if (pair.Left != null && pair.Left.PersistentId == persistentId)
                {
                    faction = pair.Left;
                    return true;
                }

                if (pair.Right != null && pair.Right.PersistentId == persistentId)
                {
                    faction = pair.Right;
                    return true;
                }
            }

            return false;
        }

        private readonly struct FactionPair : IEquatable<FactionPair>
        {
            public FactionConfig Left { get; }
            public FactionConfig Right { get; }

            public FactionPair(FactionConfig first, FactionConfig second)
            {
                if (first.GetInstanceID() < second.GetInstanceID())
                {
                    Left = first;
                    Right = second;
                }
                else
                {
                    Left = second;
                    Right = first;
                }
            }

            public bool Equals(FactionPair other) => Left == other.Left && Right == other.Right;
            public override bool Equals(object obj) => obj is FactionPair other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Left, Right);
        }
    }
}
