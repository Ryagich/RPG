using System.Collections.Generic;
using YG;

namespace Telemetry
{
    /// <summary>Delivers telemetry through the configured PluginYG2 Yandex Metrica module.</summary>
    public sealed class YandexMetricaTelemetry : IGameTelemetry
    {
        public void Track(string eventId, IReadOnlyDictionary<string, string> parameters = null)
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                return;
            }

            if (parameters == null || parameters.Count == 0)
            {
                YG2.MetricaSend(eventId);
                return;
            }

            var payload = new Dictionary<string, string>(parameters.Count);
            foreach (KeyValuePair<string, string> parameter in parameters)
            {
                payload[parameter.Key] = parameter.Value;
            }

            YG2.MetricaSend(eventId, payload);
        }
    }
}
