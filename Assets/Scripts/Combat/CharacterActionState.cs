using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    public sealed class CharacterActionState
    {
        private readonly HashSet<object> blockSources = new();
        private bool isDirectlyBlocked;

        public bool IsActionBlocked => isDirectlyBlocked || blockSources.Count > 0;

        public void SetActionBlocked(bool isBlocked)
        {
            isDirectlyBlocked = isBlocked;
        }

        public void SetActionBlocked(object source, bool isBlocked)
        {
            if (source == null)
            {
                return;
            }

            if (isBlocked)
            {
                blockSources.Add(source);
                return;
            }

            blockSources.Remove(source);
        }
    }

    /// <summary>
    /// Converts a local evasion direction to one of the four cardinal positions available in
    /// the Dodge and Roll blend trees.
    /// </summary>
    public static class EvasionAnimationDirectionUtility
    {
        private const float DirectionThreshold = 0.0001f;

        public static Vector2 SnapToCardinal(Vector3 localDirection)
        {
            var absoluteX = Mathf.Abs(localDirection.x);
            var absoluteZ = Mathf.Abs(localDirection.z);

            if (absoluteX <= DirectionThreshold && absoluteZ <= DirectionThreshold)
            {
                return Vector2.zero;
            }

            if (absoluteX > absoluteZ)
            {
                return new Vector2(Mathf.Sign(localDirection.x), 0f);
            }

            return new Vector2(0f, Mathf.Sign(localDirection.z));
        }
    }
}
