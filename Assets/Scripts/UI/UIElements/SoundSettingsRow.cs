using GameAudio;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace UI.UIElements
{
    public sealed class SoundSettingsRow : MonoBehaviour
    {
        private const string LocalizationTable = "Tables";
        private const string MasterTitleKey = "Audio_Settings_Master";
        private const string UiTitleKey = "Audio_Settings_UI";
        private const string GameTitleKey = "Audio_Settings_Game";
        private const string MusicTitleKey = "Audio_Settings_Music";
        private const string VoiceTitleKey = "Audio_Settings_Voice";

        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text value;
        [SerializeField] private Slider slider;

        private IAudioService audioService;
        private AudioMixerCategory category;
        private LocalizedString localizedTitle;

        public void Initialize(AudioMixerCategory valueCategory, IAudioService valueAudioService)
        {
            category = valueCategory;
            audioService = valueAudioService;
            localizedTitle = new LocalizedString(LocalizationTable, GetTitleKey(valueCategory));
            localizedTitle.StringChanged += OnTitleChanged;
            slider.SetValueWithoutNotify(audioService.GetNormalizedVolume(valueCategory));
            UpdateValue(slider.value);
            slider.onValueChanged.AddListener(OnValueChanged);
        }

        public void Dispose()
        {
            slider?.onValueChanged.RemoveListener(OnValueChanged);
            if (localizedTitle != null)
            {
                localizedTitle.StringChanged -= OnTitleChanged;
                localizedTitle = null;
            }

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

        private void OnTitleChanged(string localizedText)
        {
            if (title != null)
            {
                title.text = localizedText;
            }
        }

        private static string GetTitleKey(AudioMixerCategory category)
        {
            return category switch
            {
                AudioMixerCategory.Master => MasterTitleKey,
                AudioMixerCategory.UI => UiTitleKey,
                AudioMixerCategory.Game => GameTitleKey,
                AudioMixerCategory.Music => MusicTitleKey,
                AudioMixerCategory.Voice => VoiceTitleKey,
                _ => throw new System.ArgumentOutOfRangeException(nameof(category), category, null),
            };
        }
    }
}
