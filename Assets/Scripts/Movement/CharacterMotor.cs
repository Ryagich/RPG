using UnityEngine;
using VContainer.Unity;

namespace Movement
{
    /// <summary>
    /// The single owner of a character's physical displacement. Sources provide intent only;
    /// this motor performs exactly one CharacterController.Move per frame.
    /// </summary>
    public sealed class CharacterMotor : ITickable
    {
        private readonly CharacterController controller;
        private readonly ICharacterPlanarMotionSource planarMotionSource;
        private readonly ICharacterVerticalMotionSource verticalMotionSource;
        private Vector3 pendingRootMotion;

        public CharacterMotor(CharacterController controller,
            ICharacterPlanarMotionSource planarMotionSource,
            ICharacterVerticalMotionSource verticalMotionSource)
        {
            this.controller = controller;
            this.planarMotionSource = planarMotionSource;
            this.verticalMotionSource = verticalMotionSource;
        }

        public bool IsGrounded => controller != null && controller.enabled
                                  && controller.gameObject.activeInHierarchy && controller.isGrounded;
        public CollisionFlags LastCollisionFlags { get; private set; }

        /// <summary>Queues world-space Animator delta for the next unified movement step.</summary>
        public void SubmitRootMotion(Vector3 worldDelta)
        {
            worldDelta.y = 0f;
            pendingRootMotion += worldDelta;
        }

        public void Tick()
        {
            if (controller == null || !controller.enabled || !controller.gameObject.activeInHierarchy)
            {
                pendingRootMotion = Vector3.zero;
                LastCollisionFlags = CollisionFlags.None;
                return;
            }

            var deltaTime = Time.deltaTime;
            var planarVelocity = planarMotionSource?.GetPlanarVelocity(deltaTime) ?? Vector3.zero;
            planarVelocity.y = 0f;
            var verticalVelocity = verticalMotionSource?.GetVerticalVelocity(deltaTime, controller.isGrounded) ?? 0f;
            var displacement = planarVelocity * deltaTime
                               + Vector3.up * (verticalVelocity * deltaTime)
                               + pendingRootMotion;
            pendingRootMotion = Vector3.zero;

            LastCollisionFlags = controller.Move(displacement);
            verticalMotionSource?.OnMotionApplied(LastCollisionFlags, controller.isGrounded);
            if (planarMotionSource is ICharacterMotorMotionObserver observer)
            {
                observer.OnMotorMoved();
            }
        }
    }

    /// <summary>Produces desired horizontal velocity; it never moves a CharacterController.</summary>
    public interface ICharacterPlanarMotionSource
    {
        Vector3 GetPlanarVelocity(float deltaTime);
    }

    /// <summary>Owns vertical-velocity integration while CharacterMotor owns displacement.</summary>
    public interface ICharacterVerticalMotionSource
    {
        float GetVerticalVelocity(float deltaTime, bool wasGrounded);
        void OnMotionApplied(CollisionFlags collisionFlags, bool isGrounded);
    }

    /// <summary>Receives the completed motor step without gaining access to Move.</summary>
    public interface ICharacterMotorMotionObserver
    {
        void OnMotorMoved();
    }
}
