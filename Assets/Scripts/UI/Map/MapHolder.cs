using UnityEngine;
using UnityEngine.UI;

namespace UI.Map
{
    public sealed class MapHolder : MonoBehaviour
    {
        [field: SerializeField] public ScrollRect MapScroll { get; private set; }
        [field: SerializeField] public Title Title { get; private set; }
    }
}
