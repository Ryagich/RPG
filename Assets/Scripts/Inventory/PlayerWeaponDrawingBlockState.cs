using System;
using System.Collections.Generic;

namespace Inventory
{
    /// <summary>
    /// Player-scoped record of weapon-drawing-blocked zones currently containing the player.
    /// </summary>
    public sealed class PlayerWeaponDrawingBlockState : IDisposable
    {
        private readonly HashSet<WeaponDrawingBlockedZone> activeZones = new();

        public bool IsWeaponDrawingBlocked => activeZones.Count > 0;
        public event Action Changed;

        public void RegisterZone(WeaponDrawingBlockedZone zone)
        {
            if (zone != null)
            {
                if (activeZones.Add(zone) && activeZones.Count == 1)
                {
                    Changed?.Invoke();
                }
            }
        }

        public void UnregisterZone(WeaponDrawingBlockedZone zone)
        {
            if (zone != null)
            {
                if (activeZones.Remove(zone) && activeZones.Count == 0)
                {
                    Changed?.Invoke();
                }
            }
        }

        public void Dispose()
        {
            activeZones.Clear();
        }
    }
}
