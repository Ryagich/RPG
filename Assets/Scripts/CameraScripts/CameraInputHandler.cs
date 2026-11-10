using Input;
using MessagePipe;
using Messages;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace CameraScripts
{
    /// <summary>
    /// Owns only the Input System bindings for the gameplay camera and translates them into
    /// domain-neutral input messages. Camera behaviour remains in <see cref="CameraMovement"/>.
    /// </summary>
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class CameraInputHandler : IStartable, System.IDisposable
    {
        private readonly InputConfig inputConfig;
        private readonly IPublisher<CameraLookInputMessage> lookInputPublisher;
        private readonly IPublisher<CameraZoomInputMessage> zoomInputPublisher;

        private InputAction lookAction;
        private InputAction zoomInAction;
        private InputAction zoomOutAction;

        public CameraInputHandler(
            InputConfig inputConfig,
            IPublisher<CameraLookInputMessage> lookInputPublisher,
            IPublisher<CameraZoomInputMessage> zoomInputPublisher)
        {
            this.inputConfig = inputConfig;
            this.lookInputPublisher = lookInputPublisher;
            this.zoomInputPublisher = zoomInputPublisher;
        }

        public void Start()
        {
            lookAction = inputConfig.CameraLook?.action;
            zoomInAction = inputConfig.CameraZoomIn?.action;
            zoomOutAction = inputConfig.CameraZoomOut?.action;

            Subscribe(lookAction, OnLook, "CameraLook");
            Subscribe(zoomInAction, OnZoomIn, "CameraZoomIn");
            Subscribe(zoomOutAction, OnZoomOut, "CameraZoomOut");
        }

        public void Dispose()
        {
            Unsubscribe(lookAction, OnLook);
            Unsubscribe(zoomInAction, OnZoomIn);
            Unsubscribe(zoomOutAction, OnZoomOut);
        }

        private void OnLook(InputAction.CallbackContext context)
        {
            var delta = context.ReadValue<Vector2>();
            if (delta.sqrMagnitude > Mathf.Epsilon)
            {
                lookInputPublisher.Publish(new CameraLookInputMessage(delta));
            }
        }

        private void OnZoomIn(InputAction.CallbackContext _) =>
            zoomInputPublisher.Publish(new CameraZoomInputMessage(CameraZoomDirection.In));

        private void OnZoomOut(InputAction.CallbackContext _) =>
            zoomInputPublisher.Publish(new CameraZoomInputMessage(CameraZoomDirection.Out));

        private static void Subscribe(
            InputAction action,
            System.Action<InputAction.CallbackContext> callback,
            string actionName)
        {
            if (action == null)
            {
                Debug.LogError($"Camera input action {actionName} is not configured.");
                return;
            }

            action.performed += callback;
            action.Enable();
        }

        private static void Unsubscribe(
            InputAction action,
            System.Action<InputAction.CallbackContext> callback)
        {
            if (action == null)
            {
                return;
            }

            action.performed -= callback;
            action.Disable();
        }
    }
}
