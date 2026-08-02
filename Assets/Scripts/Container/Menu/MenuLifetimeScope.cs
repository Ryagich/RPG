using Container.Project;
using Loading;
using UI.Configs;
using UI.Pages;
using UI.UIElements;
using GameAudio;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Container.Menu
{
    public sealed class MenuLifetimeScope : LifetimeScope
    {
        [SerializeField] private Canvas sceneCanvas;

        private Canvas canvas;

        protected override void Awake()
        {
            var projectScope = ProjectLifetimeScope.Instance;
            if (projectScope == null)
            {
                Debug.LogError("ProjectLifetimeScope not found.");
                return;
            }

            canvas = sceneCanvas;
            if (canvas == null)
            {
                Debug.LogError("Menu canvas is not assigned.", this);
                return;
            }

            parentReference.Object = projectScope;
            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(canvas).As<Canvas>();
            builder.Register<MenuMainPage>(Lifetime.Singleton);
            builder.Register<MenuSettingsPage>(Lifetime.Singleton);
            builder.Register<MenuSoundsSettingsPage>(Lifetime.Singleton);
            builder.Register<MenuGameplaySettingsPage>(Lifetime.Singleton);
            builder.RegisterEntryPoint<MenuController>().AsSelf();
        }
    }

    public sealed class MenuController : IStartable, System.IDisposable
    {
        private const string DevelopSceneName = "Develop";
        private const string GameSceneName = "Village";

        private readonly SceneLoadingService sceneLoadingService;
        private readonly MenuMainPage mainPage;
        private readonly MenuSettingsPage bindingsSettingsPage;
        private readonly MenuSoundsSettingsPage soundsSettingsPage;
        private readonly MenuGameplaySettingsPage gameplaySettingsPage;
        private readonly IAudioService audioService;
        private BasePage currentPage;

        public MenuController(
            SceneLoadingService sceneLoadingService,
            MenuMainPage mainPage,
            MenuSettingsPage bindingsSettingsPage,
            MenuSoundsSettingsPage soundsSettingsPage,
            MenuGameplaySettingsPage gameplaySettingsPage,
            IAudioService audioService)
        {
            this.sceneLoadingService = sceneLoadingService;
            this.mainPage = mainPage;
            this.bindingsSettingsPage = bindingsSettingsPage;
            this.soundsSettingsPage = soundsSettingsPage;
            this.gameplaySettingsPage = gameplaySettingsPage;
            this.audioService = audioService;
        }

        public void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true;
            audioService.PlayMainMenuMusic();

            mainPage.GameRequested += LoadGame;
            mainPage.DevelopRequested += LoadDevelop;
            mainPage.SettingsRequested += ShowSettings;
            SubscribeSettingsPage(bindingsSettingsPage);
            SubscribeSettingsPage(soundsSettingsPage);
            SubscribeSettingsPage(gameplaySettingsPage);
            ShowMain();
        }

        public void Dispose()
        {
            mainPage.GameRequested -= LoadGame;
            mainPage.DevelopRequested -= LoadDevelop;
            mainPage.SettingsRequested -= ShowSettings;
            UnsubscribeSettingsPage(bindingsSettingsPage);
            UnsubscribeSettingsPage(soundsSettingsPage);
            UnsubscribeSettingsPage(gameplaySettingsPage);
            currentPage?.Hide();
            currentPage = null;
            audioService.StopMainMenuMusic();
        }

        private void LoadDevelop()
        {
            audioService.StopMainMenuMusic();
            sceneLoadingService.Load(DevelopSceneName);
        }

        private void LoadGame()
        {
            audioService.StopMainMenuMusic();
            sceneLoadingService.Load(GameSceneName);
        }

        private void ShowMain() => ShowPage(mainPage);

        private void ShowSettings() => ShowPage(bindingsSettingsPage);

        private void SubscribeSettingsPage(MenuSettingsSectionPage page)
        {
            page.SectionRequested += ShowSettingsSection;
            page.CloseRequested += ShowMain;
        }

        private void UnsubscribeSettingsPage(MenuSettingsSectionPage page)
        {
            page.SectionRequested -= ShowSettingsSection;
            page.CloseRequested -= ShowMain;
        }

        private void ShowSettingsSection(SettingsSection section)
        {
            switch (section)
            {
                case SettingsSection.Bindings:
                    ShowPage(bindingsSettingsPage);
                    break;
                case SettingsSection.Sounds:
                    ShowPage(soundsSettingsPage);
                    break;
                case SettingsSection.Gameplay:
                    ShowPage(gameplaySettingsPage);
                    break;
            }
        }

        private void ShowPage(BasePage page)
        {
            if (currentPage == page)
            {
                return;
            }

            currentPage?.Hide();
            currentPage = page;
            currentPage.Draw();
        }
    }

    public sealed class MenuMainPage : BasePage
    {
        private readonly UIConfig uiConfig;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private RectTransform contentRect;
        private RectTransform menuBackground;
        private MenuUI menuUI;

        public override PageType Type { get; } = PageType.MenuMain;
        public event System.Action GameRequested;
        public event System.Action DevelopRequested;
        public event System.Action SettingsRequested;

        public MenuMainPage(UIConfig uiConfig, Canvas canvas, IObjectResolver resolver)
        {
            this.uiConfig = uiConfig;
            this.resolver = resolver;
            canvasRect = canvas.GetComponent<RectTransform>();
        }

        public override void Draw()
        {
            if (contentRect != null)
            {
                return;
            }

            if (uiConfig.ContentPref == null || uiConfig.LeftMenuShadowGradient == null || uiConfig.MenuUI == null)
            {
                Debug.LogError("Content Pref, Left Menu Shadow Gradient, or Main Menu prefab is not assigned in UIConfig.");
                return;
            }

            contentRect = resolver.Instantiate(uiConfig.ContentPref, canvasRect);
            contentRect.name = $"{uiConfig.ContentPref.name} | {Type}";

            menuUI = resolver.Instantiate(uiConfig.MenuUI, contentRect);
            menuUI.name = uiConfig.MenuUI.name;

            menuBackground = resolver.Instantiate(uiConfig.LeftMenuShadowGradient, menuUI.GetComponent<RectTransform>());
            menuBackground.name = uiConfig.LeftMenuShadowGradient.name;
            menuBackground.SetAsFirstSibling();

            menuUI.ToGameButton.onClick.AddListener(OnGameRequested);
            menuUI.ToDevelopButton.onClick.AddListener(OnDevelopRequested);
            menuUI.SettingsButton.onClick.AddListener(OnSettingsRequested);
        }

        public override void Hide()
        {
            if (contentRect == null)
            {
                return;
            }

            if (menuUI != null)
            {
                menuUI.ToGameButton.onClick.RemoveListener(OnGameRequested);
                menuUI.ToDevelopButton.onClick.RemoveListener(OnDevelopRequested);
                menuUI.SettingsButton.onClick.RemoveListener(OnSettingsRequested);
            }

            Object.Destroy(contentRect.gameObject);
            contentRect = null;
            menuBackground = null;
            menuUI = null;
        }

        private void OnGameRequested() => GameRequested?.Invoke();
        private void OnDevelopRequested() => DevelopRequested?.Invoke();
        private void OnSettingsRequested() => SettingsRequested?.Invoke();
    }

    public abstract class MenuSettingsSectionPage : BasePage
    {
        private readonly UIConfig uiConfig;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private RectTransform contentRect;
        private RectTransform menuBackground;
        private TitleSectionsHolder titleSections;

        protected abstract SettingsSection Section { get; }
        public event System.Action<SettingsSection> SectionRequested;
        public event System.Action CloseRequested;

        protected MenuSettingsSectionPage(UIConfig uiConfig, Canvas canvas, IObjectResolver resolver)
        {
            this.uiConfig = uiConfig;
            this.resolver = resolver;
            canvasRect = canvas.GetComponent<RectTransform>();
        }

        public override void Draw()
        {
            if (contentRect != null)
            {
                return;
            }

            if (uiConfig.ContentPref == null || uiConfig.LeftMenuShadowGradient == null || uiConfig.TitleSections == null)
            {
                Debug.LogError("Content Pref, Left Menu Shadow Gradient, or Title Sections prefab is not assigned in UIConfig.");
                return;
            }

            if (!CanDrawSectionContent())
            {
                return;
            }

            contentRect = resolver.Instantiate(uiConfig.ContentPref, canvasRect);
            contentRect.name = $"{uiConfig.ContentPref.name} | {Type}";

            menuBackground = resolver.Instantiate(uiConfig.LeftMenuShadowGradient, contentRect);
            menuBackground.name = uiConfig.LeftMenuShadowGradient.name;
            menuBackground.SetAsFirstSibling();

            titleSections = resolver.Instantiate(uiConfig.TitleSections, contentRect);
            titleSections.name = uiConfig.TitleSections.name;
            titleSections.Initialize(Section);
            titleSections.SectionRequested += OnSectionRequested;
            titleSections.CloseRequested += OnCloseRequested;

            DrawSectionContent(contentRect);
        }

        public override void Hide()
        {
            if (contentRect == null)
            {
                return;
            }

            if (titleSections != null)
            {
                titleSections.SectionRequested -= OnSectionRequested;
                titleSections.CloseRequested -= OnCloseRequested;
                titleSections.Dispose();
            }

            HideSectionContent();
            Object.Destroy(contentRect.gameObject);
            titleSections = null;
            menuBackground = null;
            contentRect = null;
        }

        protected virtual bool CanDrawSectionContent() => true;
        protected virtual void DrawSectionContent(RectTransform parent) { }
        protected virtual void HideSectionContent() { }

        private void OnSectionRequested(SettingsSection section) => SectionRequested?.Invoke(section);
        private void OnCloseRequested() => CloseRequested?.Invoke();
    }

    public sealed class MenuSettingsPage : MenuSettingsSectionPage
    {
        private readonly UIConfig uiConfig;
        private readonly Input.InputConfig inputConfig;
        private readonly IObjectResolver resolver;
        private RectTransform bindingsPage;
        private SettingsMenuUI settingsMenuUI;

        public override PageType Type { get; } = PageType.MenuSettings;
        protected override SettingsSection Section => SettingsSection.Bindings;

        public MenuSettingsPage(UIConfig uiConfig, Input.InputConfig inputConfig, Canvas canvas, IObjectResolver resolver)
            : base(uiConfig, canvas, resolver)
        {
            this.uiConfig = uiConfig;
            this.inputConfig = inputConfig;
            this.resolver = resolver;
        }

        protected override bool CanDrawSectionContent()
        {
            if (uiConfig.Bindings != null)
            {
                return true;
            }

            Debug.LogError("Bindings prefab is not assigned in UIConfig.");
            return false;
        }

        protected override void DrawSectionContent(RectTransform parent)
        {
            bindingsPage = resolver.Instantiate(uiConfig.Bindings, parent);
            bindingsPage.name = uiConfig.Bindings.name;
            settingsMenuUI = bindingsPage.gameObject.AddComponent<SettingsMenuUI>();
            settingsMenuUI.Initialize(inputConfig.Movement.action.actionMap.asset, uiConfig, bindingsPage);
        }

        protected override void HideSectionContent()
        {
            settingsMenuUI?.Dispose();
            settingsMenuUI = null;
            bindingsPage = null;
        }
    }

    public sealed class MenuSoundsSettingsPage : MenuSettingsSectionPage
    {
        private readonly UIConfig uiConfig;
        private readonly IObjectResolver resolver;
        private readonly IAudioService audioService;
        private SoundSettingsPageUI soundSettingsPage;

        public override PageType Type { get; } = PageType.MenuSoundsSettings;
        protected override SettingsSection Section => SettingsSection.Sounds;

        public MenuSoundsSettingsPage(UIConfig uiConfig, Canvas canvas, IObjectResolver resolver, IAudioService audioService)
            : base(uiConfig, canvas, resolver)
        {
            this.uiConfig = uiConfig;
            this.resolver = resolver;
            this.audioService = audioService;
        }

        protected override bool CanDrawSectionContent()
        {
            if (uiConfig.SoundSettings != null)
            {
                return true;
            }

            Debug.LogError("Sound Settings prefab is not assigned in UIConfig.");
            return false;
        }

        protected override void DrawSectionContent(RectTransform parent)
        {
            soundSettingsPage = resolver.Instantiate(uiConfig.SoundSettings, parent);
            soundSettingsPage.name = uiConfig.SoundSettings.name;
            soundSettingsPage.Initialize(audioService);
        }

        protected override void HideSectionContent()
        {
            soundSettingsPage?.Dispose();
            soundSettingsPage = null;
        }
    }

    public sealed class MenuGameplaySettingsPage : MenuSettingsSectionPage
    {
        public override PageType Type { get; } = PageType.MenuGameplaySettings;
        protected override SettingsSection Section => SettingsSection.Gameplay;

        public MenuGameplaySettingsPage(UIConfig uiConfig, Canvas canvas, IObjectResolver resolver)
            : base(uiConfig, canvas, resolver) { }
    }
}
