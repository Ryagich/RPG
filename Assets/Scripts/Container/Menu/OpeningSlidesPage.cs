using System;
using GameModes;
using Localization;
using TMPro;
using UI;
using UI.Configs;
using UI.Pages;
using UI.UIElements;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Localization;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Container.Menu
{
    public sealed class OpeningSlidesPage : BasePage, ITickable
    {
        private enum HintPhase
        {
            Hidden,
            Restoring,
            Showing,
            Fading
        }

        private sealed class HintState
        {
            public float Alpha;
            public float StartAlpha;
            public float TargetAlpha;
            public float Duration;
            public float Elapsed;
            public HintPhase Phase;
        }

        private static bool hasBeenShownThisApplication;

        private readonly OpeningSlidesConfig config;
        private readonly StatsConfig visibilityConfig;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;

        private OpeningSlidesView view;
        private LocalizedString currentSlideText;
        private int slideIndex;
        private bool isDrawn;
        private bool isSpaceHeld;
        private bool isAutoAdvancePending;
        private float spaceHoldElapsed;
        private float autoAdvanceElapsed;
        private readonly HintState nextHint = new();
        private readonly HintState skipHint = new();

        public OpeningSlidesPage(
            UIConfig uiConfig,
            StatsConfig visibilityConfig,
            Canvas canvas,
            IObjectResolver resolver)
        {
            config = uiConfig.OpeningSlidesConfig;
            this.visibilityConfig = visibilityConfig;
            canvasRect = canvas.GetComponent<RectTransform>();
            this.resolver = resolver;
        }

        public static bool ShouldShowAtApplicationStart => !hasBeenShownThisApplication;

        public override PageType Type { get; } = PageType.OpeningSlides;
        public event Action Completed;

        public override void Draw()
        {
            hasBeenShownThisApplication = true;
            if (config == null || config.ViewPrefab == null || config.Slides == null || config.Slides.Count == 0)
            {
                Debug.LogWarning("Opening slides are not configured. Opening the main menu.");
                Completed?.Invoke();
                return;
            }

            view = resolver.Instantiate(config.ViewPrefab, canvasRect);
            view.name = config.ViewPrefab.name;
            if (!HasRequiredReferences())
            {
                Finish();
                return;
            }

            isDrawn = true;
            slideIndex = 0;
            ShowSlide();
            BeginHintsAtFullAlpha();
        }

        public override void Hide()
        {
            isDrawn = false;
            isSpaceHeld = false;
            spaceHoldElapsed = 0f;
            CancelAutoAdvance();
            UnsubscribeFromSlideText();
            StopVoiceOver();
            if (view != null)
            {
                Object.Destroy(view.gameObject);
                view = null;
            }
        }

        public void Tick()
        {
            if (!isDrawn)
            {
                return;
            }

            var deltaTime = Time.unscaledDeltaTime;
            UpdateHints(deltaTime);
            UpdateVoiceOverState();
            ProcessInput(deltaTime);
            if (isDrawn)
            {
                UpdateAutoAdvance(deltaTime);
            }
        }

        private bool HasRequiredReferences()
        {
            if (view.SlideImage != null && view.FilmWearOverlay != null && view.SlideText != null && view.NextText != null && view.SkipText != null && view.VoiceOverSource != null)
            {
                return true;
            }

            Debug.LogError("OpeningSlidesView must reference SlideImage, FilmWearOverlay, SlideText, NextText, SkipText, and VoiceOverSource.", view);
            return false;
        }

        private void ShowSlide()
        {
            CancelAutoAdvance();
            var slide = config.Slides[slideIndex];
            view.SlideImage.sprite = slide.Image;
            SetSlideText(slide.Text);
            StopVoiceOver();
            if (slide.VoiceOver == null)
            {
                OnVoiceOverFinished();
                return;
            }

            view.VoiceOverSource.clip = slide.VoiceOver;
            view.VoiceOverSource.Play();
        }

        private void SetSlideText(LocalizedString slideText)
        {
            UnsubscribeFromSlideText();
            view.SlideText.text = string.Empty;
            if (slideText == null)
            {
                return;
            }

            currentSlideText = slideText;
            currentSlideText.StringChanged += OnSlideTextChanged;
        }

        private void UnsubscribeFromSlideText()
        {
            if (currentSlideText != null)
            {
                currentSlideText.StringChanged -= OnSlideTextChanged;
                currentSlideText = null;
            }
        }

        private void OnSlideTextChanged(string localizedText)
        {
            if (view?.SlideText != null)
            {
                view.SlideText.text = localizedText;
            }
        }

        private void UpdateVoiceOverState()
        {
            if (view.VoiceOverSource.clip != null && !view.VoiceOverSource.isPlaying)
            {
                view.VoiceOverSource.clip = null;
                OnVoiceOverFinished();
            }
        }

        private void ProcessInput(float deltaTime)
        {
            var isLastSlide = slideIndex == config.Slides.Count - 1;
            var keyboard = Keyboard.current;
            var spacePressed = keyboard?.spaceKey.wasPressedThisFrame == true;
            var spaceReleased = keyboard?.spaceKey.wasReleasedThisFrame == true;
            var nonSpaceButtonPressed = HasNonSpaceButtonPressed();

            if (spacePressed || spaceReleased || nonSpaceButtonPressed)
            {
                CancelAutoAdvance();
            }

            if (!isLastSlide && spacePressed)
            {
                isSpaceHeld = true;
                spaceHoldElapsed = 0f;
                ShowHints();
            }

            if (isSpaceHeld)
            {
                spaceHoldElapsed += deltaTime;
                if (spaceHoldElapsed >= config.HoldSpaceToSkipTime)
                {
                    Finish();
                    return;
                }

                if (spaceReleased)
                {
                    isSpaceHeld = false;
                    HandleNextInput();
                }
            }
            else if (isLastSlide && spacePressed)
            {
                HandleNextInput();
            }

            if (nonSpaceButtonPressed)
            {
                HandleNextInput();
            }
        }

        private void HandleNextInput()
        {
            if (nextHint.Phase is HintPhase.Hidden or HintPhase.Fading or HintPhase.Restoring)
            {
                ShowNextHint();
                return;
            }

            AdvanceSlide();
        }

        private void AdvanceSlide()
        {
            CancelAutoAdvance();
            StopVoiceOver();
            if (slideIndex >= config.Slides.Count - 1)
            {
                Finish();
                return;
            }

            slideIndex++;
            HideHintsImmediately();
            ShowSlide();
        }

        private void OnVoiceOverFinished()
        {
            ShowNextHint();
            isAutoAdvancePending = true;
            autoAdvanceElapsed = 0f;
        }

        private void UpdateAutoAdvance(float deltaTime)
        {
            if (!isAutoAdvancePending)
            {
                return;
            }

            autoAdvanceElapsed += deltaTime;
            if (autoAdvanceElapsed < config.AutoAdvanceDelay)
            {
                return;
            }

            AdvanceSlide();
        }

        private void CancelAutoAdvance()
        {
            isAutoAdvancePending = false;
            autoAdvanceElapsed = 0f;
        }

        private void Finish()
        {
            if (!isDrawn && view == null)
            {
                return;
            }

            Hide();
            Completed?.Invoke();
        }

        private void StopVoiceOver()
        {
            if (view?.VoiceOverSource == null)
            {
                return;
            }

            view.VoiceOverSource.Stop();
            view.VoiceOverSource.clip = null;
        }

        private void BeginHintsAtFullAlpha()
        {
            BeginHintAtFullAlpha(nextHint);
            BeginHintAtFullAlpha(skipHint);
            ApplyHintAlphas();
        }

        private void HideHintsImmediately()
        {
            HideHintImmediately(nextHint);
            HideHintImmediately(skipHint);
            ApplyHintAlphas();
        }

        private static void HideHintImmediately(HintState hint)
        {
            hint.Alpha = 0f;
            hint.Elapsed = 0f;
            hint.Duration = 0f;
            hint.Phase = HintPhase.Hidden;
        }

        private void ShowNextHint()
        {
            RestoreHint(nextHint);
        }

        private void ShowHints()
        {
            RestoreHint(nextHint);
            RestoreHint(skipHint);
        }

        private void BeginHintAtFullAlpha(HintState hint)
        {
            hint.Alpha = 1f;
            hint.Phase = HintPhase.Showing;
            hint.Duration = config.InitialHintsShowTime;
            hint.Elapsed = 0f;
        }

        private void RestoreHint(HintState hint)
        {
            StartHintPhase(hint, hint.Alpha, 1f, GetRestoreDuration(hint.Alpha), HintPhase.Restoring);
        }

        private void UpdateHints(float deltaTime)
        {
            UpdateHint(nextHint, deltaTime);
            UpdateHint(skipHint, deltaTime);
            ApplyHintAlphas();
        }

        private void UpdateHint(HintState hint, float deltaTime)
        {
            switch (hint.Phase)
            {
                case HintPhase.Restoring:
                    hint.Alpha = AdvanceAlpha(hint, deltaTime);
                    if (hint.Elapsed >= hint.Duration)
                    {
                        hint.Alpha = 1f;
                        hint.Phase = HintPhase.Showing;
                        hint.Duration = visibilityConfig.ShowTime;
                        hint.Elapsed = 0f;
                    }
                    break;
                case HintPhase.Showing:
                    hint.Elapsed += deltaTime;
                    if (hint.Elapsed >= hint.Duration)
                    {
                        StartHintPhase(hint, 1f, 0f, visibilityConfig.FadeOutTime, HintPhase.Fading);
                    }
                    break;
                case HintPhase.Fading:
                    hint.Alpha = AdvanceAlpha(hint, deltaTime);
                    if (hint.Elapsed >= hint.Duration)
                    {
                        hint.Alpha = 0f;
                        hint.Phase = HintPhase.Hidden;
                    }
                    break;
            }
        }

        private static void StartHintPhase(HintState hint, float startAlpha, float targetAlpha, float duration, HintPhase phase)
        {
            hint.StartAlpha = startAlpha;
            hint.TargetAlpha = targetAlpha;
            hint.Duration = duration;
            hint.Elapsed = 0f;
            hint.Phase = phase;
            if (duration <= 0f)
            {
                hint.Alpha = targetAlpha;
            }
        }

        private static float AdvanceAlpha(HintState hint, float deltaTime)
        {
            if (hint.Duration <= 0f)
            {
                hint.Elapsed = 0f;
                return hint.TargetAlpha;
            }

            hint.Elapsed = Mathf.Min(hint.Elapsed + deltaTime, hint.Duration);
            return Mathf.Lerp(hint.StartAlpha, hint.TargetAlpha, hint.Elapsed / hint.Duration);
        }

        private float GetRestoreDuration(float currentAlpha) => visibilityConfig.AlphaRestoreTime * Mathf.Clamp01(1f - currentAlpha);

        private void ApplyHintAlphas()
        {
            SetTextAlpha(view?.NextText, nextHint.Alpha);
            SetTextAlpha(view?.SkipText, skipHint.Alpha);
        }

        private static void SetTextAlpha(TMP_Text text, float alpha)
        {
            if (text == null)
            {
                return;
            }

            var color = text.color;
            color.a = Mathf.Clamp01(alpha);
            text.color = color;
        }

        private static bool HasNonSpaceButtonPressed()
        {
            foreach (var device in InputSystem.devices)
            {
                foreach (var control in device.allControls)
                {
                    if (control is not ButtonControl { wasPressedThisFrame: true })
                    {
                        continue;
                    }

                    if (device is Keyboard && control == Keyboard.current?.spaceKey)
                    {
                        continue;
                    }

                    return true;
                }
            }

            return false;
        }
    }
}
