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
        private readonly PlayerWeaponCombatAnimator combatAnimator;
        private readonly CharacterRootMotionController rootMotionController;
        private readonly GameModesController gameModesController;
        private readonly PlayerMovement playerMovement;
        private readonly PlayerMovementConfig playerMovementConfig;
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
        private bool isCombatActionLocked;

        public PlayerWeaponCombatActionController(
            Animator animator,
            PlayerWeaponCombatAnimator combatAnimator,
            CharacterRootMotionController rootMotionController,
            GameModesController gameModesController,
            PlayerMovement playerMovement,
            PlayerMovementConfig playerMovementConfig,
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
            this.combatAnimator = combatAnimator;
            this.rootMotionController = rootMotionController;
            this.gameModesController = gameModesController;
            this.playerMovement = playerMovement;
            this.playerMovementConfig = playerMovementConfig;
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

        public bool IsCombatActionLocked => isCombatActionLocked;
        public bool IsHitAttackInProgress => isHitAttackInProgress;
        public bool IsRollAnimationActive => IsRollInProgress();
        public bool IsAttackBlockingWeaponChanges => isHitAttackInProgress || IsCombatActionAnimationActive();
        public bool IsAttackRootMotionStateActive => combatAnimator.IsFullBodyActionActive();

        public void Start()
        {
            ResetAnimationRequests();
            playerAnimationController?.ReleaseEvasionDirection();
            ownerDamageReceiver?.SetWeaponDamageBlocked(false);
            UpdateRootMotionAvailability();
        }

        public void Tick()
        {
            UpdateRootMotionAvailability();
        }

        public void Dispose()
        {
            playerAnimationController?.ReleaseEvasionDirection();
            ownerDamageReceiver?.SetWeaponDamageBlocked(false);
            damageWindow?.End();
            UpdateRootMotionAvailability(forceDisable: true);
        }

        public void TryTriggerAttack(MouseButtonType button)
        {
            if (animator == null
             || gameModesController.GameMode != GameMode.Game
             || (actionState.IsActionBlocked && !isHitAttackInProgress))
            {
                return;
            }

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
                if (!isHitAttackInProgress)
                {
                    playerAnimationController?.SetLocomotionLocked(false);
                }

                return;
            }

            Cancel();
        }

        public void AttackStartedFromAnimationEvent()
        {
            isHitAttackInProgress = true;
            isCombatActionLocked = true;
            UpdateRootMotionAvailability();
            playerMovement?.ChangeState(false);
            playerAnimationController?.SetLocomotionLocked(true);
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

        public void LockMovementFromAnimationEvent()
        {
            isCombatActionLocked = true;
            playerMovement?.ChangeState(false);
            playerAnimationController?.SetLocomotionLocked(true);
        }

        public void UnlockMovementFromAnimationEvent()
        {
            var completedEvasion = IsEvasionInProgress();
            var completedRoll = IsRollInProgress();
            isCombatActionLocked = false;
            playerAnimationController?.ReleaseEvasionDirection();

            if (gameModesController.GameMode == GameMode.Game)
            {
                playerMovement?.ChangeState(true);
            }

            playerAnimationController?.SetLocomotionLocked(false);
            if (completedEvasion)
            {
                evasionCompletedPublisher.Publish(new PlayerEvasionCompletedMessage(completedRoll));
            }
        }

        public void AttackFinishedFromAnimationEvent()
        {
            isHitAttackInProgress = false;
            isCombatActionLocked = false;
            UpdateRootMotionAvailability();

            if (gameModesController.GameMode == GameMode.Game)
            {
                playerMovement?.ChangeState(true);
            }

            playerAnimationController?.SetLocomotionLocked(false);
        }

        public void ResetAnimationRequests()
        {
            SetAnimationRequests(lightAttackRequested: false, heavyAttackRequested: false, dodgeRequested: false, rollRequested: false);
        }

        public void Cancel(bool restoreMovement = true)
        {
            playerAnimationController?.ReleaseEvasionDirection();
            DisableDamageImmunityFromAnimationEvent();
            isCombatActionLocked = false;
            damageWindow?.End();

            if (isHitAttackInProgress)
            {
                isHitAttackInProgress = false;
                UpdateRootMotionAvailability();
                if (restoreMovement && gameModesController.GameMode == GameMode.Game)
                {
                    playerMovement?.ChangeState(true);
                }

                if (restoreMovement)
                {
                    playerAnimationController?.SetLocomotionLocked(false);
                }
            }

            ResetAnimationRequests();
        }

        public void InterruptByHitReaction()
        {
            Cancel(restoreMovement: false);
            UpdateRootMotionAvailability(forceDisable: true);
        }

        public void UpdateRootMotionAvailability(bool forceDisable = false)
        {
            if (animator == null)
            {
                return;
            }

            var isRootMotionActive = !forceDisable && IsAttackRootMotionStateActive;
            var positionMultiplier = isRootMotionActive ? GetEvasionRootMotionMultiplier() : 1f;
            rootMotionController?.SetRootMotionActive(this, isRootMotionActive, positionMultiplier);
        }

        private bool CanRequestEvasion()
        {
            return animator != null
                   && gameModesController.GameMode == GameMode.Game
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

        private float GetEvasionRootMotionMultiplier()
        {
            if (combatAnimator.IsRollActive())
            {
                return playerMovementConfig.RollRootMotionMultiplier;
            }

            return combatAnimator.IsDodgeActive()
                ? playerMovementConfig.DodgeRootMotionMultiplier
                : 1f;
        }

        private void CaptureEvasionDirection()
        {
            playerAnimationController?.CaptureEvasionDirection();
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
            fullBodyActionRequested?.Invoke();
            SetAnimationRequests(lightAttackRequested, heavyAttackRequested, dodgeRequested, rollRequested);
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
