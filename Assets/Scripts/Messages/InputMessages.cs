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

        public PlayerMoveMessage(Vector2 direction)
        {
            Direction = direction;
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
}
