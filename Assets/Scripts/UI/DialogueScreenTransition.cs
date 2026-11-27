using System;
using Dialogue;
using Input;
using MessagePipe;
using Messages;
using UI.Configs;
using UnityEngine;
using UnityEngine.UI;
using VContainer.Unity;

namespace UI
{
    /// <summary>
    /// Owns only the visual transition for a dialogue session. DialogueContext remains the
    /// source of truth for the dialogue lifetime, while the camera keeps its own transition.
    /// </summary>
    public sealed class DialogueScreenTransition : IStartable, ITickable, IDisposable
    {
        private enum Phase
        {
            Hidden,
            FadingIn,
            Holding,
            FadingOut
        }

        private readonly DialogueContext dialogueContext;
        private readonly PlayerInputLock playerInputLock;
        private readonly IPublisher<DialogueScreenFullyObscuredMessage> fullyObscuredPublisher;
        private readonly DialogueScreenTransitionConfig config;
        private readonly Canvas canvas;
        private Image overlay;
        private Phase phase;
        private float elapsed;

        public DialogueScreenTransition(
            DialogueContext dialogueContext,
            PlayerInputLock playerInputLock,
            IPublisher<DialogueScreenFullyObscuredMessage> fullyObscuredPublisher,
            DialogueScreenTransitionConfig config,
            Canvas canvas)
        {
            this.dialogueContext = dialogueContext;
            this.playerInputLock = playerInputLock;
            this.fullyObscuredPublisher = fullyObscuredPublisher;
            this.config = config;
            this.canvas = canvas;
        }

        public void Start()
        {
            if (config == null || canvas == null)
            {
                return;
            }

            CreateOverlay();
            dialogueContext.Opened += RestartTransition;
            dialogueContext.Closed += RestartTransition;

            if (dialogueContext.CurrentTarget != null)
            {
                RestartTransition();
            }
        }

        public void Tick()
        {
            if (overlay == null || phase == Phase.Hidden)
            {
                return;
            }

            elapsed += Time.unscaledDeltaTime;
            var duration = GetPhaseDuration();
            if (duration <= 0f || elapsed >= duration)
            {
                AdvancePhase();
            }

            ApplyAlpha();
        }

        public void Dispose()
        {
            dialogueContext.Opened -= RestartTransition;
            dialogueContext.Closed -= RestartTransition;

            if (overlay != null)
            {
                UnityEngine.Object.Destroy(overlay.gameObject);
                overlay = null;
            }

            playerInputLock.SetLocked(this, false);
        }

        private void RestartTransition()
        {
            if (overlay == null)
            {
                return;
            }

            playerInputLock.SetLocked(this, true);
            overlay.gameObject.SetActive(true);
            phase = Phase.FadingIn;
            elapsed = 0f;
            ApplyAlpha();
        }

        private void CreateOverlay()
        {
            var overlayObject = new GameObject(
                "Dialogue Screen Transition",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster),
                typeof(Image));
            overlayObject.transform.SetParent(canvas.transform, false);

            var overlayCanvas = overlayObject.GetComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 1000;

            var overlayRect = (RectTransform)overlayObject.transform;
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            overlay = overlayObject.GetComponent<Image>();
            overlay.raycastTarget = true;
            overlay.color = Color.clear;
            overlayObject.SetActive(false);
        }

        private float GetPhaseDuration()
        {
            return phase switch
            {
                Phase.FadingIn => config.FadeInTime,
                Phase.Holding => config.HoldTime,
                Phase.FadingOut => config.FadeOutTime,
                _ => 0f
            };
        }

        private void AdvancePhase()
        {
            var previousPhase = phase;
            elapsed = 0f;
            phase = phase switch
            {
                Phase.FadingIn => Phase.Holding,
                Phase.Holding => Phase.FadingOut,
                _ => Phase.Hidden
            };

            if (previousPhase == Phase.FadingIn && phase == Phase.Holding)
            {
                fullyObscuredPublisher.Publish(new DialogueScreenFullyObscuredMessage());
            }

            if (phase == Phase.Hidden)
            {
                overlay.gameObject.SetActive(false);
                playerInputLock.SetLocked(this, false);
            }
        }

        private void ApplyAlpha()
        {
            var duration = GetPhaseDuration();
            var normalizedTime = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            var alpha = phase switch
            {
                Phase.FadingIn => normalizedTime,
                Phase.Holding => 1f,
                Phase.FadingOut => 1f - normalizedTime,
                _ => 0f
            };

            overlay.color = new Color(0f, 0f, 0f, alpha * Mathf.Clamp01(config.MaximumOpacity));
        }
    }
}
