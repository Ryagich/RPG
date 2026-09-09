using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace UI.UIElements
{
    public sealed class SwitchMenuHolder : MonoBehaviour
    {
        [field: SerializeField] public Button YesButton { get; private set; }
        [field: SerializeField] public Button NoButton { get; private set; }
        [field: SerializeField] public TMP_Text TitleText { get; private set; }
        [field: SerializeField] public LocalizeStringEvent TitleLocalization { get; private set; }

        public event Action CancelRequested;
        private LocalizedString configuredTitle;

        public void SetTitle(LocalizedString value)
        {
            if (TitleText == null)
            {
                Debug.LogError("Switch Menu Holder requires a title text reference.", this);
                return;
            }

            if (TitleLocalization != null)
            {
                TitleLocalization.enabled = false;
            }

            if (configuredTitle != null)
            {
                configuredTitle.StringChanged -= OnTitleChanged;
            }

            configuredTitle = value;
            if (configuredTitle == null)
            {
                TitleText.text = string.Empty;
                return;
            }

            configuredTitle.StringChanged += OnTitleChanged;
            OnTitleChanged(configuredTitle.GetLocalizedString());
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelRequested?.Invoke();
            }
        }

        private void OnDestroy()
        {
            if (configuredTitle != null)
            {
                configuredTitle.StringChanged -= OnTitleChanged;
                configuredTitle = null;
            }
        }

        private void OnTitleChanged(string value)
        {
            if (TitleText != null)
            {
                TitleText.text = value;
            }
        }
    }
}
