using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI.Map
{
    public readonly struct MapQuestMarkerData
    {
        public MapQuestMarkerData(Transform targetTransform, Sprite sprite)
        {
            TargetTransform = targetTransform;
            Sprite = sprite;
        }

        public Transform TargetTransform { get; }
        public Sprite Sprite { get; }
    }

    public class MapScrollController : MonoBehaviour
    {
        private const float MinBoundsSize = 1f;
        private const float FallbackWorldSize = 100f;

        private ScrollRect mapScroll = null!;
        private RectTransform viewport = null!;
        private RectTransform content = null!;
        private RectTransform characterIconRect = null!;
        private RectTransform mapImageRect = null!;
        private CharacterIcon characterIcon = null!;
        private Transform playerTransform = null!;
        private Transform lookTransform = null!;
        private MapConfig mapConfig = null!;
        private Camera eventCamera;
        private Bounds worldBounds;
        private Vector2 baseContentSize;
        private float currentZoom = 1f;
        private bool isInitialized;
        private bool isDragging;
        private Vector3 lastMousePosition;
        private bool hasFocusTargetPosition;
        private Vector2 focusStartContentPosition;
        private Vector2 focusTargetContentPosition;
        private float focusElapsedTime;
        private readonly Vector3[] contentWorldCorners = new Vector3[4];
        private readonly List<QuestIconBinding> questIcons = new();

        public void Initialize(
            ScrollRect mapScroll,
            CharacterIcon characterIcon,
            Transform playerTransform,
            Transform lookTransform,
            MapConfig mapConfig)
        {
            this.mapScroll = mapScroll;
            this.characterIcon = characterIcon;
            this.playerTransform = playerTransform;
            this.lookTransform = lookTransform != null ? lookTransform : playerTransform;
            this.mapConfig = mapConfig;

            viewport = mapScroll.viewport != null ? mapScroll.viewport : mapScroll.GetComponent<RectTransform>();
            content = mapScroll.content;
            characterIconRect = characterIcon != null ? characterIcon.GetComponent<RectTransform>() : null;
            mapImageRect = content != null ? content.GetComponentInChildren<Image>(true)?.rectTransform : null;
            eventCamera = ResolveEventCamera();

            ConfigureScrollRect();
            ConfigureContentTransform();
            CacheWorldBounds();
            RecalculateBaseContentSize();

            currentZoom = Mathf.Clamp(currentZoom, mapConfig.MinZoom, mapConfig.MaxZoom);
            ApplyZoom();
            UpdateCharacterIcon();
            UpdateQuestIcons();
            isInitialized = true;
        }

        public void SetQuestMarkers(Image iconPrefab, IReadOnlyList<MapQuestMarkerData> markers)
        {
            ClearQuestIcons();

            if (iconPrefab == null || content == null || markers == null)
            {
                return;
            }

            foreach (MapQuestMarkerData marker in markers)
            {
                if (marker.TargetTransform == null)
                {
                    continue;
                }

                Image questIcon = Instantiate(iconPrefab, content);
                questIcon.name = $"{iconPrefab.name} | Quest";

                if (questIcon.transform is not RectTransform questIconRect)
                {
                    Destroy(questIcon.gameObject);
                    continue;
                }

                questIconRect.anchorMin = new Vector2(0f, 1f);
                questIconRect.anchorMax = new Vector2(0f, 1f);
                questIcons.Add(new QuestIconBinding(questIconRect, marker.TargetTransform));
            }

            UpdateQuestIcons();
        }

        private void Update()
        {
            if (!isInitialized || content == null || playerTransform == null)
            {
                return;
            }

            HandleZoom();
            HandleDragging();
            UpdateFocusMovement();
            UpdateCharacterIcon();
            UpdateQuestIcons();
        }

        public void FocusOnTarget(Transform targetTransform)
        {
            if (targetTransform == null || content == null || viewport == null)
            {
                return;
            }

            if (!TryGetContentPositionForWorldTarget(targetTransform.position, out Vector2 targetContentPosition))
            {
                return;
            }

            focusTargetContentPosition = GetClampedContentPosition(targetContentPosition);
            focusStartContentPosition = content.anchoredPosition;
            focusElapsedTime = 0f;
            hasFocusTargetPosition = true;
        }

        private void ConfigureScrollRect()
        {
            if (mapScroll == null)
            {
                return;
            }

            mapScroll.horizontal = false;
            mapScroll.vertical = false;
            mapScroll.inertia = false;
            mapScroll.scrollSensitivity = 0f;

            if (mapScroll.horizontalScrollbar != null)
            {
                mapScroll.horizontalScrollbar.gameObject.SetActive(false);
            }

            if (mapScroll.verticalScrollbar != null)
            {
                mapScroll.verticalScrollbar.gameObject.SetActive(false);
            }
        }

        private void ConfigureContentTransform()
        {
            if (content == null)
            {
                return;
            }

            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;

            if (mapImageRect != null)
            {
                mapImageRect.anchorMin = Vector2.zero;
                mapImageRect.anchorMax = Vector2.one;
                mapImageRect.pivot = new Vector2(0.5f, 0.5f);
                mapImageRect.anchoredPosition = Vector2.zero;
                mapImageRect.sizeDelta = Vector2.zero;
            }

            if (characterIconRect != null)
            {
                characterIconRect.anchorMin = new Vector2(0f, 1f);
                characterIconRect.anchorMax = new Vector2(0f, 1f);
            }
        }

        private void CacheWorldBounds()
        {
            Scene targetScene = playerTransform.gameObject.scene;
            Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            var hasBounds = false;
            Bounds bounds = default;

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                GameObject gameObject = renderer.gameObject;
                if (!gameObject.scene.IsValid() || gameObject.scene != targetScene)
                {
                    continue;
                }

                if (renderer.transform.IsChildOf(playerTransform) || gameObject.CompareTag("EditorOnly"))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            if (!hasBounds)
            {
                bounds = new Bounds(playerTransform.position, new Vector3(FallbackWorldSize, 0f, FallbackWorldSize));
            }

            if (bounds.size.x < MinBoundsSize)
            {
                bounds.Expand(new Vector3(MinBoundsSize - bounds.size.x, 0f, 0f));
            }

            if (bounds.size.z < MinBoundsSize)
            {
                bounds.Expand(new Vector3(0f, 0f, MinBoundsSize - bounds.size.z));
            }

            worldBounds = bounds;
        }

        private void RecalculateBaseContentSize()
        {
            if (viewport == null || content == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);

            Vector2 viewportSize = viewport.rect.size;
            if (viewportSize.x <= 0f || viewportSize.y <= 0f)
            {
                viewportSize = ((RectTransform)mapScroll.transform).rect.size;
            }

            float aspect = 1f;
            Image mapImage = mapImageRect != null ? mapImageRect.GetComponent<Image>() : null;
            if (mapImage != null && mapImage.sprite != null)
            {
                aspect = mapImage.sprite.rect.width / mapImage.sprite.rect.height;
            }
            else if (content.rect.height > 0f)
            {
                aspect = content.rect.width / content.rect.height;
            }

            float width = viewportSize.x;
            float height = width / aspect;
            if (height > viewportSize.y)
            {
                height = viewportSize.y;
                width = height * aspect;
            }

            baseContentSize = new Vector2(width, height);
        }

        private void HandleZoom()
        {
            if (!IsPointerInsideViewport())
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.001f)
            {
                return;
            }

            currentZoom = Mathf.Clamp(
                currentZoom + scroll * mapConfig.ZoomSpeed,
                mapConfig.MinZoom,
                mapConfig.MaxZoom);

            hasFocusTargetPosition = false;
            ApplyZoom(mouse.position.ReadValue());
        }

        private void HandleDragging()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                isDragging = false;
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame && IsPointerInsideViewport())
            {
                isDragging = true;
                hasFocusTargetPosition = false;
                lastMousePosition = mouse.position.ReadValue();
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                isDragging = false;
            }

            if (!isDragging || !mouse.leftButton.isPressed)
            {
                return;
            }

            Vector3 currentMousePosition = mouse.position.ReadValue();
            Vector2 delta = (Vector2)(currentMousePosition - lastMousePosition);
            lastMousePosition = currentMousePosition;

            content.anchoredPosition += delta;
            content.anchoredPosition = GetClampedContentPosition(content.anchoredPosition);
        }

        private void ApplyZoom(Vector2? focusScreenPoint = null)
        {
            if (content == null)
            {
                return;
            }

            Vector2 normalizedContentPoint = Vector2.zero;
            Vector2 focusViewportLocalPoint = Vector2.zero;
            bool hasFocusPoint =
                focusScreenPoint.HasValue &&
                TryGetNormalizedContentPoint(focusScreenPoint.Value, out normalizedContentPoint) &&
                TryGetViewportLocalPoint(focusScreenPoint.Value, out focusViewportLocalPoint);

            Vector2 targetSize = baseContentSize * currentZoom;
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetSize.x);
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetSize.y);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            if (hasFocusPoint &&
                TryGetContentScreenPoint(normalizedContentPoint, out Vector2 contentFocusScreenPoint) &&
                TryGetViewportLocalPoint(contentFocusScreenPoint, out Vector2 contentFocusViewportLocalPoint))
            {
                content.anchoredPosition += focusViewportLocalPoint - contentFocusViewportLocalPoint;
            }

            content.anchoredPosition = GetClampedContentPosition(content.anchoredPosition);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        private Vector2 GetClampedContentPosition(Vector2 position)
        {
            if (content == null || viewport == null)
            {
                return position;
            }

            Vector2 viewportSize = viewport.rect.size;
            Vector2 contentSize = content.rect.size;

            float x = contentSize.x <= viewportSize.x
                ? (viewportSize.x - contentSize.x) * 0.5f
                : Mathf.Clamp(position.x, viewportSize.x - contentSize.x, 0f);

            float y = contentSize.y <= viewportSize.y
                ? -(viewportSize.y - contentSize.y) * 0.5f
                : Mathf.Clamp(position.y, 0f, contentSize.y - viewportSize.y);

            return new Vector2(x, y);
        }

        private void UpdateCharacterIcon()
        {
            if (characterIconRect == null || characterIcon == null || playerTransform == null || content == null)
            {
                return;
            }

            Vector3 playerPosition = playerTransform.position;
            float normalizedX = Mathf.InverseLerp(worldBounds.min.x, worldBounds.max.x, playerPosition.x);
            float normalizedZ = Mathf.InverseLerp(worldBounds.min.z, worldBounds.max.z, playerPosition.z);
            Vector2 contentSize = content.rect.size;

            characterIconRect.anchoredPosition = new Vector2(
                normalizedX * contentSize.x,
                -((1f - normalizedZ) * contentSize.y));

            if (characterIcon.Direction == null)
            {
                return;
            }

            Vector3 forward = lookTransform != null ? lookTransform.forward : playerTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = playerTransform.forward;
                forward.y = 0f;
            }

            if (forward.sqrMagnitude < 0.0001f)
            {
                return;
            }

            float angle = Mathf.Atan2(forward.z, forward.x) * Mathf.Rad2Deg;
            characterIcon.Direction.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void UpdateQuestIcons()
        {
            if (content == null || questIcons.Count == 0)
            {
                return;
            }

            Vector2 contentSize = content.rect.size;
            for (var i = questIcons.Count - 1; i >= 0; i--)
            {
                QuestIconBinding questIcon = questIcons[i];
                if (questIcon.RectTransform == null)
                {
                    questIcons.RemoveAt(i);
                    continue;
                }

                if (questIcon.TargetTransform == null)
                {
                    questIcon.RectTransform.gameObject.SetActive(false);
                    continue;
                }

                questIcon.RectTransform.gameObject.SetActive(true);

                Vector3 targetPosition = questIcon.TargetTransform.position;
                float normalizedX = Mathf.InverseLerp(worldBounds.min.x, worldBounds.max.x, targetPosition.x);
                float normalizedZ = Mathf.InverseLerp(worldBounds.min.z, worldBounds.max.z, targetPosition.z);

                questIcon.RectTransform.anchoredPosition = new Vector2(
                    normalizedX * contentSize.x,
                    -((1f - normalizedZ) * contentSize.y));
            }
        }

        private void UpdateFocusMovement()
        {
            if (!hasFocusTargetPosition || content == null)
            {
                return;
            }

            float duration = Mathf.Max(0.01f, mapConfig.FocusMoveDuration);
            focusElapsedTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(focusElapsedTime / duration);
            content.anchoredPosition = Vector2.Lerp(focusStartContentPosition, focusTargetContentPosition, t);

            if (t >= 1f)
            {
                content.anchoredPosition = focusTargetContentPosition;
                hasFocusTargetPosition = false;
            }
        }

        private bool TryGetContentPositionForWorldTarget(Vector3 worldPosition, out Vector2 targetContentPosition)
        {
            targetContentPosition = Vector2.zero;
            if (content == null || viewport == null)
            {
                return false;
            }

            float normalizedX = Mathf.InverseLerp(worldBounds.min.x, worldBounds.max.x, worldPosition.x);
            float normalizedZ = Mathf.InverseLerp(worldBounds.min.z, worldBounds.max.z, worldPosition.z);
            Vector2 contentSize = content.rect.size;
            Vector2 viewportSize = viewport.rect.size;

            Vector2 anchoredTargetPosition = new Vector2(
                normalizedX * contentSize.x,
                -((1f - normalizedZ) * contentSize.y));

            targetContentPosition = new Vector2(
                viewportSize.x * 0.5f - anchoredTargetPosition.x,
                -anchoredTargetPosition.y - viewportSize.y * 0.5f);

            return true;
        }

        private void ClearQuestIcons()
        {
            for (var i = 0; i < questIcons.Count; i++)
            {
                if (questIcons[i].RectTransform != null)
                {
                    Destroy(questIcons[i].RectTransform.gameObject);
                }
            }

            questIcons.Clear();
        }

        private bool IsPointerInsideViewport()
        {
            Pointer pointer = Pointer.current;
            if (viewport == null || pointer == null)
            {
                return false;
            }

            return viewport != null &&
                   RectTransformUtility.RectangleContainsScreenPoint(viewport, pointer.position.ReadValue(), eventCamera);
        }

        private bool TryGetNormalizedContentPoint(Vector2 screenPoint, out Vector2 normalizedPoint)
        {
            normalizedPoint = Vector2.zero;
            if (content == null)
            {
                return false;
            }

            content.GetWorldCorners(contentWorldCorners);

            Vector2 topLeft = RectTransformUtility.WorldToScreenPoint(eventCamera, contentWorldCorners[1]);
            Vector2 bottomRight = RectTransformUtility.WorldToScreenPoint(eventCamera, contentWorldCorners[3]);

            if (Mathf.Abs(bottomRight.x - topLeft.x) < 0.001f ||
                Mathf.Abs(bottomRight.y - topLeft.y) < 0.001f)
            {
                return false;
            }

            normalizedPoint = new Vector2(
                Mathf.Clamp01(Mathf.InverseLerp(topLeft.x, bottomRight.x, screenPoint.x)),
                Mathf.Clamp01(Mathf.InverseLerp(topLeft.y, bottomRight.y, screenPoint.y)));

            return true;
        }

        private bool TryGetContentScreenPoint(Vector2 normalizedPoint, out Vector2 screenPoint)
        {
            screenPoint = Vector2.zero;
            if (content == null)
            {
                return false;
            }

            content.GetWorldCorners(contentWorldCorners);

            Vector2 topLeft = RectTransformUtility.WorldToScreenPoint(eventCamera, contentWorldCorners[1]);
            Vector2 bottomRight = RectTransformUtility.WorldToScreenPoint(eventCamera, contentWorldCorners[3]);

            screenPoint = new Vector2(
                Mathf.Lerp(topLeft.x, bottomRight.x, normalizedPoint.x),
                Mathf.Lerp(topLeft.y, bottomRight.y, normalizedPoint.y));

            return true;
        }

        private bool TryGetViewportLocalPoint(Vector2 screenPoint, out Vector2 localPoint)
        {
            localPoint = Vector2.zero;
            return viewport != null &&
                   RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screenPoint, eventCamera, out localPoint);
        }

        private Camera ResolveEventCamera()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return canvas.worldCamera;
        }

        private void OnDestroy()
        {
            ClearQuestIcons();
        }

        private readonly struct QuestIconBinding
        {
            public QuestIconBinding(RectTransform rectTransform, Transform targetTransform)
            {
                RectTransform = rectTransform;
                TargetTransform = targetTransform;
            }

            public RectTransform RectTransform { get; }
            public Transform TargetTransform { get; }
        }
    }
}
