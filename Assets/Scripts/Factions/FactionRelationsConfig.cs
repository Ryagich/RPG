using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Factions
{
    [CreateAssetMenu(fileName = "FactionRelationsConfig", menuName = "configs/Factions/Faction Relations")]
    public sealed class FactionRelationsConfig : ScriptableObject
    {
        [SerializeField] private int hostileBelowRelation = -50;
        [SerializeField] private int friendlyAboveRelation = 50;
        [SerializeField] private List<FactionRelationEntry> relations = new();

        public int HostileBelowRelation => hostileBelowRelation;
        public int FriendlyAboveRelation => friendlyAboveRelation;
        public IReadOnlyList<FactionRelationEntry> Relations => relations;

        public int GetRelation(FactionConfig leftFaction, FactionConfig rightFaction)
        {
            return FindRelation(leftFaction, rightFaction)?.Relation ?? 0;
        }

        public bool IsHostile(FactionConfig leftFaction, FactionConfig rightFaction)
        {
            return GetRelation(leftFaction, rightFaction) < hostileBelowRelation;
        }

        public bool IsFriendly(FactionConfig leftFaction, FactionConfig rightFaction)
        {
            return GetRelation(leftFaction, rightFaction) > friendlyAboveRelation;
        }

        public bool IsNeutral(FactionConfig leftFaction, FactionConfig rightFaction)
        {
            var relation = GetRelation(leftFaction, rightFaction);
            return relation >= hostileBelowRelation && relation <= friendlyAboveRelation;
        }

        public void SetRelation(FactionConfig leftFaction, FactionConfig rightFaction, int value)
        {
            if (leftFaction == null || rightFaction == null || leftFaction == rightFaction)
            {
                return;
            }

            var entry = FindRelation(leftFaction, rightFaction);
            if (entry == null)
            {
                entry = new FactionRelationEntry(leftFaction, rightFaction, 0);
                relations.Add(entry);
            }

            entry.SetRelation(value);
        }

        public bool SyncWithFactions(IEnumerable<FactionConfig> factionConfigs)
        {
            var factions = factionConfigs?
                .Where(faction => faction != null)
                .Distinct()
                .OrderBy(faction => faction.name)
                .ToList() ?? new List<FactionConfig>();

            var expectedPairs = new HashSet<(FactionConfig Left, FactionConfig Right)>();
            for (var leftIndex = 0; leftIndex < factions.Count; leftIndex++)
            {
                for (var rightIndex = leftIndex + 1; rightIndex < factions.Count; rightIndex++)
                {
                    expectedPairs.Add((factions[leftIndex], factions[rightIndex]));
                }
            }

            var registeredPairs = new HashSet<(FactionConfig Left, FactionConfig Right)>();
            var changed = relations.RemoveAll(entry =>
            {
                if (entry == null
                 || entry.HasMissingFaction()
                 || entry.LeftFaction == entry.RightFaction
                 || !factions.Contains(entry.LeftFaction)
                 || !factions.Contains(entry.RightFaction))
                {
                    return true;
                }

                var pair = NormalizePair(entry.LeftFaction, entry.RightFaction, factions);
                if (!expectedPairs.Contains(pair) || registeredPairs.Contains(pair))
                {
                    return true;
                }

                registeredPairs.Add(pair);
                return false;
            }) > 0;

            foreach (var pair in expectedPairs)
            {
                if (FindRelation(pair.Left, pair.Right) != null)
                {
                    continue;
                }

                relations.Add(new FactionRelationEntry(pair.Left, pair.Right, 0));
                changed = true;
            }

            var sortedRelations = relations
                .OrderBy(entry => entry.LeftFaction.name)
                .ThenBy(entry => entry.RightFaction.name)
                .ToList();

            if (!relations.SequenceEqual(sortedRelations))
            {
                relations = sortedRelations;
                changed = true;
            }

            return changed;
        }

        private FactionRelationEntry FindRelation(FactionConfig leftFaction, FactionConfig rightFaction)
        {
            return relations.FirstOrDefault(entry => entry != null && entry.MatchesPair(leftFaction, rightFaction));
        }

        private static (FactionConfig Left, FactionConfig Right) NormalizePair(
            FactionConfig first,
            FactionConfig second,
            IReadOnlyList<FactionConfig> sortedFactions)
        {
            var firstIndex = IndexOf(sortedFactions, first);
            var secondIndex = IndexOf(sortedFactions, second);
            return firstIndex <= secondIndex
                ? (first, second)
                : (second, first);
        }

        private static int IndexOf(IReadOnlyList<FactionConfig> factions, FactionConfig faction)
        {
            for (var index = 0; index < factions.Count; index++)
            {
                if (factions[index] == faction)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
