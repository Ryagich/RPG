using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.UIElements
{
    public sealed class OpeningSlidesView : MonoBehaviour
    {
        [field: SerializeField] public Image SlideImage { get; private set; }
        [field: SerializeField] public Image FilmWearOverlay { get; private set; }
        [field: SerializeField] public TMP_Text SlideText { get; private set; }
        [field: SerializeField] public TMP_Text NextText { get; private set; }
        [field: SerializeField] public TMP_Text SkipText { get; private set; }
        [field: SerializeField] public AudioSource VoiceOverSource { get; private set; }
    }
}
