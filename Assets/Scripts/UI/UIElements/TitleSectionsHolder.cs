using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI.UIElements
{
    public enum SettingsSection
    {
        Bindings,
        Sounds,
        Gameplay
    }

    public sealed class TitleSectionsHolder : MonoBehaviour
    {
        [field: SerializeField] public Button BindingsButton { get; private set; }
        [field: SerializeField] public Button SoundsButton { get; private set; }
        [field: SerializeField] public Button GameplayButton { get; private set; }

        private bool isInitialized;

        public event Action<SettingsSection> SectionRequested;
        public event Action CloseRequested;

        public void ConfigureButtons(Button bindingsButton, Button soundsButton, Button gameplayButton)
        {
            BindingsButton = bindingsButton;
            SoundsButton = soundsButton;
            GameplayButton = gameplayButton;
        }

        public void Initialize(SettingsSection currentSection)
        {
            RemoveListeners();
            AddListeners();
            SetCurrentSection(currentSection);
            isInitialized = true;
        }

        public void Dispose()
        {
            RemoveListeners();
            isInitialized = false;
        }

        private void Update()
        {
            if (!isInitialized || Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            EventSystem.current?.SetSelectedGameObject(null);
            CloseRequested?.Invoke();
        }

        private void OnDestroy() => Dispose();

        private void AddListeners()
        {
            BindingsButton?.onClick.AddListener(RequestBindings);
            SoundsButton?.onClick.AddListener(RequestSounds);
            GameplayButton?.onClick.AddListener(RequestGameplay);
        }

        private void RemoveListeners()
        {
            BindingsButton?.onClick.RemoveListener(RequestBindings);
            SoundsButton?.onClick.RemoveListener(RequestSounds);
            GameplayButton?.onClick.RemoveListener(RequestGameplay);
        }

        private void SetCurrentSection(SettingsSection currentSection)
        {
            SetButtonInteractable(BindingsButton, currentSection != SettingsSection.Bindings);
            SetButtonInteractable(SoundsButton, currentSection != SettingsSection.Sounds);
            SetButtonInteractable(GameplayButton, currentSection != SettingsSection.Gameplay);
        }

        private static void SetButtonInteractable(Button button, bool isInteractable)
        {
            if (button != null)
            {
                button.interactable = isInteractable;
            }
        }

        private void RequestBindings() => SectionRequested?.Invoke(SettingsSection.Bindings);
        private void RequestSounds() => SectionRequested?.Invoke(SettingsSection.Sounds);
        private void RequestGameplay() => SectionRequested?.Invoke(SettingsSection.Gameplay);
    }
}
