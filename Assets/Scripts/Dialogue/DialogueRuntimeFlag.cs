using UnityEngine;

namespace Dialogue
{
    /// <summary>
    /// An authored identity for a one-shot dialogue availability fact.
    /// Its value is kept at runtime by <see cref="DialogueRuntimeFlagRegistry"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueRuntimeFlag", menuName = "configs/Dialogs/Runtime Flag")]
    public sealed class DialogueRuntimeFlag : ScriptableObject
    {
    }
}
