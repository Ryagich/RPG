using Dialogs.Graph.Model;

namespace Dialogs.Graph.Editor
{
    /// <summary>Transient editor state for connecting one dialog answer to a target phrase.</summary>
    internal sealed class DialogTargetSelectionState
    {
        public bool IsActive { get; private set; }
        public DialogAnswer PendingAnswer { get; private set; }
        public DialogPhrase SourcePhrase { get; private set; }

        public void Begin(DialogPhrase sourcePhrase, DialogAnswer answer)
        {
            SourcePhrase = sourcePhrase;
            PendingAnswer = answer;
            IsActive = sourcePhrase != null && answer != null;
        }

        public void Cancel()
        {
            IsActive = false;
            PendingAnswer = null;
            SourcePhrase = null;
        }
    }
}
