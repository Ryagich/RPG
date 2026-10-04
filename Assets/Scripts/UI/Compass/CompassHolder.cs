using TMPro;
using UnityEngine;

namespace UI.Compass
{
    public sealed class CompassHolder : MonoBehaviour
    {
        [field: SerializeField] public TMP_Text Direction { get; private set; }
    }
}