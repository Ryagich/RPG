using VContainer.Unity;

namespace Interactable
{
    public interface IInteractableAvailability
    {
        bool IsInteractableAvailable(LifetimeScope interactorScope);
    }

    /// <summary>
    /// Lets one world zone select manual or automatic activation for its current interactor.
    /// The zone still owns one <see cref="Interactable"/> and one availability decision.
    /// </summary>
    public interface IInteractableModeProvider
    {
        InteractionMode GetInteractionMode(LifetimeScope interactorScope);
    }
}
