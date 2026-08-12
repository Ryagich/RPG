using System.Collections.Generic;
using System.Linq;
using Input;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class BindingsController : MonoBehaviour
{
    [SerializeField] private ButtonSoundPlayer _soundPlayer;
    [SerializeField] private Transform _settingsCanvas;

    [Header("Colors For Bindings Button")]
    [SerializeField] private Color _disableColorActiveButton;
    [SerializeField] private Color _defDisableColor;

    [Header("Colors For Settings Button")]
    [SerializeField] private Color _highlightColor;
    [SerializeField] private Color _disableHighlightColor;
    
    [Header("Debug Serialize")]
    [SerializeField] private List<Button> _settingsButtons;
    [SerializeField] private List<Button> _buttons;

    [Header("Mouse Sensitivity")]
    [SerializeField] private Slider _mouseSensitivitySlider;
    [SerializeField] private TMP_Text _mouseSensitivityValue;

    private const string DodgeActionName = "Dodge";
    private const string RollActionName = "Roll";
    private const string LessonSkipActionName = "LessonSkip";
    private const string EvasionLocalizationTable = "Tables";
    private const string DodgeLocalizationKey = "Input_Dodge";
    private const string RollLocalizationKey = "Input_Roll";
    private const string LessonSkipLocalizationKey = "Input_LessonSkip";

    private InputRebinder dodgeRebinder;
    private InputRebinder rollRebinder;
    private InputRebinder lessonSkipRebinder;

    private void Awake()
    {
        // The bindings prefab is opened as a standalone RPG page. This reference is optional
        // because the Bindings prefab is opened directly by the menu settings page.
        _settingsButtons = _settingsCanvas != null
            ? _settingsCanvas.GetComponentsInChildren<Button>().ToList()
            : new List<Button>();
        CreateCombatBindingButtons();
        _buttons = GetComponentsInChildren<Button>().ToList();
        foreach (var button in _buttons)
        {
            button.onClick.AddListener(() => DisableButtons(button));
            button.onClick.AddListener(() => _soundPlayer?.PlayClick());

            var rebinder = button.GetComponent<InputRebinder>();
            if (rebinder == null)
                continue;
            rebinder.RebindCompleted += () => ActivateButtons(button);
            rebinder.RebindCCanceled += () => ActivateButtons(button);
            rebinder.Entered += PlayHover;
        }

        InitializeMouseSensitivitySlider();
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        UpdateCombatBindingDisplayNames();
    }

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;

        if (_mouseSensitivitySlider != null)
        {
            _mouseSensitivitySlider.onValueChanged.RemoveListener(SetMouseSensitivity);
        }
    }

    private void OnSelectedLocaleChanged(Locale _)
    {
        UpdateCombatBindingDisplayNames();
    }

    private void CreateCombatBindingButtons()
    {
        dodgeRebinder = CreateBindingButton(DodgeActionName, transformAfter: null);
        rollRebinder = CreateBindingButton(RollActionName, dodgeRebinder?.transform);
        lessonSkipRebinder = CreateBindingButton(LessonSkipActionName, rollRebinder?.transform);
    }

    private InputRebinder CreateBindingButton(string actionName, Transform transformAfter)
    {
        var existingBinding = GetComponentsInChildren<InputRebinder>(true)
            .FirstOrDefault(rebinder => rebinder.ActionName == actionName);
        if (existingBinding != null)
        {
            return existingBinding;
        }

        var template = GetComponentsInChildren<InputRebinder>(true)
            .FirstOrDefault(rebinder => rebinder.ActionName == "Right Mouse");
        if (template == null)
        {
            Debug.LogError($"{actionName} binding could not be created: Right Mouse binding row was not found.", this);
            return null;
        }

        var bindingButton = Instantiate(template.gameObject, template.transform.parent);
        bindingButton.name = $"Button_{actionName}";
        bindingButton.transform.SetSiblingIndex(
            transformAfter != null
                ? transformAfter.GetSiblingIndex() + 1
                : template.transform.GetSiblingIndex() + 1);

        foreach (var behaviour in bindingButton.GetComponentsInChildren<Behaviour>(true))
        {
            if (behaviour.GetType().Namespace?.StartsWith("UnityEngine.Localization") == true)
            {
                behaviour.enabled = false;
            }
        }

        var rebinder = bindingButton.GetComponent<InputRebinder>();
        rebinder.ConfigureAction(actionName, actionName);
        return rebinder;
    }

    private void UpdateCombatBindingDisplayNames()
    {
        UpdateBindingDisplayName(dodgeRebinder, DodgeLocalizationKey);
        UpdateBindingDisplayName(rollRebinder, RollLocalizationKey);
        UpdateBindingDisplayName(lessonSkipRebinder, LessonSkipLocalizationKey);
    }

    private async void UpdateBindingDisplayName(InputRebinder rebinder, string localizationKey)
    {
        if (rebinder == null)
        {
            return;
        }

        var localizedName = await LocalizationSettings.StringDatabase
            .GetLocalizedStringAsync(EvasionLocalizationTable, localizationKey).Task;
        if (rebinder != null)
        {
            rebinder.SetDisplayName(localizedName);
        }
    }

    public void UpdateText()
    {
        if (_buttons.Count <1 )
            return;
        foreach (var button in _buttons)
        {
            button.GetComponent<InputRebinder>().UpdateText();
        }
    }
    
    private void PlayHover()
    {
        _soundPlayer?.PlayHover();
    }

    private void InitializeMouseSensitivitySlider()
    {
        if (_mouseSensitivitySlider == null)
        {
            Debug.LogError("Mouse sensitivity Slider is not assigned.", this);
            return;
        }

        _mouseSensitivitySlider.minValue = MouseSensitivitySettings.Minimum;
        _mouseSensitivitySlider.maxValue = MouseSensitivitySettings.Maximum;
        _mouseSensitivitySlider.wholeNumbers = true;
        _mouseSensitivitySlider.SetValueWithoutNotify(MouseSensitivitySettings.Value);
        UpdateMouseSensitivityValue(MouseSensitivitySettings.Value);
        _mouseSensitivitySlider.onValueChanged.AddListener(SetMouseSensitivity);
    }

    private void SetMouseSensitivity(float value)
    {
        MouseSensitivitySettings.Set(value);
        UpdateMouseSensitivityValue(MouseSensitivitySettings.Value);
    }

    private void UpdateMouseSensitivityValue(int value)
    {
        if (_mouseSensitivityValue != null)
        {
            _mouseSensitivityValue.text = value.ToString();
        }
    }
    
    private void DisableButtons(Button button)
    {
        var selectedRebinder = button.GetComponent<InputRebinder>();
        if (selectedRebinder == null || !selectedRebinder.RebindingEnabled)
        {
            return;
        }

        foreach (var b in _buttons)
        {
            if (b == button)
            {
                var colors = button.colors;
                colors.disabledColor = _disableColorActiveButton;
                button.colors = colors;
            }
            
            var rebinder = b.GetComponent<InputRebinder>();
            if (rebinder != null)
                rebinder.Entered -= PlayHover;
            b.interactable = false;
        }

        foreach (var b in _settingsButtons)
        {
            var eventTrigger = b.GetComponent<EventTrigger>();
            if (eventTrigger != null)
                eventTrigger.enabled = false;
            
            var colors = b.colors;
            colors.highlightedColor = _disableHighlightColor;
            b.colors = colors;
        }
    }

    private void ActivateButtons(Button button)
    {
        foreach (var b in _buttons)
        {
            if (b == button)
            {
                var colors = button.colors;
                colors.disabledColor = _defDisableColor;
                button.colors = colors;
            }
            
            var rebinder = b.GetComponent<InputRebinder>();
            if (rebinder != null)
                rebinder.Entered += PlayHover;
            b.interactable = true;
            rebinder?.UpdateText();
        }
        
        foreach (var b in _settingsButtons)
        {
            var eventTrigger = b.GetComponent<EventTrigger>();
            if (eventTrigger != null)
                eventTrigger.enabled = true;
            
            var colors = b.colors;
            colors.highlightedColor = _highlightColor;
            b.colors = colors;
        }
        
        EventSystem.current.SetSelectedGameObject(null);
    }
}
