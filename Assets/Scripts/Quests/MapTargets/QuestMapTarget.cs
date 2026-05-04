using System;
using Quests.Graph;
using UnityEngine;

namespace Quests.MapTargets
{
    [DisallowMultipleComponent]
    public sealed class QuestMapTarget : MonoBehaviour
    {
        [SerializeField] private QuestGraph questGraph;
        [SerializeField] private Transform targetTransform;
        [SerializeField, HideInInspector] private string targetId;

        public QuestGraph QuestGraph => questGraph;
        public Transform TargetTransform => targetTransform != null ? targetTransform : transform;
        public string TargetId => targetId;

        private void OnEnable()
        {
            QuestMapTargetRegistry.Register(this);
        }

        private void OnDisable()
        {
            QuestMapTargetRegistry.Unregister(this);
        }

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
    }
}
