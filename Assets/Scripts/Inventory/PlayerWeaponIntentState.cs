namespace Inventory
{
    /// <summary>
    /// Stores the player's latest weapon-selection and draw intent independently from the
    /// visual handoff that may still be running in the Animator.
    /// </summary>
    internal sealed class PlayerWeaponIntentState
    {
        public int SelectedSlotIndex { get; private set; } = 1;
        public bool IsDrawRequested { get; private set; }

        public bool IsSelectedSlot(int slotIndex)
        {
            return SelectedSlotIndex == slotIndex;
        }

        public void SelectSlotAndRequestDraw(int slotIndex)
        {
            SelectedSlotIndex = slotIndex;
            IsDrawRequested = true;
        }

        public void ToggleDrawRequest()
        {
            IsDrawRequested = !IsDrawRequested;
        }

        public void RequestDraw()
        {
            IsDrawRequested = true;
        }

        public void RequestSheathe()
        {
            IsDrawRequested = false;
        }

        public void SetDrawRequest(bool isDrawRequested)
        {
            IsDrawRequested = isDrawRequested;
        }
    }
}
