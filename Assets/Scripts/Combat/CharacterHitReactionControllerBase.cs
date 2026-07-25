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
        private static readonly int HitFrontHash = Animator.StringToHash("HitFront");
        private static readonly int HitBackHash = Animator.StringToHash("HitBack");
        private static readonly int HitLeftHash = Animator.StringToHash("HitLeft");
        private static readonly int HitRightHash = Animator.StringToHash("HitRight");

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

        public void RegisterDamage(float damage, Vector3 hitPoint)
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

            StartReaction(hitPoint);
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

            RegisterDamage(message.FinalDamage, message.Point);
        }

        private void StartReaction(Vector3 hitPoint)
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
            TriggerHitReactionAnimation(hitPoint);
        }

        private void EndReaction()
        {
            rootMotionController?.SetRootMotionActive(this, false);
            actionState.SetActionBlocked(false);
            ownerDamageReceiver?.SetWeaponAttackSuppressed(false);
            OnReactionEnded();
        }

        private void TriggerHitReactionAnimation(Vector3 hitPoint)
        {
            if (animator == null)
            {
                return;
            }

            animator.ResetTrigger(HitFrontHash);
            animator.ResetTrigger(HitBackHash);
            animator.ResetTrigger(HitLeftHash);
            animator.ResetTrigger(HitRightHash);
            animator.SetTrigger(ResolveHitTrigger(hitPoint));
        }

        private int ResolveHitTrigger(Vector3 hitPoint)
        {
            var direction = hitPoint - ownerTransform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return HitFrontHash;
            }

            var localDirection = ownerTransform.InverseTransformDirection(direction.normalized);
            if (Mathf.Abs(localDirection.z) >= Mathf.Abs(localDirection.x))
            {
                return localDirection.z >= 0f ? HitFrontHash : HitBackHash;
            }

            return localDirection.x >= 0f ? HitRightHash : HitLeftHash;
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
