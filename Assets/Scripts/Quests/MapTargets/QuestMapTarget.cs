using System;
using Quests.Graph;
using UnityEngine;
using VContainer;

namespace Quests.MapTargets
{
    [DisallowMultipleComponent]
    public sealed class QuestMapTarget : MonoBehaviour
    {
        [SerializeField] private QuestGraph questGraph;
        [SerializeField] private Transform targetTransform;
        [SerializeField, HideInInspector] private string targetId;
        private IQuestMapTargetRegistry registry;

        public QuestGraph QuestGraph => questGraph;
        public Transform TargetTransform => targetTransform != null ? targetTransform : transform;
        public string TargetId => targetId;

        [Inject]
        public void Construct(IQuestMapTargetRegistry targetRegistry)
        {
            if (registry == targetRegistry)
            {
                return;
            }

            registry?.Unregister(this);
            registry = targetRegistry;
            if (isActiveAndEnabled)
            {
                registry.Register(this);
            }
        }

        private void OnEnable()
        {
            registry?.Register(this);
        }

        private void OnDisable()
        {
            registry?.Unregister(this);
        }

#if UNITY_EDITOR
        private void Reset()
        {
            if (targetTransform == null)
            {
                targetTransform = transform;
            }

            EnsureTargetId();
        }

        private void OnValidate()
        {
            if (targetTransform == null)
            {
                targetTransform = transform;
            }

            EnsureTargetId();
        }

        private void EnsureTargetId()
        {
            if (!string.IsNullOrWhiteSpace(targetId) && !HasDuplicateTargetId())
            {
                return;
            }

            targetId = Guid.NewGuid().ToString("N");
        }

        private bool HasDuplicateTargetId()
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return false;
            }

            QuestMapTarget[] targets = FindObjectsByType<QuestMapTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < targets.Length; i++)
            {
                QuestMapTarget other = targets[i];
                if (other != null && other != this && other.targetId == targetId)
                {
                    return true;
                }
            }

            return false;
        }
#endif
    }
}
