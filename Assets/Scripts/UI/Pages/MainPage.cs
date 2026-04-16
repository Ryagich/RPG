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

            hpFiller.Current
                    .Subscribe(ApplyHpFill)
                    .AddTo(drawDisposables);
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

        private void ApplyHpFill(float value)
        {
            Debug.Log("ApplyHpFill");
            var normalizedValue = Mathf.Approximately(statsController.Hp.Max, 0f) ? 0f : value / statsController.Hp.Max;
            statsHolder.HPHolder.Fill.fillAmount = normalizedValue;
        }
    }
}
