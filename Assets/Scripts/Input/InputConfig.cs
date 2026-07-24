using UnityEngine;
using UnityEngine.InputSystem;

namespace Input
{
    [CreateAssetMenu(fileName = "InputConfig", menuName = "configs/Input/InputConfig")]
    public class InputConfig : ScriptableObject
    {
        [field: SerializeField] public InputActionReference Movement { get; private set; } = null!;
        [field: SerializeField] public InputActionReference Interactable { get; private set; }
        [field: SerializeField] public InputActionReference Inventory { get; private set; }
        [field: SerializeField] public InputActionReference LeftClick { get; private set; }
        [field: SerializeField] public InputActionReference RightClick { get; private set; }
        [field: SerializeField] public InputActionReference Dodge { get; private set; }
        [field: SerializeField] public InputActionReference Run { get; private set; }
        [field: SerializeField] public InputActionReference ShowStats { get; private set; }
        [field: SerializeField] public InputActionReference Pause { get; private set; }
        [field: SerializeField] public InputActionReference Map { get; private set; }
        [field: SerializeField] public InputActionReference TargetLock { get; private set; }
        [field: SerializeField] public InputActionReference TargetLockNext { get; private set; }
        [field: SerializeField] public InputActionReference TargetLockPrevious { get; private set; }
        [field: SerializeField] public InputActionReference FastSlot1 { get; private set; }
        [field: SerializeField] public InputActionReference FastSlot2 { get; private set; }
        [field: SerializeField] public InputActionReference FastSlot3 { get; private set; }
        [field: SerializeField] public InputActionReference FastSlot4 { get; private set; }
        [field: SerializeField] public InputActionReference WeaponSlot1 { get; private set; }
        [field: SerializeField] public InputActionReference WeaponSlot2 { get; private set; }
        [field: SerializeField] public InputActionReference CameraZoomIn { get; private set; }
        [field: SerializeField] public InputActionReference CameraZoomOut { get; private set; }

        private void OnEnable()
        {
            var actionMap = Movement?.action?.actionMap;
            if (actionMap == null)
            {
                return;
            }

            Interactable = EnsureReference(Interactable, actionMap, "Interactable");
            Inventory = EnsureReference(Inventory, actionMap, "Inventory");
            LeftClick = EnsureReference(LeftClick, actionMap, "Left Mouse");
            RightClick = EnsureReference(RightClick, actionMap, "Right Mouse");
            Dodge = EnsureReference(Dodge, actionMap, "Dodge");
            Run = EnsureReference(Run, actionMap, "Run");
            ShowStats = EnsureReference(ShowStats, actionMap, "ShowStats");
            Pause = EnsureReference(Pause, actionMap, "Pause");
            Map = EnsureReference(Map, actionMap, "Map");
            TargetLock = EnsureReference(TargetLock, actionMap, "TargetLock");
            TargetLockNext = EnsureReference(TargetLockNext, actionMap, "TargetLockNext");
            TargetLockPrevious = EnsureReference(TargetLockPrevious, actionMap, "TargetLockPrevious");
            FastSlot1 = EnsureReference(FastSlot1, actionMap, "FastSlot1");
            FastSlot2 = EnsureReference(FastSlot2, actionMap, "FastSlot2");
            FastSlot3 = EnsureReference(FastSlot3, actionMap, "FastSlot3");
            FastSlot4 = EnsureReference(FastSlot4, actionMap, "FastSlot4");
            WeaponSlot1 = EnsureReference(WeaponSlot1, actionMap, "WeaponSlot1");
            WeaponSlot2 = EnsureReference(WeaponSlot2, actionMap, "WeaponSlot2");
            CameraZoomIn = EnsureReference(CameraZoomIn, actionMap, "CameraZoomIn");
            CameraZoomOut = EnsureReference(CameraZoomOut, actionMap, "CameraZoomOut");
        }

        private static InputActionReference EnsureReference(
            InputActionReference reference,
            InputActionMap actionMap,
            string actionName)
        {
            if (reference?.action != null)
            {
                return reference;
            }

            var action = actionMap.FindAction(actionName, false);
            return action == null ? null : InputActionReference.Create(action);
        }
    }

    /// <summary>
    /// Persistent, project-wide mouse sensitivity expressed in points.
    /// One hundred points keeps the camera's authored sensitivity unchanged.
    /// </summary>
    public static class MouseSensitivitySettings
    {
        private const string PreferenceKey = "RPG.Input.MouseSensitivity";

        public const int Minimum = 1;
        public const int Maximum = 300;
        public const int Default = 100;

        private static int current = -1;

        public static int Value
        {
            get
            {
                if (current < Minimum)
                {
                    current = Mathf.Clamp(PlayerPrefs.GetInt(PreferenceKey, Default), Minimum, Maximum);
                }

                return current;
            }
        }

        public static float Multiplier => Value / (float)Default;

        public static void Set(float value)
        {
            current = Mathf.Clamp(Mathf.RoundToInt(value), Minimum, Maximum);
            PlayerPrefs.SetInt(PreferenceKey, current);
            PlayerPrefs.Save();
        }
    }
}
