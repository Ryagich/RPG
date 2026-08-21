using System.Collections.Generic;
using System;
using MessagePipe;
using Messages;
using UnityEngine;
using VContainer.Unity;

namespace Combat
{
    public abstract class CharacterHitReactionControllerBase : ICharacterHitReactionController, IStartable, ITickable, IDisposable
    {
        private static readonly int HitHash = Animator.StringToHash("Hit");
        private static readonly int HitDirectionXHash = Animator.StringToHash("HitDirectionX");
        private static readonly int HitDirectionYHash = Animator.StringToHash("HitDirectionY");

        private readonly Queue<DamageSample> damageSamples = new();
        private readonly HitReactionConfig config;
        private readonly CharacterActionState actionState;
        private readonly CharacterRootMotionController rootMotionController;
        private readonly CharacterDamageReceiver ownerDamageReceiver;
        private readonly Transform ownerTransform;
        private readonly Animator animator;
        private readonly ISubscriber<CharacterDamagedMessage> damagedSubscriber;

        private float reactionTimer;
        private float cooldownTimer;
        private IDisposable damageSubscription;

        protected CharacterHitReactionControllerBase(
            HitReactionConfig config,
            CharacterActionState actionState,
            CharacterRootMotionController rootMotionController,
            CharacterDamageReceiver ownerDamageReceiver,
            Transform ownerTransform,
            Animator animator,
            ISubscriber<CharacterDamagedMessage> damagedSubscriber)
        {
            this.config = config;
            this.actionState = actionState;
            this.rootMotionController = rootMotionController;
            this.ownerDamageReceiver = ownerDamageReceiver;
            this.ownerTransform = ownerTransform;
            this.animator = animator;
            this.damagedSubscriber = damagedSubscriber;
        }

        public bool IsReacting => reactionTimer > 0f;

        public virtual void Start()
        {
            actionState.SetActionBlocked(false);
            damageSubscription = damagedSubscriber.Subscribe(OnCharacterDamaged);
        }

        public void Dispose()
        {
            ownerDamageReceiver?.SetWeaponAttackSuppressed(false);
            damageSubscription?.Dispose();
        }

        public void CancelReaction()
        {
            if (!IsReacting)
            {
                return;
            }

            reactionTimer = 0f;
            cooldownTimer = 0f;
            damageSamples.Clear();
            EndReaction();
        }

        public void Tick()
        {
            var deltaTime = Time.deltaTime;

            if (cooldownTimer > 0f)
            {
                cooldownTimer = Mathf.Max(0f, cooldownTimer - deltaTime);
            }

            if (reactionTimer > 0f)
            {
                reactionTimer = Mathf.Max(0f, reactionTimer - deltaTime);
                if (reactionTimer <= 0f)
                {
                    EndReaction();
                }
            }

            PruneOldDamageSamples(Time.time);
            OnTick(deltaTime);
        }

        public void RegisterDamage(float damage, Vector3 hitPoint, Transform attackerTransform = null)
        {
            if (damage <= 0f || ownerTransform == null || config == null)
            {
                return;
            }

            var now = Time.time;
            damageSamples.Enqueue(new DamageSample(now, damage));
            PruneOldDamageSamples(now);

            if (IsReacting || cooldownTimer > 0f || CalculateWindowDamage() < config.DamageReactionThreshold)
            {
                return;
            }

            StartReaction(hitPoint, attackerTransform);
        }

        protected virtual void OnTick(float deltaTime) { }

        protected abstract void OnReactionStarted();
        protected abstract void OnReactionEnded();

        private void OnCharacterDamaged(CharacterDamagedMessage message)
        {
            if (message.CharacterTransform != ownerTransform)
            {
                return;
            }

            RegisterDamage(message.FinalDamage, message.Point, message.Attacker?.OwnerTransform);
        }

        private void StartReaction(Vector3 hitPoint, Transform attackerTransform)
        {
            damageSamples.Clear();
            reactionTimer = config.ReactionCooldown;
            cooldownTimer = config.ReactionCooldown;
            actionState.SetActionBlocked(true);
            // Stop outgoing weapon damage before interrupting animation flow. This also protects
            // against a delayed BeginDamageWindow event from the attack that was interrupted.
            ownerDamageReceiver?.SetWeaponAttackSuppressed(true);
            rootMotionController?.SetRootMotionActive(this, true);

            OnReactionStarted();
            TriggerHitReactionAnimation(hitPoint, attackerTransform);
        }

        private void EndReaction()
        {
            reactionTimer = 0f;
            rootMotionController?.SetRootMotionActive(this, false);
            actionState.SetActionBlocked(false);
            ownerDamageReceiver?.SetWeaponAttackSuppressed(false);
            OnReactionEnded();
        }

        private void TriggerHitReactionAnimation(Vector3 hitPoint, Transform attackerTransform)
        {
            if (animator == null)
            {
                return;
            }

            var localReactionDirection = ResolveLocalReactionDirection(hitPoint, attackerTransform);
            animator.SetFloat(HitDirectionXHash, localReactionDirection.x);
            animator.SetFloat(HitDirectionYHash, localReactionDirection.z);
            LogHitReactionDirection(hitPoint, attackerTransform, localReactionDirection);
            animator.ResetTrigger(HitHash);
            animator.SetTrigger(HitHash);
        }

        private Vector3 ResolveLocalReactionDirection(Vector3 hitPoint, Transform attackerTransform)
        {
            // HitDirectionX/Y are consumed by the Animator's Blend Tree, so their coordinate
            // system must match the Animator transform. The gameplay root can have a different
            // orientation from the visual hierarchy (as it does for the player prefab).
            var animationTransform = animator != null ? animator.transform : ownerTransform;
            var attackerDirection = attackerTransform != null
                ? GetPlanarDirection(attackerTransform.position - animationTransform.position)
                : Vector3.zero;

            // Weapon hits always provide their attacker. The impact point keeps non-weapon
            // callers directional too, without selecting a fixed reaction animation.
            var incomingDirection = attackerDirection.sqrMagnitude > 0.0001f
                ? attackerDirection
                : GetPlanarDirection(hitPoint - animationTransform.position);
            if (incomingDirection.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            var localAttackerDirection = animationTransform.InverseTransformDirection(incomingDirection.normalized);

            // Blend Tree coordinates describe the direction of the reaction, not the attacker:
            // attacker behind (local Z < 0) therefore yields HitDirectionY = 1,
            // while attacker in front (local Z > 0) yields HitDirectionY = -1.
            return -new Vector3(localAttackerDirection.x, 0f, localAttackerDirection.z);
        }

        private void LogHitReactionDirection(
            Vector3 hitPoint,
            Transform attackerTransform,
            Vector3 localReactionDirection)
        {
            if (attackerTransform == null)
            {
                Debug.LogWarning(
                    $"[HitReaction] '{ownerTransform.name}': attacker is unavailable; " +
                    $"reaction was derived from hit point={hitPoint}. " +
                    $"Blend Tree parameters=({localReactionDirection.x:F2}, {localReactionDirection.z:F2}).",
                    ownerTransform);
                return;
            }

            var animationTransform = animator != null ? animator.transform : ownerTransform;
            var attackerWorldOffset = GetPlanarDirection(attackerTransform.position - animationTransform.position);
            var attackerLocalDirection = animationTransform.InverseTransformDirection(attackerWorldOffset);
            Debug.Log(
                $"[HitReaction] target='{ownerTransform.name}', attacker='{attackerTransform.name}'; " +
                $"attacker world position={attackerTransform.position}, animation transform='{animationTransform.name}', " +
                $"animation world position={animationTransform.position}; " +
                $"attacker direction in animation local space=({attackerLocalDirection.x:F2}, {attackerLocalDirection.z:F2}); " +
                $"reaction direction (opposite attacker)=({localReactionDirection.x:F2}, {localReactionDirection.z:F2}); " +
                $"HitDirectionX={localReactionDirection.x:F2}, HitDirectionY={localReactionDirection.z:F2}.",
                ownerTransform);
        }

        private static Vector3 GetPlanarDirection(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude <= 0.0001f ? Vector3.zero : direction.normalized;
        }

        private void PruneOldDamageSamples(float now)
        {
            var window = Mathf.Max(0.01f, config.DamageReactionWindow);
            while (damageSamples.Count > 0 && now - damageSamples.Peek().Time > window)
            {
                damageSamples.Dequeue();
            }
        }

        private float CalculateWindowDamage()
        {
            var totalDamage = 0f;
            foreach (var sample in damageSamples)
            {
                totalDamage += sample.Damage;
            }

            return totalDamage;
        }

        private readonly struct DamageSample
        {
            public readonly float Time;
            public readonly float Damage;

            public DamageSample(float time, float damage)
            {
                Time = time;
                Damage = damage;
            }
        }
    }
}
