using Interactable;
using Stats;
using UI.Configs;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace UI.Pages
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class MainPage : BasePage
    {
        private enum HpFillMode
        {
            Synced,
            FillAnimated,
            ChangedFillAnimated
        }

        private readonly UIConfig uiConfig;
        private readonly StatsConfig statsConfig;
        private readonly StatsController statsController;
        private readonly StatFiller hpFiller;
        private readonly PlayerInteractableLogic playerInteractableLogic;
        private readonly ItemHolderInteractableLogic itemHolderInteractableLogic;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private readonly CompositeDisposable drawDisposables = new();

        public override PageType Type { get; } = PageType.MainGame;

        private RectTransform contentRect;
        private StatsHolder statsHolder;
        private InteractableInterface interactableInterface;
        private BeatingHeart beatingHeart;
        private float lastHpTarget;
        private HpFillMode hpFillMode;

        public MainPage
            (
                UIConfig uiConfig,
                StatsConfig statsConfig,
                StatsController statsController,
                StatFiller hpFiller,
                Canvas canvas,
                PlayerInteractableLogic playerInteractableLogic,
                ItemHolderInteractableLogic itemHolderInteractableLogic,
                IObjectResolver resolver
            )
        {
            this.resolver = resolver;
            this.uiConfig = uiConfig;
            this.statsConfig = statsConfig;
            this.statsController = statsController;
            this.hpFiller = hpFiller;
            this.playerInteractableLogic = playerInteractableLogic;
            this.itemHolderInteractableLogic = itemHolderInteractableLogic;

            canvasRect = canvas.GetComponent<RectTransform>();
        }

        public override void Draw()
        {
            contentRect = resolver.Instantiate(uiConfig.ContentPref, canvasRect);
            contentRect.name = $"{uiConfig.ContentPref.name} | {Type}";

            statsHolder = resolver.Instantiate(uiConfig.StatsHolder, contentRect);
            statsHolder.name = $"{uiConfig.StatsHolder.name} | {Type}";

            interactableInterface = new InteractableInterface
                (
                    uiConfig,
                    contentRect,
                    playerInteractableLogic,
                    itemHolderInteractableLogic
                );

            lastHpTarget = statsController.Hp.Value.Value;
            hpFillMode = HpFillMode.Synced;

            hpFiller.Current
                    .Subscribe(_ => RefreshHpFill())
                    .AddTo(drawDisposables);
            statsController.Hp.Value
                           .Subscribe(OnHpTargetChanged)
                           .AddTo(drawDisposables);

            RefreshHpFill();
            beatingHeart = new BeatingHeart(statsConfig, statsController.Hp, hpFiller, statsHolder.HPHolder);
        }

        public override void Hide()
        {
            beatingHeart?.Dispose();
            beatingHeart = null;

            drawDisposables.Clear();

            interactableInterface?.Dispose();
            interactableInterface = null;

            statsHolder = null;

            if (contentRect)
            {
                Object.Destroy(contentRect.gameObject);
            }
        }

        private void RefreshHpFill()
        {
            var hpHolder = statsHolder?.HPHolder;
            if (hpHolder == null || hpHolder.Fill == null || hpHolder.ChangedFill == null)
            {
                return;
            }

            var normalizedCurrent = GetNormalizedHp(hpFiller.Current.Value);
            var normalizedTarget = GetNormalizedHp(statsController.Hp.Value.Value);

            switch (ResolveHpFillDirection())
            {
                case HpFillMode.FillAnimated:
                    hpHolder.Fill.fillAmount = normalizedCurrent;
                    hpHolder.ChangedFill.fillAmount = normalizedTarget;
                    break;
                case HpFillMode.ChangedFillAnimated:
                    hpHolder.Fill.fillAmount = normalizedTarget;
                    hpHolder.ChangedFill.fillAmount = normalizedCurrent;
                    break;
                default:
                    hpHolder.Fill.fillAmount = normalizedTarget;
                    hpHolder.ChangedFill.fillAmount = normalizedTarget;
                    break;
            }
        }

        private float GetNormalizedHp(float value)
        {
            var maxHp = statsController.Hp.Max;
            return Mathf.Approximately(maxHp, 0f) ? 0f : value / maxHp;
        }

        private void OnHpTargetChanged(float newTarget)
        {
            SelectHpFillMode(newTarget);
            lastHpTarget = newTarget;
            RefreshHpFill();
        }

        private HpFillMode ResolveHpFillDirection()
        {
            if (!Mathf.Approximately(hpFiller.Current.Value, statsController.Hp.Value.Value))
            {
                return hpFillMode;
            }

            return HpFillMode.Synced;
        }

        private void SelectHpFillMode(float newTarget)
        {
            var hpHolder = statsHolder?.HPHolder;
            if (hpHolder == null || hpHolder.Fill == null || hpHolder.ChangedFill == null)
            {
                return;
            }

            var target = GetNormalizedHp(newTarget);
            var fill = hpHolder.Fill.fillAmount;
            var changedFill = hpHolder.ChangedFill.fillAmount;

            var shouldAnimateFill = target > fill;
            var shouldAnimateChangedFill = target < changedFill;

            var nextMode = hpFillMode;
            if (shouldAnimateFill && shouldAnimateChangedFill)
            {
                nextMode = hpFillMode == HpFillMode.Synced
                    ? newTarget >= lastHpTarget
                        ? HpFillMode.FillAnimated
                        : HpFillMode.ChangedFillAnimated
                    : hpFillMode;
            }
            else if (shouldAnimateFill)
            {
                nextMode = HpFillMode.FillAnimated;
            }
            else if (shouldAnimateChangedFill)
            {
                nextMode = HpFillMode.ChangedFillAnimated;
            }
            else
            {
                nextMode = HpFillMode.Synced;
            }

            RebaseAnimatedFill(nextMode, fill, changedFill, target);
            hpFillMode = nextMode;
        }

        private void RebaseAnimatedFill(HpFillMode nextMode, float fill, float changedFill, float target)
        {
            var currentVisual = nextMode switch
            {
                HpFillMode.FillAnimated => fill,
                HpFillMode.ChangedFillAnimated => changedFill,
                _ => target
            };

            hpFiller.Current.Value = currentVisual * statsController.Hp.Max;
        }
    }
}
