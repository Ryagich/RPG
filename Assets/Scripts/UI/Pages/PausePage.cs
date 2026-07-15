using GameModes;
using GameAudio;
using Input;
using Loading;
using MessagePipe;
using Messages;
using Stats;
using UI.Configs;
using UI.UIElements;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using Utils;

namespace UI.Pages
{
    public class PausePage : BasePage
    {
        private const string MenuSceneName = "Menu";

        public override PageType Type { get; } = PageType.Pause;

        private readonly UIConfig uiConfig;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;
        private readonly SceneLoadingService sceneLoadingService;
        private readonly StatsConfig statsConfig;
        private readonly StatsController statsController;
        private readonly StatFiller hpFiller;

        private RectTransform contentRect;
        private PauseMenu pauseMenu;
        private Image bloodScreen;

        public PausePage(
            UIConfig uiConfig,
            StatsConfig statsConfig,
            StatsController statsController,
            StatFillers statFillers,
            Canvas canvas,
            IObjectResolver resolver,
            IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher,
            SceneLoadingService sceneLoadingService)
        {
            this.uiConfig = uiConfig;
            this.statsConfig = statsConfig;
            this.statsController = statsController;
            hpFiller = statFillers.Get(StatType.Hp);
            this.resolver = resolver;
            this.changeGameModeRequestPublisher = changeGameModeRequestPublisher;
            this.sceneLoadingService = sceneLoadingService;

            canvasRect = canvas.GetComponent<RectTransform>();
        }

        public override void Draw()
        {
            contentRect = resolver.Instantiate(uiConfig.ContentPref, canvasRect);
            contentRect.name = $"{uiConfig.ContentPref.name} | {Type}";

            if (uiConfig.PauseMenu == null)
            {
                return;
            }

            pauseMenu = resolver.Instantiate(uiConfig.PauseMenu, contentRect);
            pauseMenu.name = $"{uiConfig.PauseMenu.name} | {Type}";

            bloodScreen = PageUiUtilities.CreateBloodScreen(uiConfig, resolver, contentRect, Type);
            ApplyFrozenBloodScreen();

            if (pauseMenu.ContinueButton != null)
            {
                pauseMenu.ContinueButton.onClick.AddListener(ContinueGame);
            }

            if (pauseMenu.MenuButton != null)
            {
                pauseMenu.MenuButton.onClick.AddListener(LoadMenu);
            }

            if (pauseMenu.SettingsButton != null)
            {
                pauseMenu.SettingsButton.onClick.AddListener(OpenSettings);
            }
        }

        public override void Hide()
        {
            if (pauseMenu != null)
            {
                if (pauseMenu.ContinueButton != null)
                {
                    pauseMenu.ContinueButton.onClick.RemoveListener(ContinueGame);
                }

                if (pauseMenu.MenuButton != null)
                {
                    pauseMenu.MenuButton.onClick.RemoveListener(LoadMenu);
                }

                if (pauseMenu.SettingsButton != null)
                {
                    pauseMenu.SettingsButton.onClick.RemoveListener(OpenSettings);
                }

                pauseMenu = null;
            }

            bloodScreen = null;

            if (contentRect)
            {
                Object.Destroy(contentRect.gameObject);
            }
        }

        private void ContinueGame()
        {
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
        }

        private void OpenSettings()
        {
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.PauseSettings));
        }

        private void LoadMenu()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true;
            sceneLoadingService.Load(MenuSceneName);
        }

        private void ApplyFrozenBloodScreen()
        {
            if (bloodScreen == null || statsConfig == null || statsController?.Hp == null || hpFiller == null)
            {
                return;
            }

            bloodScreen.raycastTarget = false;

            var color = statsConfig.HpDecreaseColor;
            color.a = CalculateBloodAlpha();
            bloodScreen.color = color;

            const float maxBeatScaleOffset = 0.035f;
            var scaleOffset = Mathf.Lerp(0f, maxBeatScaleOffset, color.a);
            var scale = Mathf.Lerp(1f + scaleOffset, 1f, CalculateCurrentHeartPulse());
            bloodScreen.transform.localScale = Vector3.one * scale;
        }

        private float CalculateBloodAlpha()
        {
            var hp = statsController.Hp;
            if (Mathf.Approximately(hp.Max, 0f))
            {
                return 0f;
            }

            var criticalHp = hp.Max * statsConfig.HpStat.MinSafePercent;
            var currentHp = Mathf.Clamp(hpFiller.Current.Value, 0f, hp.Max);

            if (criticalHp <= 0f)
            {
                return Mathf.Approximately(currentHp, 0f)
                    ? Mathf.Clamp01(statsConfig.BloodScreenAlphaMultiplier)
                    : 0f;
            }

            if (currentHp >= criticalHp)
            {
                return 0f;
            }

            var normalizedAlpha = 1f - Mathf.Clamp01(currentHp / criticalHp);
            return Mathf.Clamp01(normalizedAlpha * statsConfig.BloodScreenAlphaMultiplier);
        }

        private float CalculateCurrentHeartPulse()
        {
            var hp = statsController.Hp;
            if (Mathf.Approximately(hp.Max, 0f))
            {
                return 0.5f;
            }

            var missingHealthNormalized = 1f - Mathf.Clamp01(hpFiller.Current.Value / hp.Max);
            var baseBpm = Mathf.Lerp(statsConfig.MinHeartbeat, statsConfig.MaxHeartbeat, missingHealthNormalized);
            var bpm = baseBpm * statsConfig.HeartbeatTempoMultiplier;
            var phase = (bpm / 60f) * Time.time * Mathf.PI * 2f;
            return (Mathf.Pow(Mathf.Sin(phase), statsConfig.Sharpness) + 1f) * 0.5f;
        }
    }

    /// <summary>
    /// Settings opened from the pause menu. It uses the same prefabs as the main-menu settings
    /// while the PauseSettings game mode keeps gameplay frozen.
    /// </summary>
    public sealed class PauseSettingsPage : BasePage
    {
        private readonly UIConfig uiConfig;
        private readonly InputConfig inputConfig;
        private readonly IAudioService audioService;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;

        private RectTransform contentRect;
        private TitleSectionsHolder titleSections;
        private RectTransform bindingsPage;
        private SettingsMenuUI settingsMenuUI;
        private SoundSettingsPageUI soundSettingsPage;

        public override PageType Type { get; } = PageType.PauseSettings;

        public PauseSettingsPage(
            UIConfig uiConfig,
            InputConfig inputConfig,
            IAudioService audioService,
            Canvas canvas,
            IObjectResolver resolver,
            IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher)
        {
            this.uiConfig = uiConfig;
            this.inputConfig = inputConfig;
            this.audioService = audioService;
            this.resolver = resolver;
            this.changeGameModeRequestPublisher = changeGameModeRequestPublisher;
            canvasRect = canvas.GetComponent<RectTransform>();
        }

        public override void Draw()
        {
            ShowSection(SettingsSection.Bindings);
        }

        public override void Hide()
        {
            if (titleSections != null)
            {
                titleSections.SectionRequested -= ShowSection;
                titleSections.CloseRequested -= CloseSettings;
                titleSections.Dispose();
                titleSections = null;
            }

            ClearSectionContent();

            if (contentRect != null)
            {
                Object.Destroy(contentRect.gameObject);
                contentRect = null;
            }
        }

        private void ShowSection(SettingsSection section)
        {
            if (!EnsureSettingsShell())
            {
                return;
            }

            ClearSectionContent();
            titleSections.Initialize(section);

            switch (section)
            {
                case SettingsSection.Bindings:
                    DrawBindings();
                    break;
                case SettingsSection.Sounds:
                    DrawSounds();
                    break;
            }
        }

        private bool EnsureSettingsShell()
        {
            if (contentRect != null)
            {
                return true;
            }

            if (uiConfig.ContentPref == null || uiConfig.LeftMenuShadowGradient == null || uiConfig.TitleSections == null)
            {
                Debug.LogError("Content Pref, Left Menu Shadow Gradient, or Title Sections prefab is not assigned in UIConfig.");
                return false;
            }

            contentRect = resolver.Instantiate(uiConfig.ContentPref, canvasRect);
            contentRect.name = $"{uiConfig.ContentPref.name} | {Type}";

            var menuBackground = resolver.Instantiate(uiConfig.LeftMenuShadowGradient, contentRect);
            menuBackground.name = uiConfig.LeftMenuShadowGradient.name;
            menuBackground.SetAsFirstSibling();

            titleSections = resolver.Instantiate(uiConfig.TitleSections, contentRect);
            titleSections.name = uiConfig.TitleSections.name;
            titleSections.SectionRequested += ShowSection;
            titleSections.CloseRequested += CloseSettings;
            return true;
        }

        private void CloseSettings()
        {
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Pause));
        }

        private void DrawBindings()
        {
            if (uiConfig.Bindings == null)
            {
                Debug.LogError("Bindings prefab is not assigned in UIConfig.");
                return;
            }

            bindingsPage = resolver.Instantiate(uiConfig.Bindings, contentRect);
            bindingsPage.name = uiConfig.Bindings.name;
            settingsMenuUI = bindingsPage.gameObject.AddComponent<SettingsMenuUI>();
            settingsMenuUI.Initialize(inputConfig.Movement.action.actionMap.asset, uiConfig, bindingsPage);
        }

        private void DrawSounds()
        {
            if (uiConfig.SoundSettings == null)
            {
                Debug.LogError("Sound Settings prefab is not assigned in UIConfig.");
                return;
            }

            soundSettingsPage = resolver.Instantiate(uiConfig.SoundSettings, contentRect);
            soundSettingsPage.name = uiConfig.SoundSettings.name;
            soundSettingsPage.Initialize(audioService);
        }

        private void ClearSectionContent()
        {
            settingsMenuUI?.Dispose();
            settingsMenuUI = null;

            if (bindingsPage != null)
            {
                Object.Destroy(bindingsPage.gameObject);
                bindingsPage = null;
            }

            if (soundSettingsPage != null)
            {
                soundSettingsPage.Dispose();
                Object.Destroy(soundSettingsPage.gameObject);
                soundSettingsPage = null;
            }
        }
    }
}
