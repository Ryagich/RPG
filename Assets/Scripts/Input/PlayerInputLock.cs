using System;
using System.Collections.Generic;

namespace Input
{
    /// <summary>
    /// Coordinates temporary locks of all player-originated input. A source owns only its own
    /// lock, so overlapping transitions cannot restore input prematurely.
    /// </summary>
    public sealed class PlayerInputLock
    {
        private readonly HashSet<object> sources = new();

        public bool IsLocked => sources.Count > 0;
        public event Action<bool> Changed;

        public void SetLocked(object source, bool isLocked)
        {
            if (source == null)
            {
                return;
            }

            var wasLocked = IsLocked;
            if (isLocked)
            {
                sources.Add(source);
            }
            else
            {
                sources.Remove(source);
            }

            if (wasLocked != IsLocked)
            {
                Changed?.Invoke(IsLocked);
            }
        }
    }
}
