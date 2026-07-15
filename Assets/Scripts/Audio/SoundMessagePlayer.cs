using MessagePipe;
using Messages;
using VContainer.Unity;

namespace GameAudio
{
    /// <summary>Bridges the existing gameplay MessagePipe sound messages to the pooled service.</summary>
    public sealed class SoundMessagePlayer : IStartable, System.IDisposable
    {
        private readonly ISubscriber<PlaySoundMessage> subscriber;
        private readonly IAudioService audioService;
        private System.IDisposable subscription;

        public SoundMessagePlayer(ISubscriber<PlaySoundMessage> subscriber, IAudioService audioService)
        {
            this.subscriber = subscriber;
            this.audioService = audioService;
        }

        public void Start() => subscription = subscriber.Subscribe(message => audioService.Play(message.SoundSettings, message.Position, message.Parent));
        public void Dispose() => subscription?.Dispose();
    }
}
