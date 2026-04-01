using Character;
using Interactable;

namespace Dialogue
{
    public class DialogueContext
    {
        public Interactable.Interactable CurrentTarget { get; private set; }
        public CharacterInfo CurrentTargetCharacterInfo { get; private set; }

        public void SetTarget(Interactable.Interactable target, CharacterInfo characterInfo = null)
        {
            CurrentTarget = target;
            CurrentTargetCharacterInfo = characterInfo;
        }

        public void Clear()
        {
            CurrentTarget = null;
            CurrentTargetCharacterInfo = null;
        }
    }
}