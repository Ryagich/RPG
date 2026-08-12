using System;
using CameraScripts;
using Container;
using Combat;
using Dialogue;
using GameModes;
using Inventory;
using Inventory.Inventories;
using Inventory.Item;
using Localization;
using MessagePipe;
using Messages;
using NPC;
using Quests;
using Quests.Graph;
using Quests.Graph.Model;
using Stats;
using TargetLock;
using UnityEngine;
using VContainer;

namespace Training
{
    /// <summary>
    /// Owns one arena session. Dialogue selects an event asset; this controller owns all
    /// subsequent world/session transitions and never leaks that knowledge into dialogue UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrainingSessionController : MonoBehaviour
    {
        private enum SessionKind
        {
            Tutorial,
            Sparring,
            EvasionPractice
        }

        private enum SessionOutcome
        {
            None,
            Draw,
            PlayerWon,
            OpponentWon
        }
        private enum TrainingState
        {
            Inactive,
            WaitingForArenaCamera,
            WaitingIntroductionDismiss,
            WaitingForEnemyVision,
            WaitingVisionDismiss,
            PreparingDodge,
            WaitingDodgeSwing,
            WaitingDodge,
            WaitingDodgeCompletion,
            ApproachingForAttackLesson,
            WaitingAttackLessonDismiss,
            WaitingPlayerAttack,
            WaitingPlayerAttackRecovery,
            WaitingRoll,
            WaitingRollCompletion,
            WaitingStaminaDismiss,
            WaitingLoadDismiss,
            WaitingFinalLessonDismiss,
            Sparring,
            Ending
        }

        [Header("Arena")]
        [SerializeField] private Transform playerSpawnPoint;
        [SerializeField] private Transform opponentSpawnPoint;
        [SerializeField] private GameObject sparringOpponentPrefab;
        [SerializeField, Min(1f)] private float sparringDurationSeconds = 300f;
        [Header("Dialogue events")]
        [SerializeField] private DialogueGameplayEvent startCombatTrainingEvent;
        [SerializeField] private DialogueGameplayEvent startSparringEvent;
        [SerializeField] private DialogueGameplayEvent startEvasionPracticeEvent;
        [Header("Temporary quest")]
        [SerializeField] private QuestGraph combatTrainingQuest;
        [SerializeField] private QuestNodeData[] combatTrainingQuestNodes;
        [Header("Training equipment")]
        [SerializeField] private ItemConfig temporaryTrainingWeapon;
        [SerializeField] private QuestGraph evasionPracticeQuest;
        [SerializeField] private QuestNodeData evasionPracticeQuestNode;
        [SerializeField, Min(1)] private int evasionPracticeTarget = 10;
        [Header("Post-session dialogue")]
        [SerializeField] private DialogueRuntimeFlag[] drawDialogueFlags;
        [SerializeField] private DialogueRuntimeFlag[] playerWonDialogueFlags;
        [SerializeField] private DialogueRuntimeFlag[] opponentWonDialogueFlags;

        private IDisposable dialogueEventSubscription;
        private IDisposable lessonSkipSubscription;
        private IDisposable lessonEvasionInputSubscription;
        private IDisposable lessonAttackInputSubscription;
        private IDisposable dodgeSubscription;
        private IDisposable rollSubscription;
        private IDisposable evasionCompletedSubscription;
        private IDisposable opponentAttackStartedSubscription;
        private IDisposable damagedSubscription;
        private LessonConfig lessonConfig;
        private LessonPresentationContext lessonContext;
        private QuestSelectionLock questSelectionLock;
        private QuestObjectiveOverrideContext questObjectiveOverride;
        private DialogueRuntimeFlagRegistry dialogueRuntimeFlags;
        private IPublisher<ChangeGameModeRequest> gameModeRequestPublisher;
        private IPublisher<PlayerRelocatedMessage> playerRelocatedPublisher;
        private IPublisher<DodgeInputMessage> dodgeInputPublisher;
        private IPublisher<RollInputMessage> rollInputPublisher;
        private IPublisher<MouseDown> mouseDownPublisher;
        private IPublisher<WeaponSlotInputMessage> weaponSlotInputPublisher;
        private INonLethalCombatSessionRegistry nonLethalCombatSessions;
        private GameObject spawnedOpponent;
        private PlayerLifetimeScope playerScope;
        private NpcLifetimeScope opponentScope;
        private CharacterDamageReceiver playerDamageReceiver;
        private CharacterDamageReceiver opponentDamageReceiver;
        private ICharacterHitReactionController playerHitReaction;
        private ICharacterHitReactionController opponentHitReaction;
        private CharacterActionState playerActionState;
        private CharacterActionState opponentActionState;
        private TargetLockTarget playerTarget;
        private NpcVision opponentVision;
        private NpcNavMeshController opponentNavigation;
        private NpcWeaponInHandController opponentWeapon;
        private NpcStateMachineRunner opponentStateMachine;
        private PlayerWeaponInHandController playerWeapon;
        private CameraMotor cameraMotor;
        private StatsController playerStats;
        private StatsController opponentStats;
        private QuestController playerQuestController;
        private PlayerInventory playerInventory;
        private Animator playerAnimator;
        private Animator opponentAnimator;
        private TrainingState state;
        private SessionKind sessionKind;
        private Pose returnPose;
        private float startingPlayerHp;
        private float startingOpponentHp;
        private float opponentHpBeforeRequiredAttack = float.NaN;
        private float sparringElapsedSeconds;
        private AnimatorUpdateMode previousPlayerAnimatorUpdateMode;
        private AnimatorUpdateMode previousOpponentAnimatorUpdateMode;
        private bool animatorUpdateModeOverridden;
        private bool endingSheatheRequested;
        private bool playerSheatheInputBlockedLogged;
        private bool playerSheathedForEnding;
        private bool opponentSheathedForEnding;
        private bool isActive;
        private SessionOutcome sessionOutcome;
        private string temporaryWeaponRuntimeTag;
        private int evasionPracticeCount;
        private bool evasionAttemptPending;
        private bool evasionAttemptWasHit;
        private float lessonSkipAvailableAt;

        [Inject]
        public void Construct(
            ISubscriber<DialogueGameplayEventRaisedMessage> dialogueEventSubscriber,
            ISubscriber<LessonSkipInputMessage> lessonSkipSubscriber,
            ISubscriber<LessonEvasionInputMessage> lessonEvasionInputSubscriber,
            ISubscriber<LessonAttackInputMessage> lessonAttackInputSubscriber,
            ISubscriber<DodgeInputMessage> dodgeSubscriber,
            ISubscriber<RollInputMessage> rollSubscriber,
            ISubscriber<PlayerEvasionCompletedMessage> evasionCompletedSubscriber,
            ISubscriber<NpcAttackStartedMessage> opponentAttackStartedSubscriber,
            ISubscriber<CharacterDamagedMessage> damagedSubscriber,
            LessonConfig lessonConfig,
            LessonPresentationContext lessonContext,
            QuestSelectionLock questSelectionLock,
            QuestObjectiveOverrideContext questObjectiveOverride,
            DialogueRuntimeFlagRegistry dialogueRuntimeFlags,
            IPublisher<ChangeGameModeRequest> gameModeRequestPublisher,
            IPublisher<PlayerRelocatedMessage> playerRelocatedPublisher,
            IPublisher<DodgeInputMessage> dodgeInputPublisher,
            IPublisher<RollInputMessage> rollInputPublisher,
            IPublisher<MouseDown> mouseDownPublisher,
            IPublisher<WeaponSlotInputMessage> weaponSlotInputPublisher,
            INonLethalCombatSessionRegistry nonLethalCombatSessions)
        {
            dialogueEventSubscription?.Dispose();
            dialogueEventSubscription = dialogueEventSubscriber.Subscribe(OnDialogueGameplayEvent);
            lessonSkipSubscription?.Dispose();
            lessonSkipSubscription = lessonSkipSubscriber.Subscribe(OnLessonSkipInput);
            lessonEvasionInputSubscription?.Dispose();
            lessonEvasionInputSubscription = lessonEvasionInputSubscriber.Subscribe(OnLessonEvasionInput);
            lessonAttackInputSubscription?.Dispose();
            lessonAttackInputSubscription = lessonAttackInputSubscriber.Subscribe(OnLessonAttackInput);
            dodgeSubscription?.Dispose();
            dodgeSubscription = dodgeSubscriber.Subscribe(OnGameplayDodgeInput);
            rollSubscription?.Dispose();
            rollSubscription = rollSubscriber.Subscribe(OnGameplayRollInput);
            evasionCompletedSubscription?.Dispose();
            evasionCompletedSubscription = evasionCompletedSubscriber.Subscribe(OnEvasionCompleted);
            opponentAttackStartedSubscription?.Dispose();
            opponentAttackStartedSubscription = opponentAttackStartedSubscriber.Subscribe(OnOpponentAttackStarted);
            damagedSubscription?.Dispose();
            damagedSubscription = damagedSubscriber.Subscribe(OnCharacterDamaged);
            this.lessonConfig = lessonConfig;
            this.lessonContext = lessonContext;
            this.questSelectionLock = questSelectionLock;
            this.questObjectiveOverride = questObjectiveOverride;
            this.dialogueRuntimeFlags = dialogueRuntimeFlags;
            this.gameModeRequestPublisher = gameModeRequestPublisher;
            this.playerRelocatedPublisher = playerRelocatedPublisher;
            this.dodgeInputPublisher = dodgeInputPublisher;
            this.rollInputPublisher = rollInputPublisher;
            this.mouseDownPublisher = mouseDownPublisher;
            this.weaponSlotInputPublisher = weaponSlotInputPublisher;
            this.nonLethalCombatSessions = nonLethalCombatSessions;
        }

        private void OnDestroy()
        {
            dialogueEventSubscription?.Dispose();
            lessonSkipSubscription?.Dispose();
            lessonEvasionInputSubscription?.Dispose();
            lessonAttackInputSubscription?.Dispose();
            dodgeSubscription?.Dispose();
            rollSubscription?.Dispose();
            evasionCompletedSubscription?.Dispose();
            opponentAttackStartedSubscription?.Dispose();
            damagedSubscription?.Dispose();

            if (playerDamageReceiver != null)
            {
                nonLethalCombatSessions?.End(playerDamageReceiver);
            }

            RemoveTemporaryTrainingWeapon();

            RestoreAnimatorTimeAfterSheathing();
        }

        private void Update()
        {
            if (!isActive)
            {
                return;
            }

            BindParticipantsIfReady();

            switch (state)
            {
                case TrainingState.WaitingForArenaCamera:
                    if (cameraMotor == null || cameraMotor.IsGameplaySettled)
                    {
                        ShowLesson(LessonId.CombatIntroduction);
                        state = TrainingState.WaitingIntroductionDismiss;
                    }
                    break;
                case TrainingState.WaitingForEnemyVision:
                    if (opponentVision != null && playerScope != null && opponentVision.IsInView(playerScope.transform))
                    {
                        ShowLesson(LessonId.EnemyVision);
                        state = TrainingState.WaitingVisionDismiss;
                    }
                    break;
                case TrainingState.PreparingDodge:
                    PrepareDodgeLesson();
                    break;
                case TrainingState.ApproachingForAttackLesson:
                    PrepareAttackLesson();
                    break;
                case TrainingState.WaitingPlayerAttack:
                    WaitForPlayerAttackToDamageOpponent();
                    break;
                case TrainingState.WaitingPlayerAttackRecovery:
                    ShowRollLessonWhenPlayerActionCompletes();
                    break;
                case TrainingState.WaitingRollCompletion:
                    ShowStaminaLessonWhenPlayerRollCompletes();
                    break;
                case TrainingState.Sparring:
                    sparringElapsedSeconds += Time.deltaTime;
                    if (sparringElapsedSeconds >= sparringDurationSeconds)
                    {
                        BeginEnding(SessionOutcome.Draw);
                    }
                    break;
                case TrainingState.Ending:
                    BeginEndingSheathingIfReady();
                    break;
            }
        }

        private void OnDialogueGameplayEvent(DialogueGameplayEventRaisedMessage message)
        {
            if (message.Event == null || isActive)
            {
                return;
            }

            if (message.Event == startCombatTrainingEvent)
            {
                StartSession(SessionKind.Tutorial);
            }
            else if (message.Event == startSparringEvent)
            {
                StartSession(SessionKind.Sparring);
            }
            else if (message.Event == startEvasionPracticeEvent)
            {
                StartSession(SessionKind.EvasionPractice);
            }
        }

        private void StartSession(SessionKind kind)
        {
            playerScope = FindFirstObjectByType<PlayerLifetimeScope>();
            if (playerScope == null || playerSpawnPoint == null || opponentSpawnPoint == null || sparringOpponentPrefab == null)
            {
                Debug.LogWarning("Combat training cannot start: arena or player dependencies are not configured.", this);
                return;
            }

            isActive = true;
            sessionKind = kind;
            sessionOutcome = SessionOutcome.None;
            opponentHpBeforeRequiredAttack = float.NaN;
            ClearOutcomeDialogueFlags();
            returnPose = new Pose(playerScope.transform.position, playerScope.transform.rotation);
            PlacePlayer(playerScope);
            playerRelocatedPublisher.Publish(new PlayerRelocatedMessage());

            if (spawnedOpponent != null)
            {
                Destroy(spawnedOpponent);
            }

            spawnedOpponent = Instantiate(
                sparringOpponentPrefab,
                opponentSpawnPoint.position,
                opponentSpawnPoint.rotation,
                transform);

            if (kind == SessionKind.Tutorial)
            {
                // Keep the game unpaused until the follow camera catches up with the teleported
                // player. Lesson mode pauses the camera together with world simulation.
                state = TrainingState.WaitingForArenaCamera;
            }
            else
            {
                opponentStateMachine?.SetExternalControl(false);
                sparringElapsedSeconds = 0f;
                state = TrainingState.Sparring;
            }
        }

        private void BindParticipantsIfReady()
        {
            if (playerDamageReceiver != null || spawnedOpponent == null || playerScope == null)
            {
                return;
            }

            opponentScope = spawnedOpponent.GetComponent<NpcLifetimeScope>();
            if (opponentScope?.Container == null || playerScope.Container == null)
            {
                return;
            }

            playerDamageReceiver = playerScope.Container.Resolve<CharacterDamageReceiver>();
            opponentDamageReceiver = opponentScope.Container.Resolve<CharacterDamageReceiver>();
            playerHitReaction = playerScope.Container.Resolve<ICharacterHitReactionController>();
            opponentHitReaction = opponentScope.Container.Resolve<ICharacterHitReactionController>();
            playerActionState = playerScope.Container.Resolve<CharacterActionState>();
            opponentActionState = opponentScope.Container.Resolve<CharacterActionState>();
            playerTarget = playerScope.Container.Resolve<TargetLockTarget>();
            playerWeapon = playerScope.Container.Resolve<PlayerWeaponInHandController>();
            cameraMotor = playerScope.Container.Resolve<CameraMotor>();
            playerStats = playerScope.Container.Resolve<StatsController>();
            playerQuestController = playerScope.Container.Resolve<QuestController>();
            playerInventory = playerScope.Container.Resolve<PlayerInventory>();
            opponentStats = opponentScope.Container.Resolve<StatsController>();
            playerAnimator = playerScope.Container.Resolve<Animator>();
            opponentAnimator = opponentScope.Container.Resolve<Animator>();
            opponentVision = opponentScope.Container.Resolve<NpcVision>();
            opponentNavigation = opponentScope.Container.Resolve<NpcNavMeshController>();
            opponentWeapon = opponentScope.Container.Resolve<NpcWeaponInHandController>();
            opponentStateMachine = opponentScope.Container.Resolve<NpcStateMachineRunner>();

            opponentStateMachine?.SetExternalControl(true);
            nonLethalCombatSessions.Begin(playerDamageReceiver, opponentDamageReceiver);
            startingPlayerHp = playerDamageReceiver.CurrentHp;
            startingOpponentHp = opponentDamageReceiver.CurrentHp;

            EnsureTrainingWeapon();

            BeginTutorialQuestIfNeeded();
            BeginEvasionPracticeQuestIfNeeded();

            if (state == TrainingState.Sparring)
            {
                opponentStateMachine?.SetExternalControl(false);
            }
        }

        private void PrepareDodgeLesson()
        {
            if (opponentWeapon == null || opponentNavigation == null || playerScope == null)
            {
                return;
            }

            if (!opponentWeapon.IsWeaponDrawn)
            {
                opponentWeapon.RequestDrawWeapon();
                return;
            }

            var attackDistance = Mathf.Max(0.75f, opponentVision != null ? opponentVision.AttackViewDistance * 0.85f : 1.5f);
            if (PlanarDistance(spawnedOpponent.transform.position, playerScope.transform.position) > attackDistance)
            {
                opponentNavigation.MoveTo(playerScope.transform.position, stoppingDistance: attackDistance);
                return;
            }

            opponentNavigation.Stop();
            if (opponentWeapon.RequestAttack())
            {
                state = TrainingState.WaitingDodgeSwing;
            }
        }

        private void PrepareAttackLesson()
        {
            if (opponentNavigation == null || playerScope == null)
            {
                return;
            }

            var attackDistance = Mathf.Max(0.75f, opponentVision != null ? opponentVision.AttackViewDistance * 0.85f : 1.5f);
            if (PlanarDistance(spawnedOpponent.transform.position, playerScope.transform.position) > attackDistance)
            {
                opponentNavigation.MoveTo(playerScope.transform.position, stoppingDistance: attackDistance);
                return;
            }

            opponentNavigation.Stop();
            ShowLesson(LessonId.WeaponAttacks);
            state = TrainingState.WaitingAttackLessonDismiss;
        }

        private void OnLessonSkipInput(LessonSkipInputMessage _)
        {
            var lesson = lessonContext?.CurrentLesson;
            if (!isActive
                || lesson == null
                || !lesson.CanSkipWithLessonInput
                || Time.unscaledTime < lessonSkipAvailableAt)
            {
                return;
            }

            AdvanceAfterLessonDismissal(lesson.Id);
        }

        private void AdvanceAfterLessonDismissal(LessonId lessonId)
        {
            switch (state)
            {
                case TrainingState.WaitingIntroductionDismiss when lessonId == LessonId.CombatIntroduction:
                    ResumeGameplay();
                    state = TrainingState.WaitingForEnemyVision;
                    break;
                case TrainingState.WaitingVisionDismiss when lessonId == LessonId.EnemyVision:
                    ResumeGameplay();
                    state = TrainingState.PreparingDodge;
                    break;
                case TrainingState.WaitingStaminaDismiss when lessonId == LessonId.Stamina:
                    ShowLesson(LessonId.StaminaLoad);
                    state = TrainingState.WaitingLoadDismiss;
                    break;
                case TrainingState.WaitingLoadDismiss when lessonId == LessonId.StaminaLoad:
                    ShowLesson(LessonId.FinalSparring);
                    state = TrainingState.WaitingFinalLessonDismiss;
                    break;
                case TrainingState.WaitingFinalLessonDismiss when lessonId == LessonId.FinalSparring:
                    ResumeGameplay();
                    opponentStateMachine?.SetExternalControl(false);
                    sparringElapsedSeconds = 0f;
                    state = TrainingState.Sparring;
                    break;
            }
        }

        private void OnGameplayDodgeInput(DodgeInputMessage _)
        {
            BeginEvasionPracticeAttemptIfEligible();
        }

        private void OnGameplayRollInput(RollInputMessage _)
        {
            BeginEvasionPracticeAttemptIfEligible();
        }

        private void OnLessonEvasionInput(LessonEvasionInputMessage message)
        {
            switch (message.Action)
            {
                case LessonEvasionAction.Dodge when state == TrainingState.WaitingDodge:
                    // The physical key press was intentionally not forwarded while the lesson
                    // paused gameplay. Transition first, then issue one normal combat command.
                    // This is independent of MessagePipe subscriber ordering.
                    state = TrainingState.WaitingDodgeCompletion;
                    ResumeGameplay();
                    dodgeInputPublisher.Publish(new DodgeInputMessage());
                    break;
                case LessonEvasionAction.Roll when state == TrainingState.WaitingRoll:
                    state = TrainingState.WaitingRollCompletion;
                    ResumeGameplay();
                    rollInputPublisher.Publish(new RollInputMessage());
                    break;
            }
        }

        private void OnLessonAttackInput(LessonAttackInputMessage message)
        {
            if (state != TrainingState.WaitingAttackLessonDismiss)
            {
                return;
            }

            // The pressed button is held while gameplay is paused. Resume first so the
            // weapon controller receives one ordinary light or heavy attack command.
            opponentHpBeforeRequiredAttack = opponentDamageReceiver?.CurrentHp ?? float.NaN;
            state = TrainingState.WaitingPlayerAttack;
            ResumeGameplay();
            mouseDownPublisher.Publish(new MouseDown(message.Button));
        }

        private void WaitForPlayerAttackToDamageOpponent()
        {
            if (opponentDamageReceiver == null
                || float.IsNaN(opponentHpBeforeRequiredAttack)
                || opponentDamageReceiver.CurrentHp >= opponentHpBeforeRequiredAttack)
            {
                return;
            }

            BeginRollLessonAfterPlayerAttack();
        }

        private void OnOpponentAttackStarted(NpcAttackStartedMessage message)
        {
            if (state != TrainingState.WaitingDodgeSwing || message.CharacterTransform != spawnedOpponent?.transform)
            {
                return;
            }

            ShowLesson(LessonId.Dodge);
            state = TrainingState.WaitingDodge;
        }

        private void OnEvasionCompleted(PlayerEvasionCompletedMessage message)
        {
            if (sessionKind == SessionKind.EvasionPractice && state == TrainingState.Sparring)
            {
                CompleteEvasionPracticeAttempt();
                return;
            }

            if (state == TrainingState.WaitingDodgeCompletion)
            {
                state = TrainingState.ApproachingForAttackLesson;
                return;
            }

        }

        private void ShowRollLessonWhenPlayerActionCompletes()
        {
            if (playerWeapon == null || playerWeapon.IsCombatActionLocked)
            {
                return;
            }

            ShowLesson(LessonId.Roll);
            state = TrainingState.WaitingRoll;
        }

        private void ShowStaminaLessonWhenPlayerRollCompletes()
        {
            if (playerWeapon == null || playerWeapon.IsRollAnimationActive)
            {
                return;
            }

            ShowLesson(LessonId.Stamina);
            state = TrainingState.WaitingStaminaDismiss;
        }

        private void BeginRollLessonAfterPlayerAttack()
        {
            opponentHpBeforeRequiredAttack = float.NaN;
            state = TrainingState.WaitingPlayerAttackRecovery;
        }

        private void OnCharacterDamaged(CharacterDamagedMessage message)
        {
            if (state == TrainingState.Sparring)
            {
                if (sessionKind == SessionKind.EvasionPractice && message.CharacterTransform == playerScope?.transform)
                {
                    evasionAttemptWasHit = true;
                }
                if (message.CharacterTransform == playerScope?.transform || message.CharacterTransform == spawnedOpponent?.transform)
                {
                    // Evasion practice is a timed drill rather than a duel: non-lethal combat
                    // still protects both participants, but reaching one HP must not end it.
                    if (sessionKind != SessionKind.EvasionPractice
                        && (playerDamageReceiver?.CurrentHp <= 1f || opponentDamageReceiver?.CurrentHp <= 1f))
                    {
                        BeginEnding(playerDamageReceiver?.CurrentHp <= 1f
                            ? SessionOutcome.OpponentWon
                            : SessionOutcome.PlayerWon);
                    }
                }

                return;
            }

            if (state != TrainingState.WaitingPlayerAttack
                || message.Attacker != playerDamageReceiver
                || message.CharacterTransform != spawnedOpponent?.transform)
            {
                return;
            }

            BeginRollLessonAfterPlayerAttack();
        }

        private void BeginEnding(SessionOutcome outcome = SessionOutcome.None)
        {
            if (!isActive || state == TrainingState.Ending)
            {
                return;
            }

            state = TrainingState.Ending;
            endingSheatheRequested = false;
            playerSheatheInputBlockedLogged = false;
            playerSheathedForEnding = false;
            opponentSheathedForEnding = false;
            sessionOutcome = outcome;
            SetOutcomeDialogueFlag();
            lessonContext.Clear();
            CompleteTutorialQuestIfNeeded();
            EndEvasionPracticeQuestIfNeeded();
            opponentStateMachine?.SetExternalControl(true);
            opponentNavigation?.Stop();

            gameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Lesson));
        }

        private void BeginEndingSheathingIfReady()
        {
            if (endingSheatheRequested)
            {
                CompleteEndingWhenWeaponsAreSheathed();
                return;
            }

            // CharacterDamagedMessage can start a hit reaction after this session receives the
            // same message. Deferring this phase until Update guarantees that every subscriber
            // has observed the hit before the terminal sequence clears that combat-only state.
            playerHitReaction?.CancelReaction();
            opponentHitReaction?.CancelReaction();
            OverrideAnimatorTimeForSheathing();

            // The action state is released by each clip's UnlockMovement/AttackFinished event.
            // The terminal sequence must wait for that gameplay-owned boundary, then starts
            // the ordinary sheathing transitions together.
            if (!AreParticipantsReadyForSheathing())
            {
                return;
            }

            playerSheathedForEnding = playerWeapon == null || playerWeapon.IsWeaponSheathed;
            opponentSheathedForEnding = opponentWeapon == null || opponentWeapon.IsWeaponSheathed;

            if (!playerSheathedForEnding)
            {
                if (!RequestPlayerWeaponSheathingThroughSlotInput())
                {
                    return;
                }
            }

            endingSheatheRequested = true;

            if (!opponentSheathedForEnding)
            {
                Debug.Log("[TrainingEndingSheathe] Requesting opponent sheathing.");
                opponentWeapon.RequestSheatheWeapon();
            }

            CompleteEndingWhenWeaponsAreSheathed();
        }

        private bool AreParticipantsReadyForSheathing()
        {
            return (playerActionState == null || !playerActionState.IsActionBlocked)
                   && (opponentActionState == null || !opponentActionState.IsActionBlocked);
        }

        private bool RequestPlayerWeaponSheathingThroughSlotInput()
        {
            if (playerWeapon == null)
            {
                Debug.Log("[TrainingEndingSheathe] Player weapon controller is unavailable; no slot input was sent.");
                return true;
            }

            if (!playerWeapon.IsWeaponDrawn)
            {
                Debug.Log($"[TrainingEndingSheathe] Player weapon is already marked as sheathed; no slot input was sent. ActiveSlot={playerWeapon.ActiveWeaponSlotIndex}, IsWeaponSheathed={playerWeapon.IsWeaponSheathed}.");
                return true;
            }

            if (!playerWeapon.CanProcessWeaponSlotInput)
            {
                if (!playerSheatheInputBlockedLogged)
                {
                    Debug.Log("[TrainingEndingSheathe] Waiting until player can process normal weapon-slot input.");
                    playerSheatheInputBlockedLogged = true;
                }

                return false;
            }

            var activeSlotIndex = playerWeapon.ActiveWeaponSlotIndex;
            Debug.Log($"[TrainingEndingSheathe] Publishing normal player weapon-slot input to sheathe. ActiveSlot={activeSlotIndex}.");
            weaponSlotInputPublisher.Publish(new WeaponSlotInputMessage(activeSlotIndex));
            return true;
        }

        private void CompleteEndingWhenWeaponsAreSheathed()
        {
            playerSheathedForEnding |= playerWeapon == null || playerWeapon.IsWeaponSheathed;
            opponentSheathedForEnding |= opponentWeapon == null || opponentWeapon.IsWeaponSheathed;

            if (!playerSheathedForEnding || !opponentSheathedForEnding)
            {
                return;
            }

            RestoreAnimatorTimeAfterSheathing();
            RestoreHealth(playerStats, startingPlayerHp);
            RestoreHealth(opponentStats, startingOpponentHp);
            nonLethalCombatSessions.End(playerDamageReceiver);
            RemoveTemporaryTrainingWeapon();

            if (playerScope != null)
            {
                PlacePlayerAtPose(playerScope, returnPose);
                playerRelocatedPublisher.Publish(new PlayerRelocatedMessage());
            }

            if (spawnedOpponent != null)
            {
                Destroy(spawnedOpponent);
            }

            spawnedOpponent = null;
            isActive = false;
            state = TrainingState.Inactive;
            gameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
        }

        private void OverrideAnimatorTimeForSheathing()
        {
            if (animatorUpdateModeOverridden)
            {
                return;
            }

            if (playerAnimator != null)
            {
                previousPlayerAnimatorUpdateMode = playerAnimator.updateMode;
                playerAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }

            if (opponentAnimator != null)
            {
                previousOpponentAnimatorUpdateMode = opponentAnimator.updateMode;
                opponentAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }

            animatorUpdateModeOverridden = true;
        }

        private void RestoreAnimatorTimeAfterSheathing()
        {
            if (!animatorUpdateModeOverridden)
            {
                return;
            }

            if (playerAnimator != null)
            {
                playerAnimator.updateMode = previousPlayerAnimatorUpdateMode;
            }

            if (opponentAnimator != null)
            {
                opponentAnimator.updateMode = previousOpponentAnimatorUpdateMode;
            }

            animatorUpdateModeOverridden = false;
        }

        private static void RestoreHealth(StatsController statsController, float startingHp)
        {
            if (statsController?.Hp?.Value == null)
            {
                return;
            }

            var currentHp = statsController.Hp.Value.Value;
            var targetHp = Mathf.Max(startingHp, currentHp);
            statsController.AddValue(StatType.Hp, targetHp - currentHp);
        }

        private void ShowLesson(LessonId lessonId)
        {
            if (!lessonConfig.TryGetLesson(lessonId, out var lesson))
            {
                Debug.LogWarning($"Training lesson '{lessonId}' is not configured.", this);
                return;
            }

            lessonContext.Show(lesson);
            SetTutorialQuestStage(lessonId);
            lessonSkipAvailableAt = Time.unscaledTime + Mathf.Max(0f, lessonConfig.SkipTextShowDelay);
            gameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Lesson));
        }

        private void BeginTutorialQuestIfNeeded()
        {
            if (sessionKind != SessionKind.Tutorial || combatTrainingQuest == null || playerQuestController == null)
            {
                return;
            }

            if (!playerQuestController.HasQuest(combatTrainingQuest))
            {
                playerQuestController.TryAddQuest(combatTrainingQuest);
            }

            playerQuestController.TrySetCurrentQuest(combatTrainingQuest);
            questSelectionLock?.Lock();
        }

        private void SetTutorialQuestStage(LessonId lessonId)
        {
            if (sessionKind != SessionKind.Tutorial
                || combatTrainingQuest == null
                || playerQuestController == null
                || combatTrainingQuestNodes == null)
            {
                return;
            }

            int index = (int)lessonId;
            if (index < 0 || index >= combatTrainingQuestNodes.Length)
            {
                return;
            }

            QuestNodeData node = combatTrainingQuestNodes[index];
            if (node != null)
            {
                playerQuestController.TrySetCurrentNode(combatTrainingQuest, node);
            }
        }

        private void CompleteTutorialQuestIfNeeded()
        {
            if (sessionKind != SessionKind.Tutorial || combatTrainingQuest == null || playerQuestController == null)
            {
                return;
            }

            QuestNodeData currentNode = playerQuestController.GetCurrentNode(combatTrainingQuest);
            if (currentNode != null)
            {
                playerQuestController.TryCompleteNode(combatTrainingQuest, currentNode);
            }

            questSelectionLock?.Unlock();
        }

        private void BeginEvasionPracticeQuestIfNeeded()
        {
            if (sessionKind != SessionKind.EvasionPractice || evasionPracticeQuest == null || playerQuestController == null)
            {
                return;
            }

            evasionPracticeCount = 0;
            evasionAttemptPending = false;
            evasionAttemptWasHit = false;
            if (!playerQuestController.HasQuest(evasionPracticeQuest))
            {
                playerQuestController.TryAddQuest(evasionPracticeQuest);
            }

            playerQuestController.TrySetCurrentQuest(evasionPracticeQuest);
            questSelectionLock?.Lock();
            RefreshEvasionPracticeObjective();
        }

        private void EndEvasionPracticeQuestIfNeeded()
        {
            if (sessionKind != SessionKind.EvasionPractice || evasionPracticeQuest == null || playerQuestController == null)
            {
                return;
            }

            if (playerQuestController.HasQuest(evasionPracticeQuest))
            {
                playerQuestController.TryRemoveQuest(evasionPracticeQuest);
            }

            questObjectiveOverride?.Clear(evasionPracticeQuest);
            questSelectionLock?.Unlock();
        }

        private void BeginEvasionPracticeAttemptIfEligible()
        {
            if (sessionKind != SessionKind.EvasionPractice
                || state != TrainingState.Sparring
                || evasionPracticeCount >= evasionPracticeTarget
                || opponentWeapon?.IsAttackInProgress != true
                || playerScope == null
                || spawnedOpponent == null)
            {
                return;
            }

            var attackRange = opponentVision != null ? opponentVision.AttackViewDistance : 1.5f;
            if (PlanarDistance(playerScope.transform.position, spawnedOpponent.transform.position) > attackRange)
            {
                return;
            }

            evasionAttemptPending = true;
            evasionAttemptWasHit = false;
        }

        private void CompleteEvasionPracticeAttempt()
        {
            if (!evasionAttemptPending)
            {
                return;
            }

            evasionAttemptPending = false;
            if (evasionAttemptWasHit || evasionPracticeCount >= evasionPracticeTarget)
            {
                return;
            }

            evasionPracticeCount++;
            RefreshEvasionPracticeObjective();
            if (evasionPracticeCount < evasionPracticeTarget || evasionPracticeQuest == null || evasionPracticeQuestNode == null)
            {
                return;
            }

            playerQuestController?.TryCompleteNode(evasionPracticeQuest, evasionPracticeQuestNode);
            questObjectiveOverride?.Clear(evasionPracticeQuest);
        }

        private void RefreshEvasionPracticeObjective()
        {
            if (evasionPracticeQuest == null || lessonConfig == null)
            {
                return;
            }

            questObjectiveOverride?.Set(
                evasionPracticeQuest,
                string.Empty,
                string.Format(
                    lessonConfig.EvasionPracticeObjective.GetLocalizedStringCached(),
                    evasionPracticeCount,
                    evasionPracticeTarget));
        }

        private void ClearOutcomeDialogueFlags()
        {
            ClearOutcomeDialogueFlags(drawDialogueFlags);
            ClearOutcomeDialogueFlags(playerWonDialogueFlags);
            ClearOutcomeDialogueFlags(opponentWonDialogueFlags);
        }

        private void ClearOutcomeDialogueFlags(System.Collections.Generic.IReadOnlyList<DialogueRuntimeFlag> flags)
        {
            if (dialogueRuntimeFlags == null || flags == null)
            {
                return;
            }

            foreach (DialogueRuntimeFlag flag in flags)
            {
                dialogueRuntimeFlags.Deactivate(flag);
            }
        }

        private void SetOutcomeDialogueFlag()
        {
            if (sessionKind == SessionKind.EvasionPractice || dialogueRuntimeFlags == null)
            {
                return;
            }

            DialogueRuntimeFlag[] candidates = sessionOutcome switch
            {
                SessionOutcome.Draw => drawDialogueFlags,
                SessionOutcome.PlayerWon => playerWonDialogueFlags,
                SessionOutcome.OpponentWon => opponentWonDialogueFlags,
                _ => null
            };

            if (candidates == null || candidates.Length == 0)
            {
                return;
            }

            var validCandidates = new System.Collections.Generic.List<DialogueRuntimeFlag>();
            foreach (DialogueRuntimeFlag candidate in candidates)
            {
                if (candidate != null)
                {
                    validCandidates.Add(candidate);
                }
            }

            if (validCandidates.Count > 0)
            {
                dialogueRuntimeFlags.Replace(
                    validCandidates,
                    validCandidates[UnityEngine.Random.Range(0, validCandidates.Count)]);
            }
        }

        private void EnsureTrainingWeapon()
        {
            if (sessionKind != SessionKind.Tutorial
                || temporaryTrainingWeapon == null
                || playerInventory == null
                || HasPlayerWeapon())
            {
                return;
            }

            temporaryWeaponRuntimeTag = $"training:{System.Guid.NewGuid():N}";
            var issuedStack = new ItemStack(temporaryTrainingWeapon, runtimeTag: temporaryWeaponRuntimeTag);
            if (!playerInventory.TryPlaceInSlot(ItemType.Weapon, issuedStack, out ItemStack remainder, out _)
                || remainder != null)
            {
                temporaryWeaponRuntimeTag = null;
                Debug.LogWarning("Combat training could not issue its temporary weapon.", this);
            }
        }

        private bool HasPlayerWeapon()
        {
            if (playerInventory?.LeftWeaponSlot?.ItemConfig?.ItemType == ItemType.Weapon
                || playerInventory?.RightWeaponSlot?.ItemConfig?.ItemType == ItemType.Weapon)
            {
                return true;
            }

            if (playerInventory != null)
            {
                foreach (ItemInInventory item in playerInventory.Items)
                {
                    if (item?.ItemStack?.ItemConfig?.ItemType == ItemType.Weapon)
                    {
                        return true;
                    }
                }
            }

            return playerInventory?.HandSourceInventory.Value == playerInventory
                && playerInventory.HandSlot.Value?.ItemConfig?.ItemType == ItemType.Weapon;
        }

        private void RemoveTemporaryTrainingWeapon()
        {
            if (string.IsNullOrWhiteSpace(temporaryWeaponRuntimeTag))
            {
                return;
            }

            playerInventory?.RemoveRuntimeTaggedItems(temporaryWeaponRuntimeTag);
            foreach (ItemHolder holder in FindObjectsByType<ItemHolder>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (holder != null && holder.RuntimeTag == temporaryWeaponRuntimeTag)
                {
                    Destroy(holder.gameObject);
                }
            }

            temporaryWeaponRuntimeTag = null;
        }

        private void ResumeGameplay()
        {
            lessonContext.Clear();
            gameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
        }

        private static float PlanarDistance(Vector3 first, Vector3 second)
        {
            first.y = 0f;
            second.y = 0f;
            return Vector3.Distance(first, second);
        }

        private void PlacePlayer(PlayerLifetimeScope playerScope)
        {
            PlacePlayerAtPose(playerScope, new Pose(playerSpawnPoint.position, playerSpawnPoint.rotation));
        }

        private static void PlacePlayerAtPose(PlayerLifetimeScope playerScope, Pose pose)
        {
            var characterController = playerScope.GetComponent<CharacterController>();
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            playerScope.transform.SetPositionAndRotation(pose.position, pose.rotation);
            Physics.SyncTransforms();

            if (characterController != null)
            {
                characterController.enabled = true;
            }
        }
    }
}
