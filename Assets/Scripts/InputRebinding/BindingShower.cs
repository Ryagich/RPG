using System;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class BindingShower : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;
    [SerializeField] private InputActionAsset _inputActionAsset;
    [SerializeField] private string _map = "Player";
    [SerializeField] private string _action;

    private void FixedUpdate()
    {
        SetKey();
    }

    private void SetKey()
    {
        var inputAction = _inputActionAsset.FindActionMap(_map).FindAction(_action);

        _text.text = InputControlPath.ToHumanReadableString(
            inputAction.bindings[inputAction.GetBindingIndexForControl(
                inputAction.controls[0])].effectivePath,
            InputControlPath.HumanReadableStringOptions.OmitDevice);
    }
}