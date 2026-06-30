using Gravity;
using UnityEngine;
using VContainer.Unity;

namespace Movement
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class PlayerGravity : IFixedTickable
    {
        private const float GroundedStickVelocity = -2f;

        private readonly CharacterController controller;
        private readonly GravityConfig config;

        private float verticalVelocity;

        public PlayerGravity
            (
                CharacterController controller,
                GravityConfig config
            )
        {
            this.controller = controller;
            this.config = config;
        }

        public void FixedTick()
        {
            if (controller == null || !controller.enabled)
            {
                return;
            }

            if (controller.isGrounded)
            {
                // CharacterController updates grounded state from movement resolution.
                // Keep applying a small downward motion so the player starts falling as soon as support disappears.
                verticalVelocity = GroundedStickVelocity;
            }
            else
            {
                // v = v0 + g * dt
                verticalVelocity -= config.Gravity * Time.fixedDeltaTime;
            }

            // Δs = v * dt
            var displacement = new Vector3(0f, verticalVelocity * Time.fixedDeltaTime, 0f);

            controller.Move(displacement);
        }
    }
}
