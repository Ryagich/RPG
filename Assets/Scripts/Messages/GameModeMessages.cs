using GameModes;

namespace Messages
{
    public readonly struct GameModeChangedMessage
    {
        public readonly GameMode GameMode;

        public GameModeChangedMessage(GameMode gameMode)
        {
            GameMode = gameMode;
        }
    }
}