using System.Collections;
using TMPro;
using Loading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

namespace UI.UIElements
{
    public sealed class LoadSceneUI : MonoBehaviour
    {
        private static readonly string[] AnimationFrames = { "-", "/", "|", "\\" };

        [field: SerializeField] public TMP_Text SimpleAnimationText { get; private set; }
        [field: SerializeField] public TMP_Text PercentOfLoadText { get; private set; }
        [field: SerializeField] public Image Fill { get; private set; }

        private LoadSceneConfig config;
        private SceneLoadingService sceneLoadingService;
        private AsyncOperation loadOperation;
        private float animationTimer;
        private int animationFrameIndex;
        private bool isReadyToActivate;

        [Inject]
        public void Construct(LoadSceneConfig loadSceneConfig, SceneLoadingService loadingService)
        {
            config = loadSceneConfig;
            sceneLoadingService = loadingService;
        }

        private void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true;

            if (config == null || sceneLoadingService == null)
            {
                Debug.LogError("LoadSceneUI was not initialized by LoadSceneLifetimeScope.", this);
                return;
            }

            ResetUi();

            if (!sceneLoadingService.HasPendingRequest)
            {
                sceneLoadingService.PrepareDirectLoad(config.MenuSceneName, false);
            }

            StartCoroutine(StartLoadingAfterFirstFrame());
        }

        private IEnumerator StartLoadingAfterFirstFrame()
        {
            yield return null;
            StartAsyncLoad();
        }

        private void StartAsyncLoad()
        {
            loadOperation = SceneManager.LoadSceneAsync(sceneLoadingService.TargetSceneName);
            if (loadOperation == null)
            {
                Debug.LogError($"Failed to start loading scene '{sceneLoadingService.TargetSceneName}'.");
                return;
            }

            loadOperation.allowSceneActivation = false;
        }

        private void Update()
        {
            if (loadOperation == null)
            {
                return;
            }

            float normalizedProgress = Mathf.Clamp01(loadOperation.progress / 0.9f);
            UpdateProgress(normalizedProgress);

            if (normalizedProgress < 1f)
            {
                UpdateSimpleAnimation();
                return;
            }

            if (!isReadyToActivate)
            {
                isReadyToActivate = true;
                StopSimpleAnimation();
            }

            if (!sceneLoadingService.WaitForInputBeforeActivation)
            {
                ActivateLoadedScene();
                return;
            }

            UpdateReadyPrompt();
            if (HasAnyInput())
            {
                ActivateLoadedScene();
            }
        }

        private void ResetUi()
        {
            if (SimpleAnimationText != null)
            {
                SimpleAnimationText.text = AnimationFrames[0];
            }

            if (PercentOfLoadText != null)
            {
                PercentOfLoadText.text = "0%";
                SetTextAlpha(PercentOfLoadText, 1f);
            }

            if (Fill != null)
            {
                Fill.type = Image.Type.Filled;
                Fill.fillMethod = Image.FillMethod.Horizontal;
                Fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                Fill.fillAmount = 0f;
            }
        }

        private void UpdateProgress(float normalizedProgress)
        {
            if (Fill != null)
            {
                Fill.fillAmount = normalizedProgress;
            }

            if (!isReadyToActivate && PercentOfLoadText != null)
            {
                PercentOfLoadText.text = $"{Mathf.RoundToInt(normalizedProgress * 100f)}%";
                SetTextAlpha(PercentOfLoadText, 1f);
            }
        }

        private void UpdateSimpleAnimation()
        {
            if (SimpleAnimationText == null)
            {
                return;
            }

            animationTimer += Time.unscaledDeltaTime;
            float frameSeconds = config != null ? config.SimpleAnimationFrameSeconds : 0.18f;
            if (animationTimer < frameSeconds)
            {
                return;
            }

            animationTimer = 0f;
            animationFrameIndex = (animationFrameIndex + 1) % AnimationFrames.Length;
            SimpleAnimationText.text = AnimationFrames[animationFrameIndex];
        }

        private void StopSimpleAnimation()
        {
            if (SimpleAnimationText != null)
            {
                SimpleAnimationText.text = string.Empty;
            }
        }

        private void UpdateReadyPrompt()
        {
            if (PercentOfLoadText == null)
            {
                return;
            }

            PercentOfLoadText.text = config != null ? config.PressAnyKeyText : "Press any key";
            float blinkSpeed = config != null ? config.ReadyTextBlinkSpeed : 1.25f;
            float minAlpha = config != null ? config.ReadyTextMinAlpha : 0.45f;
            float maxAlpha = config != null ? config.ReadyTextMaxAlpha : 0.95f;
            float pulse = (Mathf.Sin(Time.unscaledTime * blinkSpeed) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, pulse);
            SetTextAlpha(PercentOfLoadText, alpha);
        }

        private void ActivateLoadedScene()
        {
            sceneLoadingService.ClearRequest();
            loadOperation.allowSceneActivation = true;
        }

        private static bool HasAnyInput()
        {
            return Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame
                || Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame
                                          || Mouse.current.rightButton.wasPressedThisFrame
                                          || Mouse.current.middleButton.wasPressedThisFrame)
                || Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame;
        }

        private static void SetTextAlpha(TMP_Text text, float alpha)
        {
            Color color = text.color;
            color.a = alpha;
            text.color = color;
        }
    }
}
