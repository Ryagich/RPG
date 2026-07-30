using System.Collections.Generic;
using Container;
using NPC;
using Player;
using StateMachine.Behaviours;
using StateMachine.Conditions;
using StateMachine.Graph;
using StateMachine.Graph.Model;
using TargetLock;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor.SceneManagement;

namespace EditorTools
{
    public static class NpcSetupMenu
    {
        private const string MenuPath = "Tools/RPG/NPC/Create Base NPC Assets";
        private const string SetupNavMeshMenuPath = "Tools/RPG/NPC/Setup NavMesh For Active Scene";
        private const string StateMachineFolderPath = "Assets/StateMachines/NPC";
        private const string StatesFolderPath = StateMachineFolderPath + "/States";
        private const string TransitionsFolderPath = StateMachineFolderPath + "/Transitions";
        private const string ConditionsFolderPath = StateMachineFolderPath + "/Conditions";
        private const string BehavioursFolderPath = StateMachineFolderPath + "/Behaviours";
        private const string NpcConfigFolderPath = "Assets/Configs/NPC";
        private const string GraphPath = "Assets/StateMachines/NpcBaseStateMachineGraph.asset";
        private const string IdleStatePath = StatesFolderPath + "/NpcIdleState.asset";
        private const string DeathStatePath = StatesFolderPath + "/NpcDeathState.asset";
        private const string MoveToItemStatePath = StatesFolderPath + "/NpcMoveToInterestedItemState.asset";
        private const string PickupWaitStatePath = StatesFolderPath + "/NpcPickupItemWaitState.asset";
        private const string PickupStatePath = StatesFolderPath + "/NpcPickupItemState.asset";
        private const string ReturnHomeStatePath = StatesFolderPath + "/NpcReturnHomeState.asset";
        private const string DialogueStatePath = StatesFolderPath + "/NpcDialogueState.asset";
        private const string HitReactionStatePath = StatesFolderPath + "/NpcHitReactionState.asset";
        private const string FleeStatePath = StatesFolderPath + "/NpcFleeState.asset";
        private const string CombatApproachStatePath = StatesFolderPath + "/NpcCombatApproachState.asset";
        private const string CombatAttackStatePath = StatesFolderPath + "/NpcCombatAttackState.asset";
        private const string PostAttackDecisionStatePath = StatesFolderPath + "/NpcPostAttackDecisionState.asset";
        private const string CombatEvasionStatePath = StatesFolderPath + "/NpcCombatEvasionState.asset";
        private const string CombatManeuverStatePath = StatesFolderPath + "/NpcCombatManeuverState.asset";
        private const string CombatCircleStatePath = StatesFolderPath + "/NpcCombatCircleState.asset";
        private const string CombatWaitStatePath = StatesFolderPath + "/NpcCombatWaitState.asset";
        private const string CombatKeepDistanceStatePath = StatesFolderPath + "/NpcCombatKeepDistanceState.asset";
        private const string CombatQueueCircleStatePath = StatesFolderPath + "/NpcCombatQueueCircleState.asset";
        private const string CombatTargetDownStatePath = StatesFolderPath + "/NpcCombatTargetDownState.asset";
        private const string CombatSearchLastKnownStatePath = StatesFolderPath + "/NpcCombatSearchLastKnownState.asset";
        private const string CombatReturnHomeStatePath = StatesFolderPath + "/NpcCombatReturnHomeState.asset";
        private const string DeathTransitionPath = TransitionsFolderPath + "/NpcIdleToDeathTransition.asset";
        private const string AnyToHitReactionTransitionPath = TransitionsFolderPath + "/NpcAnyToHitReactionTransition.asset";
        private const string HitReactionToFleeTransitionPath = TransitionsFolderPath + "/NpcHitReactionToFleeTransition.asset";
        private const string HitReactionToCombatApproachTransitionPath = TransitionsFolderPath + "/NpcHitReactionToCombatApproachTransition.asset";
        private const string HitReactionToSearchLastKnownTransitionPath = TransitionsFolderPath + "/NpcHitReactionToSearchLastKnownTransition.asset";
        private const string HitReactionToIdleTransitionPath = TransitionsFolderPath + "/NpcHitReactionToIdleTransition.asset";
        private const string IdleToMoveTransitionPath = TransitionsFolderPath + "/NpcIdleToMoveToItemTransition.asset";
        private const string IdleToFleeTransitionPath = TransitionsFolderPath + "/NpcIdleToFleeTransition.asset";
        private const string IdleToCombatApproachTransitionPath = TransitionsFolderPath + "/NpcIdleToCombatApproachTransition.asset";
        private const string MoveToItemToFleeTransitionPath = TransitionsFolderPath + "/NpcMoveToItemToFleeTransition.asset";
        private const string MoveToItemToCombatApproachTransitionPath = TransitionsFolderPath + "/NpcMoveToItemToCombatApproachTransition.asset";
        private const string PickupWaitToFleeTransitionPath = TransitionsFolderPath + "/NpcPickupWaitToFleeTransition.asset";
        private const string PickupWaitToCombatApproachTransitionPath = TransitionsFolderPath + "/NpcPickupWaitToCombatApproachTransition.asset";
        private const string PickupToFleeTransitionPath = TransitionsFolderPath + "/NpcPickupToFleeTransition.asset";
        private const string PickupToCombatApproachTransitionPath = TransitionsFolderPath + "/NpcPickupToCombatApproachTransition.asset";
        private const string ReturnHomeToFleeTransitionPath = TransitionsFolderPath + "/NpcReturnHomeToFleeTransition.asset";
        private const string ReturnHomeToCombatApproachTransitionPath = TransitionsFolderPath + "/NpcReturnHomeToCombatApproachTransition.asset";
        private const string IdleToDialogueTransitionPath = TransitionsFolderPath + "/NpcIdleToDialogueTransition.asset";
        private const string MoveToItemToDialogueTransitionPath = TransitionsFolderPath + "/NpcMoveToItemToDialogueTransition.asset";
        private const string PickupWaitToDialogueTransitionPath = TransitionsFolderPath + "/NpcPickupWaitToDialogueTransition.asset";
        private const string PickupToDialogueTransitionPath = TransitionsFolderPath + "/NpcPickupToDialogueTransition.asset";
        private const string ReturnHomeToDialogueTransitionPath = TransitionsFolderPath + "/NpcReturnHomeToDialogueTransition.asset";
        private const string DialogueToFleeTransitionPath = TransitionsFolderPath + "/NpcDialogueToFleeTransition.asset";
        private const string DialogueToCombatApproachTransitionPath = TransitionsFolderPath + "/NpcDialogueToCombatApproachTransition.asset";
        private const string DialogueToIdleTransitionPath = TransitionsFolderPath + "/NpcDialogueToIdleTransition.asset";
        private const string FleeToCombatApproachTransitionPath = TransitionsFolderPath + "/NpcFleeToCombatApproachTransition.asset";
        private const string FleeToIdleTransitionPath = TransitionsFolderPath + "/NpcFleeToIdleTransition.asset";
        private const string CombatApproachToCircleTransitionPath = TransitionsFolderPath + "/NpcCombatApproachToCircleTransition.asset";
        private const string CombatApproachToQueueTransitionPath = TransitionsFolderPath + "/NpcCombatApproachToQueueTransition.asset";
        private const string CombatApproachToTargetDownTransitionPath = TransitionsFolderPath + "/NpcCombatApproachToTargetDownTransition.asset";
        private const string CombatApproachToAttackTransitionPath = TransitionsFolderPath + "/NpcCombatApproachToAttackTransition.asset";
        private const string CombatApproachToSearchLastKnownTransitionPath = TransitionsFolderPath + "/NpcCombatApproachToSearchLastKnownTransition.asset";
        private const string CombatAttackToPostAttackDecisionTransitionPath = TransitionsFolderPath + "/NpcCombatAttackToPostAttackDecisionTransition.asset";
        private const string CombatAttackToTargetDownTransitionPath = TransitionsFolderPath + "/NpcCombatAttackToTargetDownTransition.asset";
        private const string CombatAttackToSearchLastKnownTransitionPath = TransitionsFolderPath + "/NpcCombatAttackToSearchLastKnownTransition.asset";
        private const string PostAttackDecisionToTargetDownTransitionPath = TransitionsFolderPath + "/NpcPostAttackDecisionToTargetDownTransition.asset";
        private const string PostAttackDecisionToQueueTransitionPath = TransitionsFolderPath + "/NpcPostAttackDecisionToQueueTransition.asset";
        private const string PostAttackDecisionToAttackTransitionPath = TransitionsFolderPath + "/NpcPostAttackDecisionToAttackTransition.asset";
        private const string PostAttackDecisionToApproachTransitionPath = TransitionsFolderPath + "/NpcPostAttackDecisionToApproachTransition.asset";
        private const string PostAttackDecisionToEvasionTransitionPath = TransitionsFolderPath + "/NpcPostAttackDecisionToEvasionTransition.asset";
        private const string PostAttackDecisionToManeuverTransitionPath = TransitionsFolderPath + "/NpcPostAttackDecisionToManeuverTransition.asset";
        private const string PostAttackDecisionToCircleTransitionPath = TransitionsFolderPath + "/NpcPostAttackDecisionToCircleTransition.asset";
        private const string PostAttackDecisionToWaitTransitionPath = TransitionsFolderPath + "/NpcPostAttackDecisionToWaitTransition.asset";
        private const string PostAttackDecisionToKeepDistanceTransitionPath = TransitionsFolderPath + "/NpcPostAttackDecisionToKeepDistanceTransition.asset";
        private const string CombatManeuverToTargetDownTransitionPath = TransitionsFolderPath + "/NpcCombatManeuverToTargetDownTransition.asset";
        private const string CombatManeuverToApproachTransitionPath = TransitionsFolderPath + "/NpcCombatManeuverToApproachTransition.asset";
        private const string CombatEvasionToTargetDownTransitionPath = TransitionsFolderPath + "/NpcCombatEvasionToTargetDownTransition.asset";
        private const string CombatEvasionToSearchLastKnownTransitionPath = TransitionsFolderPath + "/NpcCombatEvasionToSearchLastKnownTransition.asset";
        private const string CombatEvasionToPostAttackDecisionTransitionPath = TransitionsFolderPath + "/NpcCombatEvasionToPostAttackDecisionTransition.asset";
        private const string CombatCircleToTargetDownTransitionPath = TransitionsFolderPath + "/NpcCombatCircleToTargetDownTransition.asset";
        private const string CombatCircleToApproachTransitionPath = TransitionsFolderPath + "/NpcCombatCircleToApproachTransition.asset";
        private const string CombatWaitToTargetDownTransitionPath = TransitionsFolderPath + "/NpcCombatWaitToTargetDownTransition.asset";
        private const string CombatWaitToSearchLastKnownTransitionPath = TransitionsFolderPath + "/NpcCombatWaitToSearchLastKnownTransition.asset";
        private const string CombatWaitToPostAttackDecisionTransitionPath = TransitionsFolderPath + "/NpcCombatWaitToPostAttackDecisionTransition.asset";
        private const string CombatKeepDistanceToTargetDownTransitionPath = TransitionsFolderPath + "/NpcCombatKeepDistanceToTargetDownTransition.asset";
        private const string CombatKeepDistanceToSearchLastKnownTransitionPath = TransitionsFolderPath + "/NpcCombatKeepDistanceToSearchLastKnownTransition.asset";
        private const string CombatKeepDistanceToEvasionTransitionPath = TransitionsFolderPath + "/NpcCombatKeepDistanceToEvasionTransition.asset";
        private const string CombatKeepDistanceToAttackTransitionPath = TransitionsFolderPath + "/NpcCombatKeepDistanceToAttackTransition.asset";
        private const string CombatKeepDistanceToPostAttackDecisionTransitionPath = TransitionsFolderPath + "/NpcCombatKeepDistanceToPostAttackDecisionTransition.asset";
        private const string CombatQueueToTargetDownTransitionPath = TransitionsFolderPath + "/NpcCombatQueueToTargetDownTransition.asset";
        private const string CombatQueueToApproachTransitionPath = TransitionsFolderPath + "/NpcCombatQueueToApproachTransition.asset";
        private const string CombatQueueToSearchLastKnownTransitionPath = TransitionsFolderPath + "/NpcCombatQueueToSearchLastKnownTransition.asset";
        private const string CombatTargetDownToApproachTransitionPath = TransitionsFolderPath + "/NpcCombatTargetDownToApproachTransition.asset";
        private const string CombatTargetDownToReturnHomeTransitionPath = TransitionsFolderPath + "/NpcCombatTargetDownToReturnHomeTransition.asset";
        private const string CombatSearchToTargetDownTransitionPath = TransitionsFolderPath + "/NpcCombatSearchToTargetDownTransition.asset";
        private const string CombatSearchToApproachTransitionPath = TransitionsFolderPath + "/NpcCombatSearchToApproachTransition.asset";
        private const string CombatSearchToReturnHomeTransitionPath = TransitionsFolderPath + "/NpcCombatSearchToReturnHomeTransition.asset";
        private const string CombatReturnHomeToIdleTransitionPath = TransitionsFolderPath + "/NpcCombatReturnHomeToIdleTransition.asset";
        private const string MoveToWaitTransitionPath = TransitionsFolderPath + "/NpcMoveToItemToPickupWaitTransition.asset";
        private const string WaitToPickupTransitionPath = TransitionsFolderPath + "/NpcPickupWaitToPickupTransition.asset";
        private const string PickupToReturnTransitionPath = TransitionsFolderPath + "/NpcPickupToReturnHomeTransition.asset";
        private const string PickupToMoveTransitionPath = TransitionsFolderPath + "/NpcPickupToMoveToItemTransition.asset";
        private const string ReturnToIdleTransitionPath = TransitionsFolderPath + "/NpcReturnHomeToIdleTransition.asset";
        private const string MissingItemToReturnTransitionPath = TransitionsFolderPath + "/NpcMissingItemToReturnHomeTransition.asset";
        private const string MissingItemToNextPickupTransitionPath = TransitionsFolderPath + "/NpcMissingItemToNextPickupTransition.asset";
        private const string LostCompetitionToReturnTransitionPath = TransitionsFolderPath + "/NpcLostCompetitionToReturnHomeTransition.asset";
        private const string HpDepletedConditionPath = ConditionsFolderPath + "/NpcHpDepletedCondition.asset";
        private const string HasInterestingItemConditionPath = ConditionsFolderPath + "/NpcHasInterestingItemCondition.asset";
        private const string ReachedInterestedItemConditionPath = ConditionsFolderPath + "/NpcReachedInterestedItemCondition.asset";
        private const string PickupWaitElapsedConditionPath = ConditionsFolderPath + "/NpcPickupWaitElapsedCondition.asset";
        private const string InterestedItemMissingConditionPath = ConditionsFolderPath + "/NpcInterestedItemMissingCondition.asset";
        private const string SwitchToNextPickupConditionPath = ConditionsFolderPath + "/NpcSwitchToNextPickupCondition.asset";
        private const string LostCompetitionConditionPath = ConditionsFolderPath + "/NpcLostItemCompetitionCondition.asset";
        private const string PickupCompletedConditionPath = ConditionsFolderPath + "/NpcPickupCompletedCondition.asset";
        private const string ChainPickupFoundConditionPath = ConditionsFolderPath + "/NpcChainPickupFoundCondition.asset";
        private const string ReachedHomeConditionPath = ConditionsFolderPath + "/NpcReachedHomeCondition.asset";
        private const string HitReactionActiveConditionPath = ConditionsFolderPath + "/NpcHitReactionActiveCondition.asset";
        private const string HitReactionInactiveConditionPath = ConditionsFolderPath + "/NpcHitReactionInactiveCondition.asset";
        private const string CanFightTargetConditionPath = ConditionsFolderPath + "/NpcCanFightTargetCondition.asset";
        private const string CanFightVisibleTargetConditionPath = ConditionsFolderPath + "/NpcCanFightVisibleTargetCondition.asset";
        private const string VisibleThreatWithoutWeaponConditionPath = ConditionsFolderPath + "/NpcVisibleThreatWithoutWeaponCondition.asset";
        private const string ShouldFleeConditionPath = ConditionsFolderPath + "/NpcShouldFleeCondition.asset";
        private const string FleeCompletedConditionPath = ConditionsFolderPath + "/NpcFleeCompletedCondition.asset";
        private const string AttackCompletedConditionPath = ConditionsFolderPath + "/NpcAttackCompletedCondition.asset";
        private const string CombatMoveCompletedConditionPath = ConditionsFolderPath + "/NpcCombatMoveCompletedCondition.asset";
        private const string CombatTargetDownConditionPath = ConditionsFolderPath + "/NpcCombatTargetDownCondition.asset";
        private const string ShouldQueueForCombatSlotConditionPath = ConditionsFolderPath + "/NpcShouldQueueForCombatSlotCondition.asset";
        private const string HasDirectCombatSlotConditionPath = ConditionsFolderPath + "/NpcHasDirectCombatSlotCondition.asset";
        private const string TargetDownWaitCompletedConditionPath = ConditionsFolderPath + "/NpcTargetDownWaitCompletedCondition.asset";
        private const string InitialCircleRequestedConditionPath = ConditionsFolderPath + "/NpcInitialCircleRequestedCondition.asset";
        private const string PostAttackDecisionAttackConditionPath = ConditionsFolderPath + "/NpcPostAttackDecisionAttackCondition.asset";
        private const string PostAttackDecisionApproachConditionPath = ConditionsFolderPath + "/NpcPostAttackDecisionApproachCondition.asset";
        private const string PostAttackDecisionEvasionConditionPath = ConditionsFolderPath + "/NpcPostAttackDecisionEvasionCondition.asset";
        private const string PostAttackDecisionManeuverConditionPath = ConditionsFolderPath + "/NpcPostAttackDecisionManeuverCondition.asset";
        private const string PostAttackDecisionCircleConditionPath = ConditionsFolderPath + "/NpcPostAttackDecisionCircleCondition.asset";
        private const string PostAttackDecisionWaitConditionPath = ConditionsFolderPath + "/NpcPostAttackDecisionWaitCondition.asset";
        private const string PostAttackDecisionKeepDistanceConditionPath = ConditionsFolderPath + "/NpcPostAttackDecisionKeepDistanceCondition.asset";
        private const string CombatTargetInAttackViewConditionPath = ConditionsFolderPath + "/NpcCombatTargetInAttackViewCondition.asset";
        private const string KeepDistanceAttackReadyConditionPath = ConditionsFolderPath + "/NpcKeepDistanceAttackReadyCondition.asset";
        private const string CombatTargetOutsideAttackViewConditionPath = ConditionsFolderPath + "/NpcCombatTargetOutsideAttackViewCondition.asset";
        private const string CombatTargetLostConditionPath = ConditionsFolderPath + "/NpcCombatTargetLostCondition.asset";
        private const string ShouldSearchLastKnownTargetConditionPath = ConditionsFolderPath + "/NpcShouldSearchLastKnownTargetCondition.asset";
        private const string LastKnownLookCompletedConditionPath = ConditionsFolderPath + "/NpcLastKnownLookCompletedCondition.asset";
        private const string DialogueRequestedConditionPath = ConditionsFolderPath + "/NpcDialogueRequestedCondition.asset";
        private const string DialogueEndedConditionPath = ConditionsFolderPath + "/NpcDialogueEndedCondition.asset";
        private const string DeathBehaviourPath = BehavioursFolderPath + "/NpcDeathBehaviour.asset";
        private const string ScanVisibleItemsBehaviourPath = BehavioursFolderPath + "/NpcScanVisibleItemsBehaviour.asset";
        private const string ScanEnemiesBehaviourPath = BehavioursFolderPath + "/NpcScanEnemiesBehaviour.asset";
        private const string SheatheWeaponBehaviourPath = BehavioursFolderPath + "/NpcSheatheWeaponBehaviour.asset";
        private const string MoveToItemBehaviourPath = BehavioursFolderPath + "/NpcMoveToInterestedItemBehaviour.asset";
        private const string WaitPickupBehaviourPath = BehavioursFolderPath + "/NpcWaitPickupBehaviour.asset";
        private const string PickupInterestedItemBehaviourPath = BehavioursFolderPath + "/NpcPickupInterestedItemBehaviour.asset";
        private const string ReturnHomeBehaviourPath = BehavioursFolderPath + "/NpcReturnHomeBehaviour.asset";
        private const string DialogueBehaviourPath = BehavioursFolderPath + "/NpcDialogueBehaviour.asset";
        private const string HitReactionBehaviourPath = BehavioursFolderPath + "/NpcHitReactionBehaviour.asset";
        private const string FleeBehaviourPath = BehavioursFolderPath + "/NpcFleeBehaviour.asset";
        private const string InitialCombatTacticBehaviourPath = BehavioursFolderPath + "/NpcInitialCombatTacticBehaviour.asset";
        private const string CombatApproachBehaviourPath = BehavioursFolderPath + "/NpcCombatApproachBehaviour.asset";
        private const string CombatAttackBehaviourPath = BehavioursFolderPath + "/NpcCombatAttackBehaviour.asset";
        private const string PostAttackDecisionBehaviourPath = BehavioursFolderPath + "/NpcPostAttackDecisionBehaviour.asset";
        private const string CombatEvasionBehaviourPath = BehavioursFolderPath + "/NpcCombatEvasionBehaviour.asset";
        private const string CombatManeuverBehaviourPath = BehavioursFolderPath + "/NpcCombatManeuverBehaviour.asset";
        private const string CombatCircleBehaviourPath = BehavioursFolderPath + "/NpcCombatCircleBehaviour.asset";
        private const string CombatWaitBehaviourPath = BehavioursFolderPath + "/NpcCombatWaitBehaviour.asset";
        private const string CombatKeepDistanceBehaviourPath = BehavioursFolderPath + "/NpcCombatKeepDistanceBehaviour.asset";
        private const string CombatQueueCircleBehaviourPath = BehavioursFolderPath + "/NpcCombatQueueCircleBehaviour.asset";
        private const string CombatTargetDownBehaviourPath = BehavioursFolderPath + "/NpcCombatTargetDownBehaviour.asset";
        private const string CombatSearchLastKnownBehaviourPath = BehavioursFolderPath + "/NpcCombatSearchLastKnownBehaviour.asset";
        private const string CombatReturnHomeBehaviourPath = BehavioursFolderPath + "/NpcCombatReturnHomeBehaviour.asset";
        private const string NpcVisionConfigPath = NpcConfigFolderPath + "/NpcVisionConfig.asset";
        private const string NpcItemPickupConfigPath = NpcConfigFolderPath + "/NpcItemPickupConfig.asset";
        private const string NpcCombatConfigPath = NpcConfigFolderPath + "/NpcCombatConfig.asset";
        private const string DefaultNpcCombatProfilePath = NpcConfigFolderPath + "/NpcCombatProfile.asset";
        private const string DefaultNpcDialogPath = "Assets/Dialogs/Test Dialog.asset";
        private const string PlayerPrefabPath = "Assets/Prefabs/Scopes/Player.prefab";
        private const string NpcPrefabPath = "Assets/Prefabs/Scopes/NPC.prefab";
        private const string ProjectLifetimeScopePrefabPath = "Assets/Resources/Project/ProjectLifetimeScope.prefab";

        [MenuItem(MenuPath)]
        public static void CreateBaseNpcAssets()
        {
            ClearInspectorSelectionBeforePrefabContentsEdit();

            EnsureFolder(StateMachineFolderPath);
            EnsureFolder(StatesFolderPath);
            EnsureFolder(TransitionsFolderPath);
            EnsureFolder(ConditionsFolderPath);
            EnsureFolder(BehavioursFolderPath);
            EnsureFolder(NpcConfigFolderPath);

            var idleState = LoadOrCreate<State>(IdleStatePath);
            var deathState = LoadOrCreate<State>(DeathStatePath);
            var moveToItemState = LoadOrCreate<State>(MoveToItemStatePath);
            var pickupWaitState = LoadOrCreate<State>(PickupWaitStatePath);
            var pickupState = LoadOrCreate<State>(PickupStatePath);
            var returnHomeState = LoadOrCreate<State>(ReturnHomeStatePath);
            var dialogueState = LoadOrCreate<State>(DialogueStatePath);
            var hitReactionState = LoadOrCreate<State>(HitReactionStatePath);
            var fleeState = LoadOrCreate<State>(FleeStatePath);
            var combatApproachState = LoadOrCreate<State>(CombatApproachStatePath);
            var combatAttackState = LoadOrCreate<State>(CombatAttackStatePath);
            var postAttackDecisionState = LoadOrCreate<State>(PostAttackDecisionStatePath);
            var combatEvasionState = LoadOrCreate<State>(CombatEvasionStatePath);
            var combatManeuverState = LoadOrCreate<State>(CombatManeuverStatePath);
            var combatCircleState = LoadOrCreate<State>(CombatCircleStatePath);
            var combatWaitState = LoadOrCreate<State>(CombatWaitStatePath);
            var combatKeepDistanceState = LoadOrCreate<State>(CombatKeepDistanceStatePath);
            var combatQueueCircleState = LoadOrCreate<State>(CombatQueueCircleStatePath);
            var combatTargetDownState = LoadOrCreate<State>(CombatTargetDownStatePath);
            var combatSearchLastKnownState = LoadOrCreate<State>(CombatSearchLastKnownStatePath);
            var combatReturnHomeState = LoadOrCreate<State>(CombatReturnHomeStatePath);
            var deathTransition = LoadOrCreate<Transition>(DeathTransitionPath);
            var anyToHitReactionTransition = LoadOrCreate<Transition>(AnyToHitReactionTransitionPath);
            var hitReactionToFleeTransition = LoadOrCreate<Transition>(HitReactionToFleeTransitionPath);
            var hitReactionToCombatApproachTransition = LoadOrCreate<Transition>(HitReactionToCombatApproachTransitionPath);
            var hitReactionToSearchLastKnownTransition = LoadOrCreate<Transition>(HitReactionToSearchLastKnownTransitionPath);
            var hitReactionToIdleTransition = LoadOrCreate<Transition>(HitReactionToIdleTransitionPath);
            var idleToMoveTransition = LoadOrCreate<Transition>(IdleToMoveTransitionPath);
            var idleToFleeTransition = LoadOrCreate<Transition>(IdleToFleeTransitionPath);
            var idleToCombatApproachTransition = LoadOrCreate<Transition>(IdleToCombatApproachTransitionPath);
            var moveToItemToFleeTransition = LoadOrCreate<Transition>(MoveToItemToFleeTransitionPath);
            var moveToItemToCombatApproachTransition = LoadOrCreate<Transition>(MoveToItemToCombatApproachTransitionPath);
            var pickupWaitToFleeTransition = LoadOrCreate<Transition>(PickupWaitToFleeTransitionPath);
            var pickupWaitToCombatApproachTransition = LoadOrCreate<Transition>(PickupWaitToCombatApproachTransitionPath);
            var pickupToFleeTransition = LoadOrCreate<Transition>(PickupToFleeTransitionPath);
            var pickupToCombatApproachTransition = LoadOrCreate<Transition>(PickupToCombatApproachTransitionPath);
            var returnHomeToFleeTransition = LoadOrCreate<Transition>(ReturnHomeToFleeTransitionPath);
            var returnHomeToCombatApproachTransition = LoadOrCreate<Transition>(ReturnHomeToCombatApproachTransitionPath);
            var idleToDialogueTransition = LoadOrCreate<Transition>(IdleToDialogueTransitionPath);
            var moveToItemToDialogueTransition = LoadOrCreate<Transition>(MoveToItemToDialogueTransitionPath);
            var pickupWaitToDialogueTransition = LoadOrCreate<Transition>(PickupWaitToDialogueTransitionPath);
            var pickupToDialogueTransition = LoadOrCreate<Transition>(PickupToDialogueTransitionPath);
            var returnHomeToDialogueTransition = LoadOrCreate<Transition>(ReturnHomeToDialogueTransitionPath);
            var dialogueToFleeTransition = LoadOrCreate<Transition>(DialogueToFleeTransitionPath);
            var dialogueToCombatApproachTransition = LoadOrCreate<Transition>(DialogueToCombatApproachTransitionPath);
            var dialogueToIdleTransition = LoadOrCreate<Transition>(DialogueToIdleTransitionPath);
            var fleeToCombatApproachTransition = LoadOrCreate<Transition>(FleeToCombatApproachTransitionPath);
            var fleeToIdleTransition = LoadOrCreate<Transition>(FleeToIdleTransitionPath);
            var combatApproachToCircleTransition = LoadOrCreate<Transition>(CombatApproachToCircleTransitionPath);
            var combatApproachToQueueTransition = LoadOrCreate<Transition>(CombatApproachToQueueTransitionPath);
            var combatApproachToTargetDownTransition = LoadOrCreate<Transition>(CombatApproachToTargetDownTransitionPath);
            var combatApproachToAttackTransition = LoadOrCreate<Transition>(CombatApproachToAttackTransitionPath);
            var combatApproachToSearchLastKnownTransition = LoadOrCreate<Transition>(CombatApproachToSearchLastKnownTransitionPath);
            var combatAttackToPostAttackDecisionTransition = LoadOrCreate<Transition>(CombatAttackToPostAttackDecisionTransitionPath);
            var combatAttackToTargetDownTransition = LoadOrCreate<Transition>(CombatAttackToTargetDownTransitionPath);
            var combatAttackToSearchLastKnownTransition = LoadOrCreate<Transition>(CombatAttackToSearchLastKnownTransitionPath);
            var postAttackDecisionToTargetDownTransition = LoadOrCreate<Transition>(PostAttackDecisionToTargetDownTransitionPath);
            var postAttackDecisionToQueueTransition = LoadOrCreate<Transition>(PostAttackDecisionToQueueTransitionPath);
            var postAttackDecisionToAttackTransition = LoadOrCreate<Transition>(PostAttackDecisionToAttackTransitionPath);
            var postAttackDecisionToApproachTransition = LoadOrCreate<Transition>(PostAttackDecisionToApproachTransitionPath);
            var postAttackDecisionToEvasionTransition = LoadOrCreate<Transition>(PostAttackDecisionToEvasionTransitionPath);
            var postAttackDecisionToManeuverTransition = LoadOrCreate<Transition>(PostAttackDecisionToManeuverTransitionPath);
            var postAttackDecisionToCircleTransition = LoadOrCreate<Transition>(PostAttackDecisionToCircleTransitionPath);
            var postAttackDecisionToWaitTransition = LoadOrCreate<Transition>(PostAttackDecisionToWaitTransitionPath);
            var postAttackDecisionToKeepDistanceTransition = LoadOrCreate<Transition>(PostAttackDecisionToKeepDistanceTransitionPath);
            var combatManeuverToTargetDownTransition = LoadOrCreate<Transition>(CombatManeuverToTargetDownTransitionPath);
            var combatManeuverToApproachTransition = LoadOrCreate<Transition>(CombatManeuverToApproachTransitionPath);
            var combatEvasionToTargetDownTransition = LoadOrCreate<Transition>(CombatEvasionToTargetDownTransitionPath);
            var combatEvasionToSearchLastKnownTransition = LoadOrCreate<Transition>(CombatEvasionToSearchLastKnownTransitionPath);
            var combatEvasionToPostAttackDecisionTransition = LoadOrCreate<Transition>(CombatEvasionToPostAttackDecisionTransitionPath);
            var combatCircleToTargetDownTransition = LoadOrCreate<Transition>(CombatCircleToTargetDownTransitionPath);
            var combatCircleToApproachTransition = LoadOrCreate<Transition>(CombatCircleToApproachTransitionPath);
            var combatWaitToTargetDownTransition = LoadOrCreate<Transition>(CombatWaitToTargetDownTransitionPath);
            var combatWaitToSearchLastKnownTransition = LoadOrCreate<Transition>(CombatWaitToSearchLastKnownTransitionPath);
            var combatWaitToPostAttackDecisionTransition = LoadOrCreate<Transition>(CombatWaitToPostAttackDecisionTransitionPath);
            var combatKeepDistanceToTargetDownTransition = LoadOrCreate<Transition>(CombatKeepDistanceToTargetDownTransitionPath);
            var combatKeepDistanceToSearchLastKnownTransition = LoadOrCreate<Transition>(CombatKeepDistanceToSearchLastKnownTransitionPath);
            var combatKeepDistanceToEvasionTransition = LoadOrCreate<Transition>(CombatKeepDistanceToEvasionTransitionPath);
            var combatKeepDistanceToAttackTransition = LoadOrCreate<Transition>(CombatKeepDistanceToAttackTransitionPath);
            var combatKeepDistanceToPostAttackDecisionTransition = LoadOrCreate<Transition>(CombatKeepDistanceToPostAttackDecisionTransitionPath);
            var combatQueueToTargetDownTransition = LoadOrCreate<Transition>(CombatQueueToTargetDownTransitionPath);
            var combatQueueToApproachTransition = LoadOrCreate<Transition>(CombatQueueToApproachTransitionPath);
            var combatQueueToSearchLastKnownTransition = LoadOrCreate<Transition>(CombatQueueToSearchLastKnownTransitionPath);
            var combatTargetDownToApproachTransition = LoadOrCreate<Transition>(CombatTargetDownToApproachTransitionPath);
            var combatTargetDownToReturnHomeTransition = LoadOrCreate<Transition>(CombatTargetDownToReturnHomeTransitionPath);
            var combatSearchToTargetDownTransition = LoadOrCreate<Transition>(CombatSearchToTargetDownTransitionPath);
            var combatSearchToApproachTransition = LoadOrCreate<Transition>(CombatSearchToApproachTransitionPath);
            var combatSearchToReturnHomeTransition = LoadOrCreate<Transition>(CombatSearchToReturnHomeTransitionPath);
            var combatReturnHomeToIdleTransition = LoadOrCreate<Transition>(CombatReturnHomeToIdleTransitionPath);
            var moveToWaitTransition = LoadOrCreate<Transition>(MoveToWaitTransitionPath);
            var waitToPickupTransition = LoadOrCreate<Transition>(WaitToPickupTransitionPath);
            var pickupToReturnTransition = LoadOrCreate<Transition>(PickupToReturnTransitionPath);
            var pickupToMoveTransition = LoadOrCreate<Transition>(PickupToMoveTransitionPath);
            var returnToIdleTransition = LoadOrCreate<Transition>(ReturnToIdleTransitionPath);
            var missingItemToReturnTransition = LoadOrCreate<Transition>(MissingItemToReturnTransitionPath);
            var missingItemToNextPickupTransition = LoadOrCreate<Transition>(MissingItemToNextPickupTransitionPath);
            var lostCompetitionToReturnTransition = LoadOrCreate<Transition>(LostCompetitionToReturnTransitionPath);
            var hpDepletedCondition = LoadOrCreate<HpDepletedCondition>(HpDepletedConditionPath);
            var hasInterestingItemCondition = LoadOrCreate<NpcHasInterestingItemCondition>(HasInterestingItemConditionPath);
            var reachedInterestedItemCondition = LoadOrCreate<NpcReachedInterestedItemCondition>(ReachedInterestedItemConditionPath);
            var pickupWaitElapsedCondition = LoadOrCreate<NpcPickupWaitElapsedCondition>(PickupWaitElapsedConditionPath);
            var interestedItemMissingCondition = LoadOrCreate<NpcInterestedItemMissingCondition>(InterestedItemMissingConditionPath);
            var switchToNextPickupCondition = LoadOrCreate<NpcSwitchToNextPickupCondition>(SwitchToNextPickupConditionPath);
            var lostCompetitionCondition = LoadOrCreate<NpcLostItemCompetitionCondition>(LostCompetitionConditionPath);
            var pickupCompletedCondition = LoadOrCreate<NpcPickupCompletedCondition>(PickupCompletedConditionPath);
            var chainPickupFoundCondition = LoadOrCreate<NpcChainPickupFoundCondition>(ChainPickupFoundConditionPath);
            var reachedHomeCondition = LoadOrCreate<NpcReachedHomeCondition>(ReachedHomeConditionPath);
            var hitReactionActiveCondition = LoadOrCreate<CharacterHitReactionActiveCondition>(HitReactionActiveConditionPath);
            var hitReactionInactiveCondition = LoadOrCreate<CharacterHitReactionInactiveCondition>(HitReactionInactiveConditionPath);
            var canFightTargetCondition = LoadOrCreate<NpcCanFightTargetCondition>(CanFightTargetConditionPath);
            var canFightVisibleTargetCondition = LoadOrCreate<NpcCanFightVisibleTargetCondition>(CanFightVisibleTargetConditionPath);
            var visibleThreatWithoutWeaponCondition = LoadOrCreate<NpcVisibleThreatWithoutWeaponCondition>(VisibleThreatWithoutWeaponConditionPath);
            var shouldFleeCondition = LoadOrCreate<NpcShouldFleeCondition>(ShouldFleeConditionPath);
            var fleeCompletedCondition = LoadOrCreate<NpcFleeCompletedCondition>(FleeCompletedConditionPath);
            var attackCompletedCondition = LoadOrCreate<NpcAttackCompletedCondition>(AttackCompletedConditionPath);
            var combatMoveCompletedCondition = LoadOrCreate<NpcCombatMoveCompletedCondition>(CombatMoveCompletedConditionPath);
            var combatTargetDownCondition = LoadOrCreate<NpcCombatTargetDownCondition>(CombatTargetDownConditionPath);
            var shouldQueueForCombatSlotCondition = LoadOrCreate<NpcShouldQueueForCombatSlotCondition>(ShouldQueueForCombatSlotConditionPath);
            var hasDirectCombatSlotCondition = LoadOrCreate<NpcHasDirectCombatSlotCondition>(HasDirectCombatSlotConditionPath);
            var targetDownWaitCompletedCondition = LoadOrCreate<NpcTargetDownWaitCompletedCondition>(TargetDownWaitCompletedConditionPath);
            var initialCircleRequestedCondition = LoadOrCreate<NpcInitialCircleRequestedCondition>(InitialCircleRequestedConditionPath);
            var postAttackDecisionAttackCondition = LoadOrCreate<NpcPostAttackDecisionCondition>(PostAttackDecisionAttackConditionPath);
            var postAttackDecisionApproachCondition = LoadOrCreate<NpcPostAttackDecisionCondition>(PostAttackDecisionApproachConditionPath);
            var postAttackDecisionEvasionCondition = LoadOrCreate<NpcPostAttackDecisionCondition>(PostAttackDecisionEvasionConditionPath);
            var postAttackDecisionManeuverCondition = LoadOrCreate<NpcPostAttackDecisionCondition>(PostAttackDecisionManeuverConditionPath);
            var postAttackDecisionCircleCondition = LoadOrCreate<NpcPostAttackDecisionCondition>(PostAttackDecisionCircleConditionPath);
            var postAttackDecisionWaitCondition = LoadOrCreate<NpcPostAttackDecisionCondition>(PostAttackDecisionWaitConditionPath);
            var postAttackDecisionKeepDistanceCondition = LoadOrCreate<NpcPostAttackDecisionCondition>(PostAttackDecisionKeepDistanceConditionPath);
            var combatTargetInAttackViewCondition = LoadOrCreate<NpcCombatTargetInAttackViewCondition>(CombatTargetInAttackViewConditionPath);
            var keepDistanceAttackReadyCondition = LoadOrCreate<NpcKeepDistanceAttackReadyCondition>(KeepDistanceAttackReadyConditionPath);
            var combatTargetOutsideAttackViewCondition = LoadOrCreate<NpcCombatTargetOutsideAttackViewCondition>(CombatTargetOutsideAttackViewConditionPath);
            var combatTargetLostCondition = LoadOrCreate<NpcCombatTargetLostCondition>(CombatTargetLostConditionPath);
            var shouldSearchLastKnownTargetCondition = LoadOrCreate<NpcShouldSearchLastKnownTargetCondition>(ShouldSearchLastKnownTargetConditionPath);
            var lastKnownLookCompletedCondition = LoadOrCreate<NpcLastKnownLookCompletedCondition>(LastKnownLookCompletedConditionPath);
            var dialogueRequestedCondition = LoadOrCreate<NpcDialogueRequestedCondition>(DialogueRequestedConditionPath);
            var dialogueEndedCondition = LoadOrCreate<NpcDialogueEndedCondition>(DialogueEndedConditionPath);
            var deathBehaviour = LoadOrCreate<NpcDeathBehaviour>(DeathBehaviourPath);
            var scanVisibleItemsBehaviour = LoadOrCreate<NpcScanVisibleItemsBehaviour>(ScanVisibleItemsBehaviourPath);
            var scanEnemiesBehaviour = LoadOrCreate<NpcScanEnemiesBehaviour>(ScanEnemiesBehaviourPath);
            var sheatheWeaponBehaviour = LoadOrCreate<NpcSheatheWeaponBehaviour>(SheatheWeaponBehaviourPath);
            var moveToItemBehaviour = LoadOrCreate<NpcMoveToInterestedItemBehaviour>(MoveToItemBehaviourPath);
            var waitPickupBehaviour = LoadOrCreate<NpcWaitPickupBehaviour>(WaitPickupBehaviourPath);
            var pickupInterestedItemBehaviour = LoadOrCreate<NpcPickupInterestedItemBehaviour>(PickupInterestedItemBehaviourPath);
            var returnHomeBehaviour = LoadOrCreate<NpcReturnHomeBehaviour>(ReturnHomeBehaviourPath);
            var dialogueBehaviour = LoadOrCreate<NpcDialogueBehaviour>(DialogueBehaviourPath);
            var hitReactionBehaviour = LoadOrCreate<NpcHitReactionBehaviour>(HitReactionBehaviourPath);
            var fleeBehaviour = LoadOrCreate<NpcFleeBehaviour>(FleeBehaviourPath);
            var initialCombatTacticBehaviour = LoadOrCreate<NpcInitialCombatTacticBehaviour>(InitialCombatTacticBehaviourPath);
            var combatApproachBehaviour = LoadOrCreate<NpcCombatApproachBehaviour>(CombatApproachBehaviourPath);
            var combatAttackBehaviour = LoadOrCreate<NpcCombatAttackBehaviour>(CombatAttackBehaviourPath);
            var postAttackDecisionBehaviour = LoadOrCreate<NpcPostAttackDecisionBehaviour>(PostAttackDecisionBehaviourPath);
            var combatEvasionBehaviour = LoadOrCreate<NpcCombatEvasionBehaviour>(CombatEvasionBehaviourPath);
            var combatManeuverBehaviour = LoadOrCreate<NpcCombatManeuverBehaviour>(CombatManeuverBehaviourPath);
            var combatCircleBehaviour = LoadOrCreate<NpcCombatCircleBehaviour>(CombatCircleBehaviourPath);
            var combatWaitBehaviour = LoadOrCreate<NpcCombatWaitBehaviour>(CombatWaitBehaviourPath);
            var combatKeepDistanceBehaviour = LoadOrCreate<NpcCombatKeepDistanceBehaviour>(CombatKeepDistanceBehaviourPath);
            var combatQueueCircleBehaviour = LoadOrCreate<NpcCombatQueueCircleBehaviour>(CombatQueueCircleBehaviourPath);
            var combatTargetDownBehaviour = LoadOrCreate<NpcCombatTargetDownBehaviour>(CombatTargetDownBehaviourPath);
            var combatSearchLastKnownBehaviour = LoadOrCreate<NpcCombatSearchLastKnownBehaviour>(CombatSearchLastKnownBehaviourPath);
            var combatReturnHomeBehaviour = LoadOrCreate<NpcCombatReturnHomeBehaviour>(CombatReturnHomeBehaviourPath);
            var visionConfig = LoadOrCreate<NpcVisionConfig>(NpcVisionConfigPath);
            var itemPickupConfig = LoadOrCreate<NpcItemPickupConfig>(NpcItemPickupConfigPath);
            var combatConfig = LoadOrCreate<NpcCombatConfig>(NpcCombatConfigPath);
            _ = LoadOrCreate<NpcCombatProfile>(DefaultNpcCombatProfilePath);
            EditorUtility.SetDirty(combatConfig);
            var graph = LoadOrCreate<StateMachineGraph>(GraphPath);

            ConfigureTransition(deathTransition, deathState, hpDepletedCondition);
            ConfigureDecisionCondition(postAttackDecisionAttackCondition, NpcCombatDecision.Attack);
            ConfigureDecisionCondition(postAttackDecisionApproachCondition, NpcCombatDecision.Approach);
            ConfigureDecisionCondition(postAttackDecisionEvasionCondition, NpcCombatDecision.Evade);
            ConfigureDecisionCondition(postAttackDecisionManeuverCondition, NpcCombatDecision.Maneuver);
            ConfigureDecisionCondition(postAttackDecisionCircleCondition, NpcCombatDecision.Circle);
            ConfigureDecisionCondition(postAttackDecisionWaitCondition, NpcCombatDecision.Wait);
            ConfigureDecisionCondition(postAttackDecisionKeepDistanceCondition, NpcCombatDecision.KeepDistance);

            ConfigureTransition(anyToHitReactionTransition, hitReactionState, hitReactionActiveCondition);
            ConfigureTransition(hitReactionToFleeTransition, fleeState, hitReactionInactiveCondition, shouldFleeCondition);
            ConfigureTransition(hitReactionToCombatApproachTransition, combatApproachState, hitReactionInactiveCondition, canFightTargetCondition);
            ConfigureTransition(hitReactionToSearchLastKnownTransition, combatSearchLastKnownState, hitReactionInactiveCondition, combatTargetLostCondition);
            ConfigureTransition(hitReactionToIdleTransition, idleState, hitReactionInactiveCondition);
            ConfigureTransition(idleToMoveTransition, moveToItemState, hasInterestingItemCondition);
            ConfigureTransition(idleToFleeTransition, fleeState, shouldFleeCondition);
            ConfigureTransition(idleToCombatApproachTransition, combatApproachState, canFightTargetCondition);
            ConfigureTransition(moveToItemToFleeTransition, fleeState, shouldFleeCondition);
            ConfigureTransition(moveToItemToCombatApproachTransition, combatApproachState, canFightTargetCondition);
            ConfigureTransition(pickupWaitToFleeTransition, fleeState, shouldFleeCondition);
            ConfigureTransition(pickupWaitToCombatApproachTransition, combatApproachState, canFightTargetCondition);
            ConfigureTransition(pickupToFleeTransition, fleeState, shouldFleeCondition);
            ConfigureTransition(pickupToCombatApproachTransition, combatApproachState, canFightTargetCondition);
            ConfigureTransition(returnHomeToFleeTransition, fleeState, shouldFleeCondition);
            ConfigureTransition(returnHomeToCombatApproachTransition, combatApproachState, canFightTargetCondition);
            ConfigureTransition(idleToDialogueTransition, dialogueState, dialogueRequestedCondition);
            ConfigureTransition(moveToItemToDialogueTransition, dialogueState, dialogueRequestedCondition);
            ConfigureTransition(pickupWaitToDialogueTransition, dialogueState, dialogueRequestedCondition);
            ConfigureTransition(pickupToDialogueTransition, dialogueState, dialogueRequestedCondition);
            ConfigureTransition(returnHomeToDialogueTransition, dialogueState, dialogueRequestedCondition);
            ConfigureTransition(dialogueToFleeTransition, fleeState, visibleThreatWithoutWeaponCondition);
            ConfigureTransition(dialogueToCombatApproachTransition, combatApproachState, canFightVisibleTargetCondition);
            ConfigureTransition(dialogueToIdleTransition, idleState, dialogueEndedCondition);
            ConfigureTransition(fleeToCombatApproachTransition, combatApproachState, canFightVisibleTargetCondition);
            ConfigureTransition(fleeToIdleTransition, returnHomeState, fleeCompletedCondition);
            ConfigureTransition(combatApproachToCircleTransition, combatCircleState, initialCircleRequestedCondition);
            ConfigureTransition(combatApproachToQueueTransition, combatQueueCircleState, shouldQueueForCombatSlotCondition);
            ConfigureTransition(combatApproachToTargetDownTransition, combatTargetDownState, combatTargetDownCondition);
            ConfigureTransition(combatApproachToAttackTransition, combatAttackState, combatTargetInAttackViewCondition);
            ConfigureTransition(combatApproachToSearchLastKnownTransition, combatSearchLastKnownState, combatTargetLostCondition);
            ConfigureTransition(combatAttackToPostAttackDecisionTransition, postAttackDecisionState, attackCompletedCondition);
            ConfigureTransition(combatAttackToTargetDownTransition, combatTargetDownState, combatTargetDownCondition);
            ConfigureTransition(combatAttackToSearchLastKnownTransition, combatSearchLastKnownState, combatTargetLostCondition);
            ConfigureTransition(postAttackDecisionToTargetDownTransition, combatTargetDownState, combatTargetDownCondition);
            ConfigureTransition(postAttackDecisionToQueueTransition, combatQueueCircleState, shouldQueueForCombatSlotCondition);
            ConfigureTransition(postAttackDecisionToAttackTransition, combatAttackState, postAttackDecisionAttackCondition);
            ConfigureTransition(postAttackDecisionToApproachTransition, combatApproachState, postAttackDecisionApproachCondition);
            ConfigureTransition(postAttackDecisionToEvasionTransition, combatEvasionState, postAttackDecisionEvasionCondition);
            ConfigureTransition(postAttackDecisionToManeuverTransition, combatManeuverState, postAttackDecisionManeuverCondition);
            ConfigureTransition(postAttackDecisionToCircleTransition, combatCircleState, postAttackDecisionCircleCondition);
            ConfigureTransition(postAttackDecisionToWaitTransition, combatWaitState, postAttackDecisionWaitCondition);
            ConfigureTransition(postAttackDecisionToKeepDistanceTransition, combatKeepDistanceState, postAttackDecisionKeepDistanceCondition);
            ConfigureTransition(combatManeuverToTargetDownTransition, combatTargetDownState, combatTargetDownCondition);
            ConfigureTransition(combatManeuverToApproachTransition, combatApproachState, combatMoveCompletedCondition);
            ConfigureTransition(combatEvasionToTargetDownTransition, combatTargetDownState, combatTargetDownCondition);
            ConfigureTransition(combatEvasionToSearchLastKnownTransition, combatSearchLastKnownState, combatTargetLostCondition);
            ConfigureTransition(combatEvasionToPostAttackDecisionTransition, postAttackDecisionState, combatMoveCompletedCondition);
            ConfigureTransition(combatCircleToTargetDownTransition, combatTargetDownState, combatTargetDownCondition);
            ConfigureTransition(combatCircleToApproachTransition, combatApproachState, combatMoveCompletedCondition);
            ConfigureTransition(combatWaitToTargetDownTransition, combatTargetDownState, combatTargetDownCondition);
            ConfigureTransition(combatWaitToSearchLastKnownTransition, combatSearchLastKnownState, combatTargetLostCondition);
            ConfigureTransition(combatWaitToPostAttackDecisionTransition, postAttackDecisionState, combatMoveCompletedCondition);
            ConfigureTransition(combatKeepDistanceToTargetDownTransition, combatTargetDownState, combatTargetDownCondition);
            ConfigureTransition(combatKeepDistanceToSearchLastKnownTransition, combatSearchLastKnownState, combatTargetLostCondition);
            ConfigureTransition(combatKeepDistanceToEvasionTransition, combatEvasionState, postAttackDecisionEvasionCondition);
            ConfigureTransition(combatKeepDistanceToAttackTransition, combatAttackState, keepDistanceAttackReadyCondition);
            ConfigureTransition(combatKeepDistanceToPostAttackDecisionTransition, postAttackDecisionState, combatMoveCompletedCondition);
            ConfigureTransition(combatQueueToTargetDownTransition, combatTargetDownState, combatTargetDownCondition);
            ConfigureTransition(combatQueueToApproachTransition, combatApproachState, hasDirectCombatSlotCondition);
            ConfigureTransition(combatQueueToSearchLastKnownTransition, combatSearchLastKnownState, combatTargetLostCondition);
            ConfigureTransition(combatTargetDownToApproachTransition, combatApproachState, canFightTargetCondition);
            ConfigureTransition(combatTargetDownToReturnHomeTransition, combatReturnHomeState, targetDownWaitCompletedCondition);
            ConfigureTransition(combatSearchToTargetDownTransition, combatTargetDownState, combatTargetDownCondition);
            ConfigureTransition(combatSearchToApproachTransition, combatApproachState, canFightVisibleTargetCondition);
            ConfigureTransition(combatSearchToReturnHomeTransition, combatReturnHomeState, lastKnownLookCompletedCondition);
            ConfigureTransition(combatReturnHomeToIdleTransition, idleState, combatMoveCompletedCondition);
            ConfigureTransition(moveToWaitTransition, pickupWaitState, reachedInterestedItemCondition);
            ConfigureTransition(waitToPickupTransition, pickupState, pickupWaitElapsedCondition);
            ConfigureTransition(pickupToMoveTransition, moveToItemState, chainPickupFoundCondition);
            ConfigureTransition(pickupToReturnTransition, returnHomeState, pickupCompletedCondition);
            ConfigureTransition(returnToIdleTransition, idleState, reachedHomeCondition);
            ConfigureTransition(missingItemToNextPickupTransition, moveToItemState, switchToNextPickupCondition);
            ConfigureTransition(missingItemToReturnTransition, returnHomeState, interestedItemMissingCondition);
            ConfigureTransition(lostCompetitionToReturnTransition, returnHomeState, lostCompetitionCondition);

            deathState.Behaviours = new List<BaseBehaviour> { deathBehaviour };
            deathState.Transitions = new List<Transition>();
            EditorUtility.SetDirty(deathState);

            hitReactionState.Behaviours = new List<BaseBehaviour> { hitReactionBehaviour };
            hitReactionState.Transitions = new List<Transition> { deathTransition, hitReactionToFleeTransition, hitReactionToSearchLastKnownTransition, hitReactionToCombatApproachTransition, hitReactionToIdleTransition };
            EditorUtility.SetDirty(hitReactionState);

            idleState.Behaviours = new List<BaseBehaviour> { sheatheWeaponBehaviour, scanEnemiesBehaviour, scanVisibleItemsBehaviour };
            idleState.Transitions = new List<Transition> { deathTransition, anyToHitReactionTransition, idleToFleeTransition, idleToCombatApproachTransition, idleToDialogueTransition, idleToMoveTransition };
            EditorUtility.SetDirty(idleState);

            moveToItemState.Behaviours = new List<BaseBehaviour> { scanEnemiesBehaviour, moveToItemBehaviour };
            moveToItemState.Transitions = new List<Transition>
            {
                deathTransition,
                anyToHitReactionTransition,
                moveToItemToFleeTransition,
                moveToItemToCombatApproachTransition,
                moveToItemToDialogueTransition,
                missingItemToNextPickupTransition,
                missingItemToReturnTransition,
                lostCompetitionToReturnTransition,
                moveToWaitTransition
            };
            EditorUtility.SetDirty(moveToItemState);

            pickupWaitState.Behaviours = new List<BaseBehaviour> { scanEnemiesBehaviour, waitPickupBehaviour };
            pickupWaitState.Transitions = new List<Transition>
            {
                deathTransition,
                anyToHitReactionTransition,
                pickupWaitToFleeTransition,
                pickupWaitToCombatApproachTransition,
                pickupWaitToDialogueTransition,
                missingItemToNextPickupTransition,
                missingItemToReturnTransition,
                lostCompetitionToReturnTransition,
                waitToPickupTransition
            };
            EditorUtility.SetDirty(pickupWaitState);

            pickupState.Behaviours = new List<BaseBehaviour> { scanEnemiesBehaviour, pickupInterestedItemBehaviour };
            pickupState.Transitions = new List<Transition> { deathTransition, anyToHitReactionTransition, pickupToFleeTransition, pickupToCombatApproachTransition, pickupToDialogueTransition, pickupToMoveTransition, pickupToReturnTransition };
            EditorUtility.SetDirty(pickupState);

            returnHomeState.Behaviours = new List<BaseBehaviour> { scanEnemiesBehaviour, returnHomeBehaviour };
            returnHomeState.Transitions = new List<Transition> { deathTransition, anyToHitReactionTransition, returnHomeToFleeTransition, returnHomeToCombatApproachTransition, returnHomeToDialogueTransition, returnToIdleTransition };
            EditorUtility.SetDirty(returnHomeState);

            dialogueState.Behaviours = new List<BaseBehaviour> { scanEnemiesBehaviour, dialogueBehaviour };
            dialogueState.Transitions = new List<Transition>
            {
                deathTransition,
                anyToHitReactionTransition,
                dialogueToFleeTransition,
                dialogueToCombatApproachTransition,
                dialogueToIdleTransition
            };
            EditorUtility.SetDirty(dialogueState);

            fleeState.Behaviours = new List<BaseBehaviour> { scanEnemiesBehaviour, fleeBehaviour };
            fleeState.Transitions = new List<Transition> { deathTransition, anyToHitReactionTransition, fleeToCombatApproachTransition, fleeToIdleTransition };
            EditorUtility.SetDirty(fleeState);

            combatApproachState.Behaviours = new List<BaseBehaviour> { initialCombatTacticBehaviour, combatApproachBehaviour };
            combatApproachState.Transitions = new List<Transition>
            {
                deathTransition,
                anyToHitReactionTransition,
                combatApproachToTargetDownTransition,
                combatApproachToSearchLastKnownTransition,
                combatApproachToQueueTransition,
                combatApproachToCircleTransition,
                combatApproachToAttackTransition
            };
            EditorUtility.SetDirty(combatApproachState);

            combatAttackState.Behaviours = new List<BaseBehaviour> { combatAttackBehaviour };
            combatAttackState.Transitions = new List<Transition>
            {
                deathTransition,
                anyToHitReactionTransition,
                combatAttackToTargetDownTransition,
                combatAttackToSearchLastKnownTransition,
                combatAttackToPostAttackDecisionTransition
            };
            EditorUtility.SetDirty(combatAttackState);

            postAttackDecisionState.Behaviours = new List<BaseBehaviour> { postAttackDecisionBehaviour };
            postAttackDecisionState.Transitions = new List<Transition>
            {
                deathTransition,
                anyToHitReactionTransition,
                postAttackDecisionToTargetDownTransition,
                combatAttackToSearchLastKnownTransition,
                postAttackDecisionToQueueTransition,
                postAttackDecisionToEvasionTransition,
                postAttackDecisionToManeuverTransition,
                postAttackDecisionToCircleTransition,
                postAttackDecisionToWaitTransition,
                postAttackDecisionToKeepDistanceTransition,
                postAttackDecisionToApproachTransition,
                postAttackDecisionToAttackTransition
            };
            EditorUtility.SetDirty(postAttackDecisionState);

            combatEvasionState.Behaviours = new List<BaseBehaviour> { combatEvasionBehaviour };
            combatEvasionState.Transitions = new List<Transition>
            {
                deathTransition,
                anyToHitReactionTransition,
                combatEvasionToTargetDownTransition,
                combatEvasionToSearchLastKnownTransition,
                combatEvasionToPostAttackDecisionTransition
            };
            EditorUtility.SetDirty(combatEvasionState);

            combatManeuverState.Behaviours = new List<BaseBehaviour> { combatManeuverBehaviour };
            combatManeuverState.Transitions = new List<Transition> { deathTransition, anyToHitReactionTransition, combatManeuverToTargetDownTransition, combatAttackToSearchLastKnownTransition, combatManeuverToApproachTransition };
            EditorUtility.SetDirty(combatManeuverState);

            combatCircleState.Behaviours = new List<BaseBehaviour> { combatCircleBehaviour };
            combatCircleState.Transitions = new List<Transition> { deathTransition, anyToHitReactionTransition, combatCircleToTargetDownTransition, combatAttackToSearchLastKnownTransition, combatCircleToApproachTransition };
            EditorUtility.SetDirty(combatCircleState);

            combatWaitState.Behaviours = new List<BaseBehaviour> { combatWaitBehaviour };
            combatWaitState.Transitions = new List<Transition>
            {
                deathTransition,
                anyToHitReactionTransition,
                combatWaitToTargetDownTransition,
                combatWaitToSearchLastKnownTransition,
                combatWaitToPostAttackDecisionTransition
            };
            EditorUtility.SetDirty(combatWaitState);

            combatKeepDistanceState.Behaviours = new List<BaseBehaviour> { combatKeepDistanceBehaviour };
            combatKeepDistanceState.Transitions = new List<Transition>
            {
                deathTransition,
                anyToHitReactionTransition,
                combatKeepDistanceToTargetDownTransition,
                combatKeepDistanceToSearchLastKnownTransition,
                combatKeepDistanceToEvasionTransition,
                combatKeepDistanceToAttackTransition,
                combatKeepDistanceToPostAttackDecisionTransition
            };
            EditorUtility.SetDirty(combatKeepDistanceState);

            combatQueueCircleState.Behaviours = new List<BaseBehaviour> { combatQueueCircleBehaviour };
            combatQueueCircleState.Transitions = new List<Transition> { deathTransition, anyToHitReactionTransition, combatQueueToTargetDownTransition, combatQueueToSearchLastKnownTransition, combatQueueToApproachTransition };
            EditorUtility.SetDirty(combatQueueCircleState);

            combatTargetDownState.Behaviours = new List<BaseBehaviour> { combatTargetDownBehaviour };
            combatTargetDownState.Transitions = new List<Transition> { deathTransition, anyToHitReactionTransition, combatTargetDownToApproachTransition, combatTargetDownToReturnHomeTransition };
            EditorUtility.SetDirty(combatTargetDownState);

            combatSearchLastKnownState.Behaviours = new List<BaseBehaviour> { scanEnemiesBehaviour, combatSearchLastKnownBehaviour };
            combatSearchLastKnownState.Transitions = new List<Transition> { deathTransition, anyToHitReactionTransition, combatSearchToTargetDownTransition, combatSearchToApproachTransition, combatSearchToReturnHomeTransition };
            EditorUtility.SetDirty(combatSearchLastKnownState);

            combatReturnHomeState.Behaviours = new List<BaseBehaviour> { scanEnemiesBehaviour, combatReturnHomeBehaviour };
            combatReturnHomeState.Transitions = new List<Transition>
            {
                deathTransition,
                anyToHitReactionTransition,
                returnHomeToFleeTransition,
                returnHomeToCombatApproachTransition,
                returnHomeToDialogueTransition,
                combatReturnHomeToIdleTransition
            };
            EditorUtility.SetDirty(combatReturnHomeState);

            graph.Nodes = new List<Node>
            {
                new(idleState) { Position = new Vector2(160f, 180f) },
                new(moveToItemState) { Position = new Vector2(520f, 80f) },
                new(pickupWaitState) { Position = new Vector2(860f, 80f) },
                new(pickupState) { Position = new Vector2(1200f, 80f) },
                new(returnHomeState) { Position = new Vector2(860f, 330f) },
                new(dialogueState) { Position = new Vector2(520f, 330f) },
                new(fleeState) { Position = new Vector2(160f, -180f) },
                new(combatApproachState) { Position = new Vector2(520f, -180f) },
                new(combatAttackState) { Position = new Vector2(860f, -180f) },
                new(postAttackDecisionState) { Position = new Vector2(1200f, -180f) },
                new(combatEvasionState) { Position = new Vector2(1540f, -180f) },
                new(combatManeuverState) { Position = new Vector2(1200f, -420f) },
                new(combatCircleState) { Position = new Vector2(520f, -420f) },
                new(combatWaitState) { Position = new Vector2(860f, -420f) },
                new(combatKeepDistanceState) { Position = new Vector2(1540f, -420f) },
                new(combatQueueCircleState) { Position = new Vector2(160f, -420f) },
                new(combatTargetDownState) { Position = new Vector2(1200f, -650f) },
                new(combatSearchLastKnownState) { Position = new Vector2(860f, -650f) },
                new(combatReturnHomeState) { Position = new Vector2(520f, -650f) },
                new(hitReactionState) { Position = new Vector2(160f, 520f) },
                new(deathState) { Position = new Vector2(520f, 520f) }
            };
            EditorUtility.SetDirty(graph);

            CreateOrUpdateNpcPrefab(graph, visionConfig);
            AssignProjectNpcConfigs(visionConfig, itemPickupConfig, combatConfig);

            AssetDatabase.SaveAssets();
            AssetDatabase.ForceReserializeAssets(new[] { NpcCombatConfigPath });
            AssetDatabase.Refresh();
            SelectAssetAfterPrefabContentsEdit(NpcPrefabPath);
            Debug.Log($"Created base NPC assets: {GraphPath}, {NpcPrefabPath}");
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void ConfigureTransition(Transition transition, State targetState, params BaseCondition[] conditions)
        {
            transition.Type = TransitionType.All;
            transition.Conditions.Clear();
            if (conditions != null)
            {
                foreach (var condition in conditions)
                {
                    if (condition != null)
                    {
                        transition.Conditions.Add(condition);
                    }
                }
            }

            transition.ActionOnTransitions.Clear();
            transition.TargetState = targetState;
            EditorUtility.SetDirty(transition);
        }

        private static void ConfigureDecisionCondition(NpcPostAttackDecisionCondition condition, NpcCombatDecision decision)
        {
            if (condition == null)
            {
                return;
            }

            condition.ExpectedDecision = decision;
            EditorUtility.SetDirty(condition);
        }

        private static void CreateOrUpdateNpcPrefab(StateMachineGraph graph, NpcVisionConfig visionConfig)
        {
            ClearInspectorSelectionBeforePrefabContentsEdit();

            var sourcePath = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath) != null
                ? NpcPrefabPath
                : PlayerPrefabPath;
            var root = PrefabUtility.LoadPrefabContents(sourcePath);
            if (root == null)
            {
                Debug.LogError($"Cannot load prefab source: {sourcePath}");
                return;
            }

            try
            {
                root.name = "NPC";

                var characterInfo = default(Object);
                var inventoryConfig = default(Object);
                var playerScope = root.GetComponent<PlayerLifetimeScope>();
                if (playerScope != null)
                {
                    var playerScopeObject = new SerializedObject(playerScope);
                    characterInfo = playerScopeObject.FindProperty("characterInfo")?.objectReferenceValue;
                    inventoryConfig = playerScopeObject.FindProperty("inventoryConfig")?.objectReferenceValue;
                    Object.DestroyImmediate(playerScope, true);
                }

                var npcScope = root.GetComponent<NpcLifetimeScope>();
                if (npcScope == null)
                {
                    npcScope = root.AddComponent<NpcLifetimeScope>();
                }

                var npcScopeObject = new SerializedObject(npcScope);
                var characterInfoProperty = npcScopeObject.FindProperty("characterInfo");
                if (characterInfoProperty != null && characterInfo != null)
                {
                    characterInfoProperty.objectReferenceValue = characterInfo;
                }

                var inventoryConfigProperty = npcScopeObject.FindProperty("inventoryConfig");
                if (inventoryConfigProperty != null && inventoryConfig != null)
                {
                    inventoryConfigProperty.objectReferenceValue = inventoryConfig;
                }

                var dialogProperty = npcScopeObject.FindProperty("dialog");
                if (dialogProperty != null && dialogProperty.objectReferenceValue == null)
                {
                    dialogProperty.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Object>(DefaultNpcDialogPath);
                }

                var stateMachineProperty = npcScopeObject.FindProperty("stateMachineGraph");
                if (stateMachineProperty != null)
                {
                    stateMachineProperty.objectReferenceValue = graph;
                }

                var parentReferenceTypeNameProperty = npcScopeObject.FindProperty("parentReference.TypeName");
                if (parentReferenceTypeNameProperty != null)
                {
                    parentReferenceTypeNameProperty.stringValue = "Container.Game.GameLifetimeScope";
                }

                npcScopeObject.ApplyModifiedPropertiesWithoutUndo();

                if (root.GetComponent<PlayerRagdollController>() == null)
                {
                    root.AddComponent<PlayerRagdollController>();
                }

                if (root.GetComponent<TargetLockTarget>() == null)
                {
                    root.AddComponent<TargetLockTarget>();
                }

                var npcVision = root.GetComponent<NpcVision>();
                if (npcVision == null)
                {
                    npcVision = root.AddComponent<NpcVision>();
                }

                var npcVisionObject = new SerializedObject(npcVision);
                var configProperty = npcVisionObject.FindProperty("config");
                if (configProperty != null)
                {
                    configProperty.objectReferenceValue = visionConfig;
                }

                npcVisionObject.ApplyModifiedPropertiesWithoutUndo();

                var npcVisionSensor = root.GetComponent<NpcVisionSensor>();
                if (npcVisionSensor == null)
                {
                    npcVisionSensor = root.AddComponent<NpcVisionSensor>();
                }

                var npcVisionSensorObject = new SerializedObject(npcVisionSensor);
                var sensorConfigProperty = npcVisionSensorObject.FindProperty("config");
                if (sensorConfigProperty != null)
                {
                    sensorConfigProperty.objectReferenceValue = visionConfig;
                }

                npcVisionSensorObject.ApplyModifiedPropertiesWithoutUndo();

                if (root.GetComponent<NpcItemInterest>() == null)
                {
                    root.AddComponent<NpcItemInterest>();
                }

                var interactable = root.GetComponent<Interactable.Interactable>();
                if (interactable == null)
                {
                    interactable = root.AddComponent<Interactable.Interactable>();
                }

                interactable.InteractionMode = Interactable.InteractionMode.Manual;

                if (root.GetComponent<NpcDialogueAvailability>() == null)
                {
                    root.AddComponent<NpcDialogueAvailability>();
                }

                var navMeshAgent = root.GetComponent<NavMeshAgent>();
                if (navMeshAgent == null)
                {
                    navMeshAgent = root.AddComponent<NavMeshAgent>();
                }

                ConfigureNpcNavMeshAgent(navMeshAgent);
                ConfigureNpcDialogueZone(root);

                MoveComponentAfterTransform(npcScope);

                PrefabUtility.SaveAsPrefabAsset(root, NpcPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AssignProjectNpcConfigs(NpcVisionConfig visionConfig, NpcItemPickupConfig itemPickupConfig, NpcCombatConfig combatConfig)
        {
            ClearInspectorSelectionBeforePrefabContentsEdit();

            var root = PrefabUtility.LoadPrefabContents(ProjectLifetimeScopePrefabPath);
            if (root == null)
            {
                Debug.LogError($"Cannot load prefab source: {ProjectLifetimeScopePrefabPath}");
                return;
            }

            try
            {
                var projectScope = root.GetComponent<Container.Project.ProjectLifetimeScope>();
                if (projectScope == null)
                {
                    Debug.LogError($"{ProjectLifetimeScopePrefabPath} has no ProjectLifetimeScope component.");
                    return;
                }

                var projectScopeObject = new SerializedObject(projectScope);
                var visionConfigProperty = projectScopeObject.FindProperty("<NpcVisionConfig>k__BackingField");
                if (visionConfigProperty != null)
                {
                    visionConfigProperty.objectReferenceValue = visionConfig;
                }

                var itemPickupConfigProperty = projectScopeObject.FindProperty("<NpcItemPickupConfig>k__BackingField");
                if (itemPickupConfigProperty != null)
                {
                    itemPickupConfigProperty.objectReferenceValue = itemPickupConfig;
                }

                var combatConfigProperty = projectScopeObject.FindProperty("<NpcCombatConfig>k__BackingField");
                if (combatConfigProperty != null)
                {
                    combatConfigProperty.objectReferenceValue = combatConfig;
                }

                projectScopeObject.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, ProjectLifetimeScopePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem(SetupNavMeshMenuPath)]
        public static void SetupNavMeshForActiveScene()
        {
            var navigationRoot = GameObject.Find("Navigation");
            if (navigationRoot == null)
            {
                navigationRoot = new GameObject("Navigation");
            }

            var surface = navigationRoot.GetComponent<NavMeshSurface>();
            if (surface == null)
            {
                surface = navigationRoot.AddComponent<NavMeshSurface>();
            }

            surface.collectObjects = CollectObjects.All;
            surface.layerMask = ~0;
            surface.ignoreNavMeshAgent = true;
            surface.ignoreNavMeshObstacle = true;
            surface.BuildNavMesh();

            EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(navigationRoot.scene);
            EditorSceneManager.SaveScene(navigationRoot.scene);
            Debug.Log($"Built NavMesh for active scene: {navigationRoot.scene.path}");
        }

        private static void ConfigureNpcNavMeshAgent(NavMeshAgent agent)
        {
            agent.radius = 0.25f;
            agent.height = 1.8f;
            agent.baseOffset = 0f;
            agent.speed = 3.5f;
            agent.angularSpeed = 720f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0.15f;
            agent.autoBraking = true;
        }

        private static void ConfigureNpcDialogueZone(GameObject root)
        {
            const string dialogueZoneName = "Dialogue Interactable Zone";
            var zoneTransform = root.transform.Find(dialogueZoneName);
            if (zoneTransform == null)
            {
                var zoneObject = new GameObject(dialogueZoneName);
                zoneTransform = zoneObject.transform;
                zoneTransform.SetParent(root.transform, false);
            }

            zoneTransform.localPosition = new Vector3(0f, 1f, 0f);
            zoneTransform.localRotation = Quaternion.identity;
            zoneTransform.localScale = Vector3.one;

            var interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer >= 0)
            {
                zoneTransform.gameObject.layer = interactableLayer;
            }

            var trigger = zoneTransform.GetComponent<SphereCollider>();
            if (trigger == null)
            {
                trigger = zoneTransform.gameObject.AddComponent<SphereCollider>();
            }

            trigger.isTrigger = true;
            trigger.center = Vector3.zero;
            trigger.radius = 1.8f;
        }

        private static void EnsureFolder(string folderPath)
        {
            var parts = folderPath.Split('/');
            var current = parts[0];

            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static void ClearInspectorSelectionBeforePrefabContentsEdit()
        {
            Selection.activeObject = null;
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static void SelectAssetAfterPrefabContentsEdit(string assetPath)
        {
            EditorApplication.delayCall += () =>
            {
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            };
        }

        private static void MoveComponentAfterTransform(Component component)
        {
            if (component == null)
            {
                return;
            }

            var components = component.GetComponents<Component>();
            for (var index = 1; index < components.Length; index++)
            {
                if (components[index] == component)
                {
                    for (var move = index; move > 1; move--)
                    {
                        UnityEditorInternal.ComponentUtility.MoveComponentUp(component);
                    }

                    return;
                }
            }
        }
    }
}
