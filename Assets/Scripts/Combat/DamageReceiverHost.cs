using UnityEngine;
using VContainer;

namespace Combat
{
    [DisallowMultipleComponent]
    public sealed class DamageReceiverHost : MonoBehaviour
    {
        public CharacterDamageReceiver Receiver { get; private set; }

        [Inject]
        public void Construct(CharacterDamageReceiver receiver)
        {
            Receiver = receiver;
        }
    }
}
