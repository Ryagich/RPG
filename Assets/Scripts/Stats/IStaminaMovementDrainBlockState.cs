namespace Stats
{
    /// <summary>
    /// An optional movement-state capability for conditions that replace the ordinary
    /// weight, run, and sprint drain with their own stamina rule.
    /// </summary>
    public interface IStaminaMovementDrainBlockState
    {
        bool IsMovementDrainBlocked { get; }
    }
}
