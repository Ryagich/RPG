using System.Collections.Generic;

namespace Telemetry
{
    /// <summary>Infrastructure boundary for reporting anonymous gameplay facts.</summary>
    public interface IGameTelemetry
    {
        void Track(string eventId, IReadOnlyDictionary<string, string> parameters = null);
    }
}
