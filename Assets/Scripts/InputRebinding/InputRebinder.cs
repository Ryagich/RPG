using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Settings;
using Input;

public class InputRebinder : MonoBehaviour, IPointerEnterHandler
{
    public event Action RebindCompleted;
    public event Action RebindCCanceled;
    public event Action Entered;

    [SerializeField] private InputActionAsset _inputActionAsset;
    [SerializeField] private string _mapName;
    [SerializeField] private string _actionName;
    [SerializeField] private int _bindingIndex;
    [SerializeField] private TMP_Text _text;
    [SerializeField] private Type _type;

    private InputAction inputAction;
    private InputActionRebindingExtensions.RebindingOperation rebindingOperation;
    private InputActionMap map;
    private string oldBind;
    private bool rebindingEnabled = true;
    private bool actionWasEnabled;

    public bool RebindingEnabled => rebindingEnabled;

    private enum Type
    {
        Button,
        Composite,
    }

    public void Configure(InputActionAsset inputActions, string mapName, string actionName, int bindingIndex, bool isComposite, string displayName)
    {
        _inputActionAsset = inputActions;
        _mapName = mapName;
        _actionName = actionName;
        _bindingIndex = bindingIndex;
        _type = isComposite ? Type.Composite : Type.Button;
        ResolveInputAction();

        foreach (var label in GetComponentsInChildren<TMP_Text>(true))
        {
            if (label != _text)
            {
                label.text = displayName;
                break;
            }
        }
    }

    private void Awake()
    {
        ResolveInputAction();
    }

    private void ResolveInputAction()
    {
        map = _inputActionAsset?.FindActionMap(_mapName);
        inputAction = map?.FindAction(_actionName);

        if (_inputActionAsset == null || inputAction == null)
            throw new Exception("InputAction PB");
    }

    private void Start()
    {
        UpdateText();
    }

    public void StartRebinding()
    {
        if (!rebindingEnabled)
        {
            return;
        }

        //Debug.Log("StartRebinding");
        //PrintBinds();
        oldBind = GetCurrentBinding().effectivePath;
        actionWasEnabled = inputAction.enabled;
        inputAction.Disable();
        if (_type == Type.Button)
        {
            rebindingOperation = inputAction.PerformInteractiveRebinding(_bindingIndex)
                // .WithControlsExcluding("Mouse")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(.1f)
                .OnComplete(operation => RebindComplete())
                .OnCancel(operation => RebindCancel())
                .Start();
        }
        else if (_type == Type.Composite)
        {
            rebindingOperation = inputAction.PerformInteractiveRebinding()
                .WithTargetBinding(_bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(.1f)
                .OnComplete(operation => RebindComplete())
                .OnCancel(operation => RebindCancel())
                .Start();
        }
    }

    private static void SetActionMap(string mapName)
    {
        var playerInput = FindObjectsByType<PlayerInput>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (playerInput.Length == 1)
        {
            playerInput[0].SwitchCurrentActionMap(mapName);
        }
    }

    private void RebindComplete()
    {
        UpdateText();
        // Debug.Log("RebindComplete");
        // PrintBinds();
        var currentBind = GetCurrentBinding();

        for (var i = 0; i < map.bindings.Count; i++)
        {
            if (map.bindings[i].effectivePath == currentBind.effectivePath
                && !BindingEqual(map.bindings[i], currentBind))
            {
                var newBind = map.bindings[i];
                newBind.overridePath = oldBind;
                map.ApplyBindingOverride(i, newBind);
            }
        }

        //Debug.Log("Unbind");
        //PrintBinds();
        rebindingOperation.Dispose();
        RestoreInputAction();
        BindingOverridesStorage.Save(_inputActionAsset);
        RebindCompleted?.Invoke();
    }

    private void RebindCancel()
    {
        rebindingOperation?.Dispose();
        rebindingOperation = null;
        RestoreInputAction();
        RebindCCanceled?.Invoke();
    }

    private void RestoreInputAction()
    {
        if (actionWasEnabled)
        {
            inputAction.Enable();
        }
    }

    private bool BindingEqual(InputBinding a, InputBinding b) => a.name == b.name && a.action == b.action;

    private void PrintBinds()
    {
        Debug.Log(string.Join('\n', map.bindings.Select(bind => $"{bind.name}-{bind.action}-{bind.effectivePath}")));
    }

    private InputBinding GetCurrentBinding()
    {
        return inputAction.bindings[_bindingIndex];
    }

    public void UpdateText()
    {
        var localeName = LocalizationSettings.SelectedLocale.Identifier.Code;
        var name = InputControlPath.ToHumanReadableString(
            GetCurrentBinding().effectivePath,
            InputControlPath.HumanReadableStringOptions.OmitDevice);
        switch (localeName)
        {
            case "en" or "EN":
                switch (name)
                {
                    case "Left Button":
                        name = "LMB";
                        break;
                    case "Right Button":
                        name = "RMB";
                        break;
                    case "Middle Button":
                        name = "MMB";
                        break;
                    case "Left Shift":
                        name = "L.Shift";
                        break;
                    case "Right Shift":
                        name = "R.Shift";
                        break;
                    case "Left Control":
                        name = "L.Control";
                        break;
                    case "Right Control":
                        name = "R.Control";
                        break;
                    case "Left Alt":
                        name = "L.Alt";
                        break;
                    case "Right Alt":
                        name = "R.Alt";
                        break;
                }

                break;
            case "ru" or "RU":
                switch (name)
                {
                    case "Left Button": name = "\u041b\u041a\u041c"; break;
                    case "Right Button": name = "\u041f\u041a\u041c"; break;
                    case "Middle Button": name = "\u041a\u043e\u043b\u0451\u0441\u0438\u043a\u043e"; break;
                    case "Space": name = "\u041f\u0440\u043e\u0431\u0435\u043b"; break;
                    case "Left Shift": name = "\u041b.Shift"; break;
                    case "Right Shift": name = "\u041f.Shift"; break;
                    case "Left Control": name = "\u041b.Control"; break;
                    case "Right Control": name = "\u041f.Control"; break;
                    case "Caps Lock": name = "Caps"; break;
                    case "Left Alt": name = "\u041b.Alt"; break;
                    case "Right Alt": name = "\u041f.Alt"; break;
                }

                break;
            case "tr" or "TR":
                switch (name)
                {
                    case "Left Button": name = "Sol FT"; break;
                    case "Right Button": name = "Sa\u011f FT"; break;
                    case "Middle Button": name = "Orta FT"; break;
                    case "Space": name = "Uzay"; break;
                    case "Left Shift": name = "Sol Shift"; break;
                    case "Right Shift": name = "Sa\u011f Shift"; break;
                    case "Left Control": name = "Sol Kontrol"; break;
                    case "Right Control": name = "Sa\u011f Kontrol"; break;
                    case "Caps Lock": name = "Caps Lock"; break;
                    case "Left Alt": name = "Sol Alt"; break;
                    case "Right Alt": name = "Sa\u011f Alt"; break;
                }

                break;
            case "es" or "ES":
                switch (name)
                {
                    case "Left Button":
                        name = "BIM";
                        break;
                    case "Right Button":
                        name = "BDM";
                        break;
                    case "Middle Button":
                        name = "BCM";
                        break;
                    case "Space":
                        name = "Espacio";
                        break;
                    case "Left Shift":
                        name = "L.Shift";
                        break;
                    case "Right Shift":
                        name = "D.Shift";
                        break;
                    case "Left Control":
                        name = "L.Control";
                        break;
                    case "Right Control":
                        name = "D.Control";
                        break;
                    case "Caps Lock":
                        name = "Caps Lock";
                        break;
                    case "Left Alt":
                        name = "L.Alt";
                        break;
                    case "Right Alt":
                        name = "D.Alt";
                        break;
                }

                break;
            case "de" or "DE":
                switch (name)
                {
                    case "Left Button":
                        name = "LM";
                        break;
                    case "Right Button":
                        name = "RM";
                        break;
                    case "Middle Button":
                        name = "MM";
                        break;
                    case "Space":
                        name = "Raum";
                        break;
                    case "Left Shift":
                        name = "L.Shift";
                        break;
                    case "Right Shift":
                        name = "R.Shift";
                        break;
                    case "Left Control":
                        name = "L.Control";
                        break;
                    case "Right Control":
                        name = "R.Control";
                        break;
                    case "Caps Lock":
                        name = "Caps Lock";
                        break;
                    case "Left Alt":
                        name = "L.Alt";
                        break;
                    case "Right Alt":
                        name = "R.Alt";
                        break;
                }

                break;
        }

        _text.text = name;
    }

    public void DisableRebinding()
    {
        rebindingEnabled = false;
    }


    public void OnPointerEnter(PointerEventData eventData)
    {
        Entered?.Invoke();
    }
}








