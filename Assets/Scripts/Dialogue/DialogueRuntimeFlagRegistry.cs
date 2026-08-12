using System.Collections.Generic;

namespace Dialogue
{
    /// <summary>
    /// Project-lifetime, non-persistent facts used only to gate a dialogue flow.
    /// The registry intentionally stores identities rather than feature-specific state.
    /// </summary>
    public sealed class DialogueRuntimeFlagRegistry
    {
        private readonly HashSet<DialogueRuntimeFlag> activeFlags = new();

        public bool IsActive(DialogueRuntimeFlag flag) => flag != null && activeFlags.Contains(flag);

        public void Activate(DialogueRuntimeFlag flag)
        {
            if (flag != null)
            {
                activeFlags.Add(flag);
            }
        }

        public void Deactivate(DialogueRuntimeFlag flag)
        {
            if (flag != null)
            {
                activeFlags.Remove(flag);
            }
        }

        public void Replace(IReadOnlyList<DialogueRuntimeFlag> candidates, DialogueRuntimeFlag activeFlag)
        {
            if (candidates != null)
            {
                foreach (DialogueRuntimeFlag candidate in candidates)
                {
                    Deactivate(candidate);
                }
            }

            Activate(activeFlag);
        }
    }
}
