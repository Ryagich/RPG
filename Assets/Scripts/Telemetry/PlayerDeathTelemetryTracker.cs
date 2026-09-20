using System;
using MessagePipe;
using Messages;
using VContainer.Unity;

namespace Telemetry
{
    /// <summary>Reports the authoritative player-death message without affecting death handling.</summary>
    public sealed class PlayerDeathTelemetryTracker : IStartable, IDisposable
    {
        private readonly ISubscriber<PlayerDiedMessage> playerDiedSubscriber;
        private readonly IGameTelemetry telemetry;
        private IDisposable subscription;

        public PlayerDeathTelemetryTracker(
            ISubscriber<PlayerDiedMessage> playerDiedSubscriber,
            IGameTelemetry telemetry)
        {
            this.playerDiedSubscriber = playerDiedSubscriber;
            this.telemetry = telemetry;
        }

        public void Start()
        {
            subscription = playerDiedSubscriber.Subscribe(_ => telemetry.Track(GameTelemetryEvents.PlayerDied));
        }

        public void Dispose()
        {
            subscription?.Dispose();
        }
    }
}
