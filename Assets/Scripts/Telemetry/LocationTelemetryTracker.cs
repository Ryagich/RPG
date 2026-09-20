using System.Collections.Generic;
using Container.Game;
using Locations;
using VContainer.Unity;

namespace Telemetry
{
    /// <summary>
    /// Reports the location selected by the location service only after the playable world has
    /// initialized. It observes selection and does not affect scene or transition lifecycles.
    /// </summary>
    public sealed class LocationTelemetryTracker : IStartable
    {
        private readonly GameWorldBootstrapper worldBootstrapper;
        private readonly LocationTransitionService locationTransitions;
        private readonly IGameTelemetry telemetry;

        public LocationTelemetryTracker(
            GameWorldBootstrapper worldBootstrapper,
            LocationTransitionService locationTransitions,
            IGameTelemetry telemetry)
        {
            this.worldBootstrapper = worldBootstrapper;
            this.locationTransitions = locationTransitions;
            this.telemetry = telemetry;
        }

        public async void Start()
        {
            await worldBootstrapper.WorldInitialized;

            string locationId = locationTransitions.CurrentLocation?.Id;
            if (!string.IsNullOrWhiteSpace(locationId))
            {
                telemetry.Track(GameTelemetryEvents.LocationEntered, new Dictionary<string, string>
                {
                    ["location_id"] = locationId
                });
            }
        }
    }
}
