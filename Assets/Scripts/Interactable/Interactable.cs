using System;
using UnityEngine;
using VContainer.Unity;

namespace Interactable
{
    public class Interactable : MonoBehaviour
    {
        public event Action<LifetimeScope> Interacted;
        public event Action<LifetimeScope> EndInteracted;
        public event Action<LifetimeScope> EndManualInteracted;
        public event Action<Interactable> Destroyed;

        public InteractionMode InteractionMode = InteractionMode.Automatic; 
        
        public void Interact(LifetimeScope scope)
        {
            Interacted?.Invoke(scope);
        }
        
        public void EndInteract(LifetimeScope scope)
        {
            EndInteracted?.Invoke(scope);
        }
        
        public void EndManualInteract(LifetimeScope scope)
        {
            EndManualInteracted?.Invoke(scope);
        }
        
        private void OnDestroy()
        {
            Destroyed?.Invoke(this);
        }
    }
    
    public enum InteractionMode
    {
        Automatic, // по входу в коллайдер, тиковое
        Manual     // по нажатию клавиши
    }
}
