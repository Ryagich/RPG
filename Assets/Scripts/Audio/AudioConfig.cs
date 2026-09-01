using UnityEngine;
using UnityEngine.Audio;

namespace GameAudio
{
    /// <summary>
    /// Single project-owned source of truth for mixer links, default levels and test sounds.
    /// Runtime volume changes are stored separately in PlayerPrefs, leaving this asset immutable.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "configs/Audio/Audio Config")]
    public sealed class AudioConfig : ScriptableObject
    {
        public const float MinimumDecibels = -80f;
        public const float MaximumDecibels = 0f;

        [Header("Mixer")]
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup masterMixerGroup;
        [SerializeField] private AudioMixerGroup uiMixerGroup;
        [SerializeField] private AudioMixerGroup gameMixerGroup;
        [SerializeField] private AudioMixerGroup musicMixerGroup;
        [SerializeField] private AudioMixerGroup voiceMixerGroup;

        [Header("Pooled source prefab")]
        [SerializeField] private AudioSource footstepSourcePrefab;

        [Header("UI")]
        [SerializeField] private AudioClip buttonHoverClip;
        [SerializeField] private AudioClip buttonClickClip;

        [Header("Music")]
        [SerializeField] private AudioClip mainMenuMusicClip;

        [Header("Default mixer levels")]
        [SerializeField, Range(MinimumDecibels, MaximumDecibels)] private float masterDefaultDecibels = -10f;
        [SerializeField, Range(MinimumDecibels, MaximumDecibels)] private float uiDefaultDecibels = -30f;
        [SerializeField, Range(MinimumDecibels, MaximumDecibels)] private float gameDefaultDecibels = -15f;
        [SerializeField, Range(MinimumDecibels, MaximumDecibels)] private float musicDefaultDecibels = -5f;
        [SerializeField, Range(MinimumDecibels, MaximumDecibels)] private float voiceDefaultDecibels = 0f;

        public AudioMixer Mixer => mixer;
        public AudioSource FootstepSourcePrefab => footstepSourcePrefab;
        public AudioClip ButtonHoverClip => buttonHoverClip;
        public AudioClip ButtonClickClip => buttonClickClip;
        public AudioClip MainMenuMusicClip => mainMenuMusicClip;

        public static readonly AudioMixerCategory[] SettingsCategories =
        {
            AudioMixerCategory.Master,
            AudioMixerCategory.UI,
            AudioMixerCategory.Game,
            AudioMixerCategory.Music,
            AudioMixerCategory.Voice,
        };

        public string GetExposedParameter(AudioMixerCategory category) => $"{category}_Volume";

        public float GetDefaultDecibels(AudioMixerCategory category)
        {
            return category switch
            {
                AudioMixerCategory.Master => masterDefaultDecibels,
                AudioMixerCategory.UI => uiDefaultDecibels,
                AudioMixerCategory.Game => gameDefaultDecibels,
                AudioMixerCategory.Music => musicDefaultDecibels,
                AudioMixerCategory.Voice => voiceDefaultDecibels,
                _ => MaximumDecibels,
            };
        }

        public AudioMixerGroup GetMixerGroup(AudioMixerCategory category)
        {
            var configuredGroup = category switch
            {
                AudioMixerCategory.Master => masterMixerGroup,
                AudioMixerCategory.UI => uiMixerGroup,
                AudioMixerCategory.Game => gameMixerGroup,
                AudioMixerCategory.Music => musicMixerGroup,
                AudioMixerCategory.Voice => voiceMixerGroup,
                _ => null,
            };
            return configuredGroup;
        }

        public void ConfigureForProject(
            AudioMixer valueMixer,
            AudioSource valueFootstepSourcePrefab,
            AudioClip valueButtonHoverClip,
            AudioClip valueButtonClickClip)
        {
            mixer = valueMixer;
            masterMixerGroup = FindGroup(AudioMixerCategory.Master);
            uiMixerGroup = FindGroup(AudioMixerCategory.UI);
            gameMixerGroup = FindGroup(AudioMixerCategory.Game);
            musicMixerGroup = FindGroup(AudioMixerCategory.Music);
            voiceMixerGroup = FindGroup(AudioMixerCategory.Voice);
            footstepSourcePrefab = valueFootstepSourcePrefab;
            buttonHoverClip = valueButtonHoverClip;
            buttonClickClip = valueButtonClickClip;
        }

        private AudioMixerGroup FindGroup(AudioMixerCategory category)
        {
            if (mixer == null)
            {
                return null;
            }

            var expectedName = category.ToString();
            var groups = mixer.FindMatchingGroups(expectedName);
            if (groups == null)
            {
                return null;
            }

            foreach (var group in groups)
            {
                if (group != null && group.name == expectedName)
                {
                    return group;
                }
            }

            return null;
        }
    }
}
