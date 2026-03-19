using Gravity;
using UnityEngine;
using VContainer.Unity;

namespace Movement
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class PlayerGravity : IFixedTickable
    {
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
            if (controller.isGrounded)
            {
                verticalVelocity = 0f;
                return;
            }

            // v = v0 + g * dt
            verticalVelocity -= config.Gravity * Time.fixedDeltaTime;

            // Δs = v * dt
            var displacement = new Vector3(0f, verticalVelocity * Time.fixedDeltaTime, 0f);

            controller.Move(displacement);
        }
    }
}