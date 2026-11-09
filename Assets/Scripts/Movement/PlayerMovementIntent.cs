using System;
using UnityEngine;

namespace Movement
{
    /// <summary>Stores input intent independently from whether locomotion may execute it.</summary>
    public sealed class PlayerMovementIntent
    {
        public event Action<Vector2> DirectionChanged;

        public Vector2 Direction { get; private set; }
        public bool IsRunPressed { get; private set; }
        public bool IsSprintPressed { get; private set; }

        public void Set(Vector2 direction, bool isRunPressed, bool isSprintPressed)
        {
            var hasDirectionChanged = Direction != direction;
            Direction = direction;
            IsRunPressed = isRunPressed;
            IsSprintPressed = isSprintPressed;

            if (hasDirectionChanged)
            {
                DirectionChanged?.Invoke(direction);
            }
        }
    }
}
