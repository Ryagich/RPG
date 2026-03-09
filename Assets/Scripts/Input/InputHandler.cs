using VContainer.Unity;

namespace Input
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class InputHandler : IStartable
    {
        private readonly InputConfig inputConfig;

        private InputHandler
            (
                InputConfig inputConfig
            )
        {
            this.inputConfig = inputConfig;
        }
        
        public void Start() { }
    }
}