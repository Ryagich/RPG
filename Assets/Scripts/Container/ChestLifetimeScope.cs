using Interactable;
using VContainer;
using VContainer.Unity;

namespace Container
{
    public class ChestLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            var interactable = gameObject.AddComponent<Interactable.Interactable>();
            interactable.InteractionMode = InteractionMode.Manual;
            
            builder.RegisterInstance(interactable);
        }
    }
}