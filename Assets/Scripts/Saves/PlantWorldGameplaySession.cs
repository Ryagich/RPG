using VContainer.Unity;

namespace Saves
{
    /// <summary>Marks the lifetime of a real gameplay scene for project-level plant progression.</summary>
    public sealed class PlantWorldGameplaySession : IStartable, System.IDisposable
    {
        private readonly PlantWorldPersistenceService plantWorld;

        public PlantWorldGameplaySession(PlantWorldPersistenceService plantWorld)
        {
            this.plantWorld = plantWorld;
        }

        public void Start()
        {
            plantWorld.BeginGameplaySession();
        }

        public void Dispose()
        {
            plantWorld.EndGameplaySession();
        }
    }
}
