namespace Player
{
    public sealed class PlayerDeathState
    {
        public bool IsDead { get; private set; }

        public void MarkDead()
        {
            IsDead = true;
        }
    }
}
