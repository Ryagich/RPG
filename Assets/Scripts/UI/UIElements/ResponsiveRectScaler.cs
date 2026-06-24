using UnityEngine;

namespace UI.UIElements
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ResponsiveRectScaler : MonoBehaviour
    {
        [SerializeField] private RectTransform target;
        [SerializeField] private Vector2 referenceSize = new(640f, 1080f);

        private RectTransform selfRect;

        public void Configure(RectTransform targetRect, Vector2 size)
        {
            target = targetRect;
            referenceSize = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
            ApplyScale();
        }

        private void Awake()
        {
            CacheSelfRect();
            ApplyScale();
        }

        private void OnEnable()
        {
            CacheSelfRect();
            ApplyScale();
        }

        private void LateUpdate()
        {
            ApplyScale();
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyScale();
        }

        private void OnValidate()
        {
            referenceSize = new Vector2(Mathf.Max(1f, referenceSize.x), Mathf.Max(1f, referenceSize.y));
            CacheSelfRect();
            ApplyScale();
        }

        private void Reset()
        {
            CacheSelfRect();
            if (target == null && transform.childCount > 0)
            {
                target = transform.GetChild(0) as RectTransform;
            }

            ApplyScale();
        }

        private void CacheSelfRect()
        {
            if (selfRect == null)
            {
                selfRect = transform as RectTransform;
            }
        }

        private void ApplyScale()
        {
            CacheSelfRect();
            if (selfRect == null || target == null)
            {
                return;
            }

            var rect = selfRect.rect;
            if (rect.width <= 0f || rect.height <= 0f || referenceSize.x <= 0f || referenceSize.y <= 0f)
            {
                return;
            }

            var scaleX = rect.width / referenceSize.x;
            var scaleY = rect.height / referenceSize.y;
            if (scaleX <= 0f || scaleY <= 0f)
            {
                return;
            }

            var center = new Vector2(0.5f, 0.5f);
            var targetScale = new Vector3(scaleX, scaleY, 1f);

            if (!Approximately(target.anchorMin, center))
            {
                target.anchorMin = center;
            }

            if (!Approximately(target.anchorMax, center))
            {
                target.anchorMax = center;
            }

            if (!Approximately(target.pivot, center))
            {
                target.pivot = center;
            }

            if (!Approximately(target.anchoredPosition, Vector2.zero))
            {
                target.anchoredPosition = Vector2.zero;
            }

            if (!Approximately(target.sizeDelta, referenceSize))
            {
                target.sizeDelta = referenceSize;
            }

            if (!Approximately(target.localScale, targetScale))
            {
                target.localScale = targetScale;
            }
        }

        private static bool Approximately(Vector2 left, Vector2 right)
        {
            return Mathf.Approximately(left.x, right.x) && Mathf.Approximately(left.y, right.y);
        }

        private static bool Approximately(Vector3 left, Vector3 right)
        {
            return Mathf.Approximately(left.x, right.x)
                   && Mathf.Approximately(left.y, right.y)
                   && Mathf.Approximately(left.z, right.z);
        }
    }
}
