namespace Combat
{
    public sealed class CharacterActionState
    {
        public bool IsActionBlocked { get; private set; }

        public void SetActionBlocked(bool isBlocked)
        {
            IsActionBlocked = isBlocked;
        }
    }
}
