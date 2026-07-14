using System;
using Input;
using TMPro;
using TargetLock;
using UI.Configs;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI.UIElements
{
    /// <summary>
    /// Behaviour for the existing Bindings page when it is shown from the main menu.
    /// It owns no visual hierarchy: the Bindings prefab is the settings UI.
    /// </summary>
    public sealed class SettingsMenuUI : MonoBehaviour
    {
        private InputActionAsset inputActions;
        private UIConfig uiConfig;
        private RectTransform bindingsPage;
        public void Initialize(InputActionAsset actions, UIConfig config, RectTransform page)
        {
            inputActions = actions;
            uiConfig = config;
            bindingsPage = page;

            BindingOverridesStorage.Load(inputActions);
            inputActions.Enable();
            DisableExternalLocalization(bindingsPage);
            ConfigureTargetLockSettings(bindingsPage);
            EventSystem.current?.SetSelectedGameObject(null);
        }

        private void OnDestroy() => Dispose();

        public void Dispose()
        {
            bindingsPage = null;
        }

        private static void DisableExternalLocalization(RectTransform page)
        {
            foreach (var component in page.GetComponentsInChildren<Component>(true))
            {
                var componentNamespace = component.GetType().Namespace;
                if (componentNamespace == null || !componentNamespace.StartsWith("UnityEngine.Localization", StringComparison.Ordinal))
                {
                    continue;
                }

                if (component is Behaviour behaviour)
                {
                    behaviour.enabled = false;
                }

                Destroy(component);
            }
        }

        private void ConfigureTargetLockSettings(RectTransform page)
        {
            var targetLockConfig = uiConfig != null ? uiConfig.TargetLockConfig : null;
            if (targetLockConfig == null)
            {
                Debug.LogError("TargetLockConfig is not assigned in UIConfig.");
                return;
            }

            var modeRow = FindDescendant(page, "TargetLock Mode");
            if (modeRow == null)
            {
                Debug.LogError("TargetLock Mode row is missing from the Bindings prefab.");
                return;
            }

            var modeButton = modeRow.GetComponent<Button>();
            if (modeButton == null)
            {
                Debug.LogError("TargetLock Mode row needs a Button component.");
                return;
            }

            modeRow.GetComponent<InputRebinder>()?.DisableRebinding();
            modeButton.onClick.RemoveAllListeners();
            modeButton.onClick.AddListener(() =>
            {
                targetLockConfig.CycleControlMode();
                UpdateTargetLockSettings(page, targetLockConfig);
            });

            UpdateTargetLockSettings(page, targetLockConfig);
        }

        private static void UpdateTargetLockSettings(RectTransform page, TargetLockConfig targetLockConfig)
        {
            var modeRow = FindDescendant(page, "TargetLock Mode");
            var modeText = FindDescendant(modeRow, "Text_Key")?.GetComponent<TMP_Text>();
            if (modeText != null)
            {
                modeText.text = targetLockConfig.ControlMode.ToString();
            }

            var targetLockEnabled = targetLockConfig.ControlMode != TargetLockControlMode.Off;
            SetRowActive(page, "Button_TargetLock", targetLockEnabled && targetLockConfig.ControlMode == TargetLockControlMode.Switch);
            SetRowActive(page, "Button_TargetLockNext", targetLockEnabled);
            SetRowActive(page, "Button_TargetLockPrevious", targetLockEnabled);
        }

        private static void SetRowActive(RectTransform page, string rowName, bool isActive)
        {
            var row = FindDescendant(page, rowName);
            if (row != null)
            {
                row.gameObject.SetActive(isActive);
            }
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == objectName)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
