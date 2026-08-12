namespace Quests
{
    /// <summary>
    /// Transient policy that protects a system-owned current objective from being
    /// replaced through the journal while that system is active.
    /// </summary>
    public sealed class QuestSelectionLock
    {
        public bool IsLocked { get; private set; }

        public void Lock() => IsLocked = true;
        public void Unlock() => IsLocked = false;
    }
}
