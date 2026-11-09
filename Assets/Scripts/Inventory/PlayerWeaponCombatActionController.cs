using System;
using Combat;
using GameModes;
using MessagePipe;
using Messages;
using Movement;
using Stats;
using TargetLock;
using UnityEngine;

namespace Inventory
{
    /// <summary>
    /// Coordinates player combat actions that are driven by the equipped weapon Animator.
    /// Weapon selection and draw/sheathe transitions remain outside this component.
    /// </summary>
    internal sealed class PlayerWeaponCombatActionController : IDisposable
    {
        private readonly Animator animator;
        private readonly CharacterController characterController;
        private readonly PlayerWeaponCombatAnimator combatAnimator;
        private readonly GameModesController gameModesController;
        private readonly PlayerMovementConfig movementConfig;
        private readonly PlayerMovementIntent movementIntent;
        private readonly PlayerAnimationController playerAnimationController;
        private readonly TargetLockController targetLockController;
        private readonly CharacterDamageReceiver ownerDamageReceiver;
        private readonly CharacterActionState actionState;
        private readonly StatsController statsController;
        private readonly IPublisher<PlayerEvasionCompletedMessage> evasionCompletedPublisher;
        private readonly EquippedWeaponDamageWindowController damageWindow;
        private readonly Func<GameObject> weaponVisualProvider;
        private readonly Func<Item.ItemConfig> weaponConfigProvider;
        private readonly Action fullBodyActionRequested;

        private bool isHitAttackInProgress;
        private Vector2? queuedEvasionDirection;
        private float? bufferedActionRequestExpiryTime;

        public PlayerWeaponCombatActionController(
            Animator animator,
            CharacterController characterController,
            PlayerWeaponCombatAnimator combatAnimator,
            GameModesController gameModesController,
            PlayerMovementConfig movementConfig,
            PlayerMovementIntent movementIntent,
            PlayerAnimationController playerAnimationController,
            TargetLockController targetLockController,
            CharacterDamageReceiver ownerDamageReceiver,
            CharacterActionState actionState,
            StatsController statsController,
            IPublisher<PlayerEvasionCompletedMessage> evasionCompletedPublisher,
            EquippedWeaponDamageWindowController damageWindow,
            Func<GameObject> weaponVisualProvider,
            Func<Item.ItemConfig> weaponConfigProvider,
            Action fullBodyActionRequested)
        {
            this.animator = animator;
            this.characterController = characterController;
            this.combatAnimator = combatAnimator;
            this.gameModesController = gameModesController;
            this.movementConfig = movementConfig;
            this.movementIntent = movementIntent;
            this.playerAnimationController = playerAnimationController;
            this.targetLockController = targetLockController;
            this.ownerDamageReceiver = ownerDamageReceiver;
            this.actionState = actionState;
            this.statsController = statsController;
            this.evasionCompletedPublisher = evasionCompletedPublisher;
            this.damageWindow = damageWindow;
            this.weaponVisualProvider = weaponVisualProvider;
            this.weaponConfigProvider = weaponConfigProvider;
            this.fullBodyActionRequested = fullBodyActionRequested;
        }

        public bool IsCombatActionLocked => IsCombatActionAnimationActive();
        public bool IsHitAttackInProgress => isHitAttackInProgress;
        public bool IsRollAnimationActive => IsRollInProgress();
        public bool IsAttackBlockingWeaponChanges => isHitAttackInProgress || IsCombatActionAnimationActive();
        public bool IsAttackRootMotionStateActive => combatAnimator.IsFullBodyActionActive();

        public void Start()
        {
            movementIntent.DirectionChanged += OnMovementDirectionChanged;
            ResetAnimationRequests();
            ownerDamageReceiver?.SetWeaponDamageBlocked(false);
        }

        public void Dispose()
        {
            movementIntent.DirectionChanged -= OnMovementDirectionChanged;
            ClearBufferedActionRequestExpiry();
            queuedEvasionDirection = null;
            ownerDamageReceiver?.SetWeaponDamageBlocked(false);
            damageWindow?.End();
        }

        public void TryTriggerAttack(MouseButtonType button)
        {
            if (animator == null
             || gameModesController.GameMode != GameMode.Game
             || (actionState.IsActionBlocked && !isHitAttackInProgress))
            {
                return;
            }

            queuedEvasionDirection = null;
            targetLockController?.TryFaceAttackTarget();
            var isHeavyAttack = button == MouseButtonType.Right;
            SpendStamina(isHeavyAttack ? GetStamina().HeavyAttackCost : GetStamina().LightAttackCost);
            RequestFullBodyAction(!isHeavyAttack, isHeavyAttack, dodgeRequested: false, rollRequested: false);
        }

        public void TryRequestDodge()
        {
            if (!CanRequestEvasion())
            {
                return;
            }

            SpendStamina(GetStamina().DodgeCost);
            CaptureEvasionDirection();
            RequestFullBodyAction(lightAttackRequested: false, heavyAttackRequested: false, dodgeRequested: true, rollRequested: false);
        }

        public void TryRequestRoll()
        {
            if (!CanRequestEvasion())
            {
                return;
            }

            SpendStamina(GetStamina().RollCost);
            CaptureEvasionDirection();
            RequestFullBodyAction(lightAttackRequested: false, heavyAttackRequested: false, dodgeRequested: false, rollRequested: true);
        }

        public void HandleGameModeChanged(GameModeChangedMessage message)
        {
            if (message.GameMode == GameMode.Game)
            {
                // Returning from UI only re-enables gameplay input. A full-body action keeps
                // ownership of its locomotion lock until its UnlockMovement animation event.
                return;
            }

            Cancel();
        }

        public void AttackStartedFromAnimationEvent()
        {
            isHitAttackInProgress = true;
        }

        public void BeginDamageWindowFromAnimationEvent()
        {
            damageWindow?.Begin(
                weaponVisualProvider?.Invoke(),
                weaponConfigProvider?.Invoke(),
                ownerDamageReceiver,
                combatAnimator.IsHeavyAttackHitActive());
        }

        public void EndDamageWindowFromAnimationEvent()
        {
            damageWindow?.End();
        }

        public void EnableDamageImmunityFromAnimationEvent()
        {
            ownerDamageReceiver?.SetWeaponDamageBlocked(true);
        }

        public void DisableDamageImmunityFromAnimationEvent()
        {
            ownerDamageReceiver?.SetWeaponDamageBlocked(false);
        }

        public void AttackFinishedFromAnimationEvent()
        {
            isHitAttackInProgress = false;
        }

        public void EvasionFinishedFromAnimationEvent()
        {
            if (IsEvasionInProgress())
            {
                evasionCompletedPublisher.Publish(new PlayerEvasionCompletedMessage(IsRollInProgress()));
            }
        }

        public void Tick()
        {
            if (!bufferedActionRequestExpiryTime.HasValue
                || Time.time < bufferedActionRequestExpiryTime.Value)
            {
                return;
            }

            SetAnimationRequests(lightAttackRequested: false, heavyAttackRequested: false, dodgeRequested: false, rollRequested: false);
            ClearBufferedActionRequestExpiry();
            queuedEvasionDirection = null;
        }

        public void ResetAnimationRequests()
        {
            SetAnimationRequests(lightAttackRequested: false, heavyAttackRequested: false, dodgeRequested: false, rollRequested: false);
            ClearBufferedActionRequestExpiry();
            ApplyQueuedEvasionDirection();
        }

        public void Cancel()
        {
            DisableDamageImmunityFromAnimationEvent();
            ClearBufferedActionRequestExpiry();
            queuedEvasionDirection = null;
            damageWindow?.End();

            if (isHitAttackInProgress)
            {
                isHitAttackInProgress = false;
            }

            ResetAnimationRequests();
        }

        public void InterruptByHitReaction()
        {
            Cancel();
        }

        private bool CanRequestEvasion()
        {
            return animator != null
                   && characterController != null
                   && characterController.enabled
                   && characterController.isGrounded
                   && gameModesController.GameMode == GameMode.Game
                   && !combatAnimator.IsAnyRequestActive()
                   && !combatAnimator.IsHitActive()
                   && (!actionState.IsActionBlocked || isHitAttackInProgress);
        }

        private bool IsCombatActionAnimationActive()
        {
            return IsAttackRootMotionStateActive || combatAnimator.IsAnyRequestActive();
        }

        private bool IsEvasionInProgress()
        {
            return combatAnimator.IsDodgeActive() || IsRollInProgress();
        }

        private bool IsRollInProgress()
        {
            return combatAnimator.IsRollActive();
        }

        private void CaptureEvasionDirection()
        {
            var direction = playerAnimationController?.ResolveEvasionDirection() ?? Vector2.zero;
            if (IsEvasionInProgress())
            {
                queuedEvasionDirection = direction;
                return;
            }

            playerAnimationController?.SetEvasionDirection(direction);
        }

        private void ApplyQueuedEvasionDirection()
        {
            if (!queuedEvasionDirection.HasValue || !IsEvasionInProgress())
            {
                return;
            }

            playerAnimationController?.SetEvasionDirection(queuedEvasionDirection.Value);
            queuedEvasionDirection = null;
        }

        private void OnMovementDirectionChanged(Vector2 direction)
        {
            if (!queuedEvasionDirection.HasValue || direction.sqrMagnitude <= 0f)
            {
                return;
            }

            var currentDirection = playerAnimationController?.ResolveEvasionDirection() ?? Vector2.zero;
            if (currentDirection != Vector2.zero)
            {
                queuedEvasionDirection = currentDirection;
            }
        }

        private void SetAnimationRequests(
            bool lightAttackRequested,
            bool heavyAttackRequested,
            bool dodgeRequested,
            bool rollRequested)
        {
            if (animator == null)
            {
                return;
            }

            combatAnimator.SetRequests(lightAttackRequested, heavyAttackRequested, dodgeRequested, rollRequested);
        }

        private void RequestFullBodyAction(
            bool lightAttackRequested,
            bool heavyAttackRequested,
            bool dodgeRequested,
            bool rollRequested)
        {
            var isBufferedRequest = IsCombatActionAnimationActive();
            fullBodyActionRequested?.Invoke();
            SetAnimationRequests(lightAttackRequested, heavyAttackRequested, dodgeRequested, rollRequested);

            if (isBufferedRequest)
            {
                bufferedActionRequestExpiryTime = Time.time + movementConfig.BufferedCombatRequestLifetime;
            }
            else
            {
                ClearBufferedActionRequestExpiry();
            }
        }

        private void ClearBufferedActionRequestExpiry()
        {
            bufferedActionRequestExpiryTime = null;
        }

        private Stamina GetStamina()
        {
            return (Stamina)statsController.GetStat(StatType.Stamina);
        }

        private void SpendStamina(float amount)
        {
            if (amount > 0f)
            {
                statsController.AddValue(StatType.Stamina, -amount, StatChangeSource.Combat);
            }
        }
    }
}
