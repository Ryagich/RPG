using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
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
    private void Awake()
    {
        // The bindings prefab is opened as a standalone RPG page. This reference is optional
        // because the Bindings prefab is opened directly by the menu settings page.
        _settingsButtons = _settingsCanvas != null
            ? _settingsCanvas.GetComponentsInChildren<Button>().ToList()
            : new List<Button>();
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
