using System;
using System.Collections.Generic;

namespace Inventory
{
    /// <summary>
    /// Player-scoped record of active sources that prohibit holding a weapon in hand.
    /// </summary>
    public sealed class PlayerWeaponDrawingBlockState : IDisposable
    {
        private readonly HashSet<object> activeSources = new();

        public bool IsWeaponDrawingBlocked => activeSources.Count > 0;
        public event Action Changed;

        public void RegisterSource(object source)
        {
            if (source != null)
            {
                if (activeSources.Add(source) && activeSources.Count == 1)
                {
                    Changed?.Invoke();
                }
            }
        }

        public void UnregisterSource(object source)
        {
            if (source != null)
            {
                if (activeSources.Remove(source) && activeSources.Count == 0)
                {
                    Changed?.Invoke();
                }
            }
        }

        public void Dispose()
        {
            activeSources.Clear();
        }
    }
}
