using System;
using UnityEngine;

namespace Factions
{
    [Serializable]
    public sealed class FactionRelationEntry
    {
        [SerializeField] private FactionConfig leftFaction;
        [SerializeField] private FactionConfig rightFaction;
        [SerializeField] private int relation;

        public FactionConfig LeftFaction => leftFaction;
        public FactionConfig RightFaction => rightFaction;
        public int Relation => relation;

        public FactionRelationEntry(FactionConfig leftFaction, FactionConfig rightFaction, int relation)
        {
            this.leftFaction = leftFaction;
            this.rightFaction = rightFaction;
            this.relation = relation;
        }

        public bool Matches(FactionConfig left, FactionConfig right)
        {
            return leftFaction == left && rightFaction == right;
        }

        public bool MatchesPair(FactionConfig first, FactionConfig second)
        {
            return (leftFaction == first && rightFaction == second)
                || (leftFaction == second && rightFaction == first);
        }

        public bool HasMissingFaction()
        {
            return leftFaction == null || rightFaction == null;
        }

        public void SetRelation(int value)
        {
            relation = value;
        }
    }
}
