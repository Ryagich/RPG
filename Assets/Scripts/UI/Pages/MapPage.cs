using System.Collections.Generic;
using GameModes;
using Localization;
using MessagePipe;
using Messages;
using Quests;
using Stats;
using UI.Configs;
using UI.Map;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace UI.Pages
{
    public sealed class MapPage : BasePage, ITickable
    {
        public override PageType Type { get; } = PageType.Map;

        private readonly UIConfig uiConfig;
        private readonly MapConfig mapConfig;
        private readonly LocalizationConfig localizationConfig;
        private readonly QuestController questController;
        private readonly Transform playerTransform;
        private readonly Animator playerAnimator;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;

        private RectTransform mapRect;
        private Title title;
        private MapScrollController mapScrollController;
        private RectTransform popupParentRect;
        private RectTransform popupRect;
        private QuestProgress hoveredQuestProgress;
        private float hoverPopupElapsed;
        private readonly List<MapQuestMarkerData> questMarkers = new();

        public MapPage(
            UIConfig uiConfig,
            MapConfig mapConfig,
            LocalizationConfig localizationConfig,
            QuestController questController,
            Transform playerTransform,
            Animator playerAnimator,
            Canvas canvas,
            IObjectResolver resolver,
            IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher)
        {
            this.uiConfig = uiConfig;
            this.mapConfig = mapConfig;
            this.localizationConfig = localizationConfig;
            this.questController = questController;
            this.playerTransform = playerTransform;
            this.playerAnimator = playerAnimator;
            this.resolver = resolver;
            this.changeGameModeRequestPublisher = changeGameModeRequestPublisher;
            canvasRect = canvas.GetComponent<RectTransform>();
        }

        public override void Draw()
        {
            if (uiConfig.Map == null)
            {
                Debug.LogError("Map prefab is not assigned in UIConfig.");
                changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
                return;
            }

            mapRect = resolver.Instantiate(uiConfig.Map, canvasRect);
            mapRect.name = $"{uiConfig.Map.name} | {Type}";
            ConfigureUnscaledAnimators(mapRect.gameObject);

            MapHolder mapHolder = mapRect.GetComponent<MapHolder>();
            if (mapHolder == null || mapHolder.MapScroll == null || mapHolder.Title == null)
            {
                Debug.LogError("Map prefab is missing MapHolder references.");
                return;
            }

            title = mapHolder.Title;
            if (title.TitleName != null)
            {
                title.TitleName.text = localizationConfig.WorldMapTitle.GetLocalizedStringCached();
            }

            title.ExitButton?.onClick.AddListener(Close);
            title.LeftButton?.onClick.AddListener(OpenQuestPage);
            title.RightButton?.onClick.AddListener(OpenQuestPage);

            if (uiConfig.CharacterIcon == null || mapHolder.MapScroll.content == null)
            {
                return;
            }

            CharacterIcon characterIcon = resolver.Instantiate(uiConfig.CharacterIcon, mapHolder.MapScroll.content);
            characterIcon.name = $"{uiConfig.CharacterIcon.name} | {Type}";

            mapScrollController = mapHolder.MapScroll.GetComponent<MapScrollController>();
            if (mapScrollController == null)
            {
                Debug.LogError("Map prefab is missing MapScrollController.");
                return;
            }

            mapScrollController.Initialize(
                mapHolder.MapScroll,
                characterIcon,
                playerTransform,
                playerAnimator != null ? playerAnimator.transform : playerTransform,
                mapConfig);

            FillQuestMarkers(mapScrollController);
            popupParentRect = mapRect;
        }

        public override void Hide()
        {
            CloseMapPopup();
            ResetHoverPopupState();

            if (title != null)
            {
                title.ExitButton?.onClick.RemoveListener(Close);
                title.LeftButton?.onClick.RemoveListener(OpenQuestPage);
                title.RightButton?.onClick.RemoveListener(OpenQuestPage);
                title = null;
            }

            if (mapRect != null)
            {
                Object.Destroy(mapRect.gameObject);
                mapRect = null;
            }

            questMarkers.Clear();
            mapScrollController = null;
            popupParentRect = null;
        }

        public void Tick()
        {
            if (mapRect == null || mapScrollController == null || Pointer.current == null)
            {
                ResetHoverPopupState();
                CloseMapPopup();
                return;
            }

            Vector2 screenPoint = Pointer.current.position.ReadValue();
            if (!mapScrollController.TryGetQuestMarkerAtScreenPoint(screenPoint, out QuestProgress questProgress))
            {
                ResetHoverPopupState();
                CloseMapPopup();
                return;
            }

            bool targetChanged = !ReferenceEquals(hoveredQuestProgress, questProgress);
            if (targetChanged)
            {
                hoveredQuestProgress = questProgress;
                hoverPopupElapsed = 0f;
                CloseMapPopup();
            }
            else
            {
                hoverPopupElapsed += Time.unscaledDeltaTime;
            }

            if (popupRect != null)
            {
                PageUiUtilities.UpdatePopupPosition(popupRect, popupParentRect, GetEventCamera(), screenPoint);
                return;
            }

            if (uiConfig.MapIconsConfig != null && hoverPopupElapsed >= uiConfig.MapIconsConfig.PopupHoverDelaySeconds)
            {
                TryOpenQuestPopup(questProgress, screenPoint);
            }
        }

        private void Close()
        {
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
        }

        private void OpenQuestPage()
        {
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Quest));
        }

        private void FillQuestMarkers(MapScrollController mapScrollController)
        {
            questMarkers.Clear();

            if (mapScrollController == null || uiConfig.MapIcon == null || uiConfig.MapIconsConfig == null || questController == null)
            {
                return;
            }

            foreach (QuestProgress questProgress in questController.Progress)
            {
                Transform mapTarget = questProgress?.CurrentNode?.MapTarget;
                if (questProgress == null || questProgress.IsCompleted || mapTarget == null)
                {
                    continue;
                }

                questMarkers.Add(new MapQuestMarkerData(mapTarget, questProgress));
            }

            mapScrollController.SetQuestMarkers(uiConfig.MapIcon, uiConfig.MapIconsConfig, questMarkers);
        }

        private void TryOpenQuestPopup(QuestProgress questProgress, Vector2 screenPoint)
        {
            if (questProgress?.QuestGraph == null || questProgress.CurrentNode == null || popupParentRect == null)
            {
                return;
            }

            if (uiConfig.QuestPopup == null || uiConfig.MapIconsConfig == null ||
                !uiConfig.MapIconsConfig.TryGetIcon(MapIconsConfig.QuestIconName, out MapIconDefinition questIcon))
            {
                Debug.LogError("Quest Popup or Quest icon definition is not assigned.");
                return;
            }

            popupRect = resolver.Instantiate(uiConfig.QuestPopup, popupParentRect);
            popupRect.name = $"{uiConfig.QuestPopup.name} | Map Quest Popup";
            if (popupRect == null)
            {
                return;
            }

            PageUiUtilities.SetPopupRaycastState(popupRect, false);
            if (!PageUiUtilities.FillMapQuestPopup(
                    popupRect,
                    questProgress.QuestGraph.Title.GetLocalizedStringCached(),
                    questProgress.CurrentNode.Name.GetLocalizedStringCached(),
                    questProgress.CurrentNode.Description.GetLocalizedStringCached(),
                    questIcon.Sprite,
                    questIcon.Color))
            {
                CloseMapPopup();
                return;
            }

            Canvas.ForceUpdateCanvases();
            PageUiUtilities.UpdatePopupPosition(popupRect, popupParentRect, GetEventCamera(), screenPoint);
        }

        private void ResetHoverPopupState()
        {
            hoveredQuestProgress = null;
            hoverPopupElapsed = 0f;
        }

        private void CloseMapPopup()
        {
            if (popupRect != null)
            {
                Object.Destroy(popupRect.gameObject);
            }

            popupRect = null;
        }

        private Camera GetEventCamera()
        {
            Canvas canvas = mapRect != null ? mapRect.GetComponentInParent<Canvas>() : null;
            return canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        private static void ConfigureUnscaledAnimators(GameObject root)
        {
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
            {
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }
        }
    }
}
