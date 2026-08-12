using UnityEngine;

namespace Dialogue
{
    /// <summary>
    /// A deliberately closed set of gameplay events that dialogue content may raise.
    /// Each asset is a meaningful contract between authored dialogue and a gameplay system.
    /// </summary>
    [CreateAssetMenu(fileName = "Dialogue Gameplay Event", menuName = "configs/Dialogue/Gameplay Event")]
    public sealed class DialogueGameplayEvent : ScriptableObject
    {
    }
}
