using UnityEngine;
using UnityEngine.UI;

namespace Stats
{
    public class StatHolder : MonoBehaviour
    {
        [field: SerializeField] public Image Icon { get; private set; }
        [field: SerializeField] public Image BackFill { get; private set; }
        [field: SerializeField] public Image Fill { get; private set; }
        [field: SerializeField] public Image ChangedFill { get; private set; }
    }
}
