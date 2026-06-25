using GameModes;
using UnityEngine;

namespace Messages
{
    public enum MouseButtonType
    {
        Left,
        Right
    }

    public readonly struct PlayerMoveMessage
    {
        public readonly Vector2 Direction;
        public readonly bool IsRunning;

        public PlayerMoveMessage(Vector2 direction, bool isRunning)
        {
            Direction = direction;
            IsRunning = isRunning;
        }
    }
    
    public readonly struct ChangeGameModeRequest
    {
        public readonly GameMode Mode;

        public ChangeGameModeRequest(GameMode mode)
        {
            Mode = mode;
        }
    }
    
    public readonly struct InteractableInputMessage { }

    public readonly struct PauseInputMessage { }

    public enum TargetLockCommand
    {
        Toggle,
        Next,
        Previous
    }

    public readonly struct TargetLockInputMessage
    {
        public readonly TargetLockCommand Command;

        public TargetLockInputMessage(TargetLockCommand command)
        {
            Command = command;
        }
    }
    
    public readonly struct MouseDown
    {
        public readonly MouseButtonType Button;

        public MouseDown(MouseButtonType button)
        {
            Button = button;
        }
    }

    public readonly struct MouseUp
    {
        public readonly MouseButtonType Button;

        public MouseUp(MouseButtonType button)
        {
            Button = button;
        }
    }

    public readonly struct ShowStatsInputMessage
    {
        public readonly bool IsPressed;

        public ShowStatsInputMessage(bool isPressed)
        {
            IsPressed = isPressed;
        }
    }

    public readonly struct FastSlotInputMessage
    {
        public readonly int SlotIndex;

        public FastSlotInputMessage(int slotIndex)
        {
            SlotIndex = slotIndex;
        }
    }

    public readonly struct WeaponSlotInputMessage
    {
        public readonly int SlotIndex;

        public WeaponSlotInputMessage(int slotIndex)
        {
            SlotIndex = slotIndex;
        }
    }
}
