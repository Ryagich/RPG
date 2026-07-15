using GameAudio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.UIElements
{
    public sealed class SoundSettingsRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text value;
        [SerializeField] private Slider slider;

        private IAudioService audioService;
        private AudioMixerCategory category;

        public void Initialize(AudioMixerCategory valueCategory, IAudioService valueAudioService)
        {
            category = valueCategory;
            audioService = valueAudioService;
            title.text = GetTitle(valueCategory);
            slider.SetValueWithoutNotify(audioService.GetNormalizedVolume(valueCategory));
            UpdateValue(slider.value);
            slider.onValueChanged.AddListener(OnValueChanged);
        }

        public void Dispose()
        {
            slider?.onValueChanged.RemoveListener(OnValueChanged);
            audioService = null;
        }

        private void OnValueChanged(float normalizedValue)
        {
            audioService?.SetNormalizedVolume(category, normalizedValue);
            UpdateValue(normalizedValue);
        }

        private void UpdateValue(float normalizedValue)
        {
            if (value != null)
            {
                value.text = $"{Mathf.RoundToInt(normalizedValue * 100f)}%";
            }
        }

        private static string GetTitle(AudioMixerCategory category)
        {
            return category switch
            {
                AudioMixerCategory.Master => "Общая громкость",
                AudioMixerCategory.UI => "Интерфейс",
                AudioMixerCategory.Game => "Игра",
                AudioMixerCategory.Music => "Музыка",
                _ => category.ToString(),
            };
        }
    }
}
