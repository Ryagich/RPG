namespace Messages
{
    /// <summary>
    /// Raised from an NPC weapon animation event after the attack has visibly begun.
    /// Consumers can synchronize presentation without observing animator state.
    /// </summary>
    public readonly struct NpcAttackStartedMessage
    {
        public UnityEngine.Transform CharacterTransform { get; }

        public NpcAttackStartedMessage(UnityEngine.Transform characterTransform)
        {
            CharacterTransform = characterTransform;
        }
    }

    public readonly struct LessonSkipInputMessage
    {
    }

    public enum LessonEvasionAction
    {
        Dodge,
        Roll
    }

    /// <summary>
    /// Acknowledges the action requested by an unskippable evasion lesson. The input layer
    /// keeps the raw combat command out of the paused game mode; the training session resumes
    /// gameplay and issues that command exactly once.
    /// </summary>
    public readonly struct LessonEvasionInputMessage
    {
        public LessonEvasionAction Action { get; }

        public LessonEvasionInputMessage(LessonEvasionAction action)
        {
            Action = action;
        }
    }

    /// <summary>
    /// Captures an attack input while a lesson pauses gameplay. The training session decides
    /// whether that input completes its current lesson before forwarding the normal command.
    /// </summary>
    public readonly struct LessonAttackInputMessage
    {
        public MouseButtonType Button { get; }

        public LessonAttackInputMessage(MouseButtonType button)
        {
            Button = button;
        }
    }

    public readonly struct PlayerEvasionCompletedMessage
    {
        public bool IsRoll { get; }

        public PlayerEvasionCompletedMessage(bool isRoll)
        {
            IsRoll = isRoll;
        }
    }

}
