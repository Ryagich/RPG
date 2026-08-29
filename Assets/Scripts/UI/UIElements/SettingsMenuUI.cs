using Input;
using TMPro;
using TargetLock;
using UI.Configs;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
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
        private bool isLocaleSubscribed;
        private int targetLockModeLocalizationRevision;

        private const string LocalizationTable = "Tables";
        private const string TargetLockModeSoftLocalizationKey = "Bindings_Target_Lock_Mode_Soft";
        private const string TargetLockModeHardLocalizationKey = "Bindings_Target_Lock_Mode_Hard";
        private const string TargetLockModeSwitchLocalizationKey = "Bindings_Target_Lock_Mode_Switch";
        private const string TargetLockModeOffLocalizationKey = "Bindings_Target_Lock_Mode_Off";
        public void Initialize(InputActionAsset actions, UIConfig config, RectTransform page)
        {
            inputActions = actions;
            uiConfig = config;
            bindingsPage = page;

            BindingOverridesStorage.Load(inputActions);
            inputActions.Enable();
            ConfigureTargetLockSettings(bindingsPage);
            if (!isLocaleSubscribed)
            {
                LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
                isLocaleSubscribed = true;
            }
            EventSystem.current?.SetSelectedGameObject(null);
        }

        private void OnDestroy() => Dispose();

        public void Dispose()
        {
            if (isLocaleSubscribed)
            {
                LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
                isLocaleSubscribed = false;
            }

            bindingsPage = null;
        }

        private void OnSelectedLocaleChanged(Locale _)
        {
            var targetLockConfig = uiConfig != null ? uiConfig.TargetLockConfig : null;
            if (bindingsPage != null && targetLockConfig != null)
            {
                UpdateTargetLockSettings(bindingsPage, targetLockConfig);
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

        private void UpdateTargetLockSettings(RectTransform page, TargetLockConfig targetLockConfig)
        {
            var modeRow = FindDescendant(page, "TargetLock Mode");
            var modeText = FindDescendant(modeRow, "Text_Key")?.GetComponent<TMP_Text>();
            UpdateTargetLockModeText(modeText, targetLockConfig);

            var targetLockEnabled = targetLockConfig.ControlMode != TargetLockControlMode.Off;
            SetRowActive(page, "Button_TargetLock", targetLockEnabled && targetLockConfig.ControlMode == TargetLockControlMode.Switch);
            SetRowActive(page, "Button_TargetLockNext", targetLockEnabled);
            SetRowActive(page, "Button_TargetLockPrevious", targetLockEnabled);
        }

        private async void UpdateTargetLockModeText(TMP_Text modeText, TargetLockConfig targetLockConfig)
        {
            if (modeText == null || targetLockConfig == null)
            {
                return;
            }

            var controlMode = targetLockConfig.ControlMode;
            var localizationKey = controlMode switch
            {
                TargetLockControlMode.Soft => TargetLockModeSoftLocalizationKey,
                TargetLockControlMode.Hard => TargetLockModeHardLocalizationKey,
                TargetLockControlMode.Switch => TargetLockModeSwitchLocalizationKey,
                TargetLockControlMode.Off => TargetLockModeOffLocalizationKey,
                _ => string.Empty
            };
            if (string.IsNullOrEmpty(localizationKey))
            {
                return;
            }

            var revision = ++targetLockModeLocalizationRevision;
            var localizedText = await LocalizationSettings.StringDatabase
                .GetLocalizedStringAsync(LocalizationTable, localizationKey).Task;
            if (revision == targetLockModeLocalizationRevision
                && modeText != null
                && targetLockConfig.ControlMode == controlMode)
            {
                modeText.text = localizedText;
            }
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
