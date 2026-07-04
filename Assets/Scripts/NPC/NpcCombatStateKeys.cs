namespace NPC
{
    public static class NpcCombatStateKeys
    {
        public const string LastKnownLookTimer = "NpcCombat.LastKnownLookTimer";
        public const string FleeDecisionTimer = "NpcCombat.FleeDecisionTimer";
        public const string FleeLookTimer = "NpcCombat.FleeLookTimer";
        public const string FleeLookingBack = "NpcCombat.FleeLookingBack";
        public const string FleeDamageTriggered = "NpcCombat.FleeDamageTriggered";
        public const string FleeCompleted = "NpcCombat.FleeCompleted";
        public const string AttackRequested = "NpcCombat.AttackRequested";
        public const string AttackBlockObserved = "NpcCombat.AttackBlockObserved";
        public const string AttackElapsed = "NpcCombat.AttackElapsed";
        public const string AttackCompleted = "NpcCombat.AttackCompleted";
        public const string ComboAttackRequests = "NpcCombat.ComboAttackRequests";
        public const string ComboAttackNextRequestTime = "NpcCombat.ComboAttackNextRequestTime";
        public const string PostAttackDecision = "NpcCombat.PostAttackDecision";
        public const string WaitTimer = "NpcCombat.WaitTimer";
        public const string WaitDuration = "NpcCombat.WaitDuration";
        public const string KeepDistanceTimer = "NpcCombat.KeepDistanceTimer";
        public const string KeepDistanceDuration = "NpcCombat.KeepDistanceDuration";
        public const string KeepDistanceNextRepositionTime = "NpcCombat.KeepDistanceNextRepositionTime";
        public const string CombatMoveCompleted = "NpcCombat.CombatMoveCompleted";
        public const string CombatMoveLastPosition = "NpcCombat.CombatMoveLastPosition";
        public const string CombatMoveStuckTimer = "NpcCombat.CombatMoveStuckTimer";
        public const string InitialCircleRequested = "NpcCombat.InitialCircleRequested";
        public const string TargetDownWaitTimer = "NpcCombat.TargetDownWaitTimer";
        public const string TargetDownWaitCompleted = "NpcCombat.TargetDownWaitCompleted";
    }
}
