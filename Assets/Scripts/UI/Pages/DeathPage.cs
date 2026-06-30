using GameModes;
using Loading;
using TMPro;
using UI.Configs;
using UI.UIElements;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace UI.Pages
{
    public sealed class DeathPage : BasePage, ITickable
    {
        private const string MenuSceneName = "Menu";

        private readonly UIConfig uiConfig;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private readonly SceneLoadingService sceneLoadingService;

        private RectTransform contentRect;
        private DeadUI deadUi;
        private Image bloodScreen;
        private TMP_Text clickText;
        private bool isDrawn;
        private bool isLeaving;

        public DeathPage(
            UIConfig uiConfig,
            Canvas canvas,
            IObjectResolver resolver,
            SceneLoadingService sceneLoadingService)
        {
            this.uiConfig = uiConfig;
            this.resolver = resolver;
            this.sceneLoadingService = sceneLoadingService;
            canvasRect = canvas.GetComponent<RectTransform>();
        }

        public override PageType Type { get; } = PageType.Death;

        public override void Draw()
        {
            isDrawn = true;
            isLeaving = false;

            contentRect = resolver.Instantiate(uiConfig.ContentPref, canvasRect);
            contentRect.name = $"{uiConfig.ContentPref.name} | {Type}";

            bloodScreen = PageUiUtilities.CreateBloodScreen(uiConfig, resolver, contentRect, Type);
            ApplyMaxBloodScreen();

            if (uiConfig.DeadUI == null)
            {
                return;
            }

            deadUi = resolver.Instantiate(uiConfig.DeadUI, contentRect);
            deadUi.name = $"{uiConfig.DeadUI.name} | {Type}";
            clickText = deadUi.ClickText;
        }

        public override void Hide()
        {
            isDrawn = false;
            clickText = null;
            deadUi = null;
            bloodScreen = null;

            if (contentRect)
            {
                Object.Destroy(contentRect.gameObject);
            }
        }

        public void Tick()
        {
            if (!isDrawn || isLeaving)
            {
                return;
            }

            UpdateClickTextBlink();

            if (HasAnyInput())
            {
                isLeaving = true;
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                sceneLoadingService.Load(MenuSceneName);
            }
        }

        private void UpdateClickTextBlink()
        {
            if (clickText == null)
            {
                return;
            }

            var config = uiConfig.ClickTextBlinkingConfig;
            var blinkSpeed = config != null ? config.BlinkSpeed : 1.25f;
            var minAlpha = config != null ? config.MinAlpha : 0.45f;
            var maxAlpha = config != null ? config.MaxAlpha : 0.95f;
            var pulse = (Mathf.Sin(Time.unscaledTime * blinkSpeed) + 1f) * 0.5f;
            var color = clickText.color;
            color.a = Mathf.Lerp(minAlpha, maxAlpha, pulse);
            clickText.color = color;
        }

        private void ApplyMaxBloodScreen()
        {
            if (bloodScreen == null)
            {
                return;
            }

            bloodScreen.raycastTarget = false;
            bloodScreen.transform.localScale = Vector3.one;
            var color = bloodScreen.color;
            color.a = 1f;
            bloodScreen.color = color;
        }

        private static bool HasAnyInput()
        {
            return Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame
                || Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame
                                          || Mouse.current.rightButton.wasPressedThisFrame
                                          || Mouse.current.middleButton.wasPressedThisFrame)
                || Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame;
        }
    }
}
