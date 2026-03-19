using GameModes;
using UnityEngine;

namespace Messages
{
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
}