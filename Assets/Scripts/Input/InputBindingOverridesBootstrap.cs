using VContainer.Unity;

namespace Input
{
    // Loads the player's bindings before scene-specific input handlers subscribe to actions.
    public sealed class InputBindingOverridesBootstrap : IStartable
    {
        private readonly InputConfig inputConfig;

        public InputBindingOverridesBootstrap(InputConfig inputConfig)
        {
            this.inputConfig = inputConfig;
        }

        public void Start()
        {
            BindingOverridesStorage.Load(inputConfig?.Movement?.action?.actionMap?.asset);
        }
    }
}
