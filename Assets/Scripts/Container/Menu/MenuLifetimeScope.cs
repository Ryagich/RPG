using Container.Project;
using UI.Configs;
using UI.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Container.Menu
{
    public sealed class MenuLifetimeScope : LifetimeScope
    {
        [SerializeField] private Canvas sceneCanvas;
        [SerializeField] private Canvas canvasPrefab;

        private Canvas canvas;

        protected override void Awake()
        {
            var projectScope = Find<ProjectLifetimeScope>();
            if (projectScope == null)
            {
                Debug.LogError("ProjectLifetimeScope not found.");
                return;
            }

            canvas = GetCanvas();
            if (canvas == null)
            {
                Debug.LogError("Menu canvas is not assigned and no Canvas was found in the Menu scene.");
                return;
            }

            parentReference.Object = projectScope;
            base.Awake();
        }

        private Canvas GetCanvas()
        {
            if (sceneCanvas != null)
            {
                return sceneCanvas;
            }

            var sceneCanvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var sceneCanvasCandidate in sceneCanvases)
            {
                if (sceneCanvasCandidate.gameObject.scene == gameObject.scene)
                {
                    return sceneCanvasCandidate;
                }
            }

            if (canvasPrefab == null)
            {
                return null;
            }

            var canvasInstance = Instantiate(canvasPrefab, transform);
            canvasInstance.name = canvasPrefab.name;
            return canvasInstance;
        }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(canvas).As<Canvas>();
            builder.RegisterEntryPoint<MenuController>().AsSelf();
        }
    }

    public sealed class MenuController : IStartable, System.IDisposable
    {
        private const string DevelopSceneName = "Develop";
        private const string GameSceneName = "Game";

        private readonly UIConfig uiConfig;
        private readonly Canvas canvas;
        private readonly IObjectResolver resolver;

        private MenuUI menuUI;

        public MenuController(UIConfig uiConfig, Canvas canvas, IObjectResolver resolver)
        {
            this.uiConfig = uiConfig;
            this.canvas = canvas;
            this.resolver = resolver;
        }

        public void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (uiConfig.MenuUI == null)
            {
                Debug.LogError("Menu UI prefab is not assigned in UIConfig.");
                return;
            }

            menuUI = resolver.Instantiate(uiConfig.MenuUI, canvas.transform);
            menuUI.name = uiConfig.MenuUI.name;

            menuUI.ToDevelopButton.onClick.AddListener(LoadDevelop);
            menuUI.ToGameButton.onClick.AddListener(LoadGame);
        }

        public void Dispose()
        {
            if (menuUI == null)
            {
                return;
            }

            menuUI.ToDevelopButton.onClick.RemoveListener(LoadDevelop);
            menuUI.ToGameButton.onClick.RemoveListener(LoadGame);
        }

        private static void LoadDevelop()
        {
            SceneManager.LoadScene(DevelopSceneName);
        }

        private static void LoadGame()
        {
            SceneManager.LoadScene(GameSceneName);
        }
    }
}
