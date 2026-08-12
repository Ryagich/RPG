using Dialogue;

namespace Messages
{
    public readonly struct DialogueExitRequestedMessage
    {
        public readonly bool ContinueForcedDialogueAfterExit;

        public DialogueExitRequestedMessage(bool continueForcedDialogueAfterExit = true)
        {
            ContinueForcedDialogueAfterExit = continueForcedDialogueAfterExit;
        }
    }

    public readonly struct DialogueGameplayEventRaisedMessage
    {
        public readonly DialogueGameplayEvent Event;

        public DialogueGameplayEventRaisedMessage(DialogueGameplayEvent @event)
        {
            Event = @event;
        }
    }
}
