using VContainer.Unity;

namespace Interactable
{
    public interface IInteractableAvailability
    {
        bool IsInteractableAvailable(LifetimeScope interactorScope);
    }
}
