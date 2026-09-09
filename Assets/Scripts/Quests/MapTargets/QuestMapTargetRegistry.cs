using System.Collections.Generic;
using Quests.Graph;
using Quests.Graph.Model;
using UnityEngine;

namespace Quests.MapTargets
{
    public interface IQuestMapTargetRegistry
    {
        bool TryGetMapPosition(QuestNodeData nodeData, out Vector3 position);
        void SetScriptTarget(QuestGraph questGraph, string targetKey, Transform targetTransform);
        void SetScriptTarget(QuestNodeData nodeData, Transform targetTransform);
        void ClearScriptTarget(QuestGraph questGraph, string targetKey);
        void ClearScriptTarget(QuestNodeData nodeData);
        void Register(QuestMapTarget questMapTarget);
        void Unregister(QuestMapTarget questMapTarget);
    }

    public sealed class QuestMapTargetRegistry : IQuestMapTargetRegistry
    {
        private readonly Dictionary<QuestGraph, Dictionary<string, QuestMapTarget>> sceneTargetsByQuest = new();
        private readonly Dictionary<QuestGraph, Dictionary<string, Transform>> scriptTargetsByQuest = new();
        public bool TryGetMapPosition(QuestNodeData nodeData, out Vector3 position)
        {
            position = default;
            if (nodeData == null || nodeData.OwnerGraph == null)
            {
                return false;
            }

            return nodeData.MapTargetSource switch
            {
                QuestMapTargetSourceType.SceneTarget => TryGetSceneTargetPosition(nodeData, out position),
                QuestMapTargetSourceType.ScriptTarget => TryGetScriptTargetPosition(nodeData.OwnerGraph, nodeData.ScriptMapTargetKey, out position),
                _ => false
            };
        }

        public void SetScriptTarget(QuestGraph questGraph, string targetKey, Transform targetTransform)
        {
            if (questGraph == null || string.IsNullOrWhiteSpace(targetKey))
            {
                return;
            }

            Dictionary<string, Transform> targets = GetOrCreateScriptTargets(questGraph);

            if (targetTransform == null)
            {
                targets.Remove(targetKey);
                return;
            }

            targets[targetKey] = targetTransform;
        }

        public void SetScriptTarget(QuestNodeData nodeData, Transform targetTransform)
        {
            if (nodeData == null ||
                nodeData.OwnerGraph == null ||
                nodeData.MapTargetSource != QuestMapTargetSourceType.ScriptTarget ||
                string.IsNullOrWhiteSpace(nodeData.ScriptMapTargetKey))
            {
                return;
            }

            SetScriptTarget(nodeData.OwnerGraph, nodeData.ScriptMapTargetKey, targetTransform);
        }

        public void ClearScriptTarget(QuestGraph questGraph, string targetKey)
        {
            if (questGraph == null || string.IsNullOrWhiteSpace(targetKey))
            {
                return;
            }

            if (scriptTargetsByQuest.TryGetValue(questGraph, out Dictionary<string, Transform> targets))
            {
                targets.Remove(targetKey);
            }
        }

        public void ClearScriptTarget(QuestNodeData nodeData)
        {
            if (nodeData == null || nodeData.OwnerGraph == null || string.IsNullOrWhiteSpace(nodeData.ScriptMapTargetKey))
            {
                return;
            }

            ClearScriptTarget(nodeData.OwnerGraph, nodeData.ScriptMapTargetKey);
        }

        public void Register(QuestMapTarget questMapTarget)
        {
            if (questMapTarget == null ||
                questMapTarget.QuestGraph == null ||
                string.IsNullOrWhiteSpace(questMapTarget.TargetId))
            {
                return;
            }

            Dictionary<string, QuestMapTarget> targets = GetOrCreateSceneTargets(questMapTarget.QuestGraph);
            targets[questMapTarget.TargetId] = questMapTarget;
        }

        public void Unregister(QuestMapTarget questMapTarget)
        {
            if (questMapTarget == null ||
                questMapTarget.QuestGraph == null ||
                string.IsNullOrWhiteSpace(questMapTarget.TargetId))
            {
                return;
            }

            if (sceneTargetsByQuest.TryGetValue(questMapTarget.QuestGraph, out Dictionary<string, QuestMapTarget> targets) &&
                targets.TryGetValue(questMapTarget.TargetId, out QuestMapTarget registeredTarget) &&
                registeredTarget == questMapTarget)
            {
                targets.Remove(questMapTarget.TargetId);
            }
        }

        private bool TryGetSceneTargetPosition(QuestNodeData nodeData, out Vector3 position)
        {
            position = default;
            if (nodeData == null || nodeData.OwnerGraph == null || string.IsNullOrWhiteSpace(nodeData.SceneMapTargetId))
            {
                return false;
            }

            if (sceneTargetsByQuest.TryGetValue(nodeData.OwnerGraph, out Dictionary<string, QuestMapTarget> targets) &&
                targets.TryGetValue(nodeData.SceneMapTargetId, out QuestMapTarget questMapTarget) &&
                questMapTarget != null)
            {
                position = questMapTarget.TargetTransform.position;
                return true;
            }

            targets?.Remove(nodeData.SceneMapTargetId);
            if (!nodeData.HasAuthoredMapPosition)
            {
                return false;
            }

            position = nodeData.AuthoredMapPosition;
            return true;
        }

        private bool TryGetScriptTargetPosition(QuestGraph questGraph, string targetKey, out Vector3 position)
        {
            position = default;
            if (questGraph == null || string.IsNullOrWhiteSpace(targetKey))
            {
                return false;
            }

            if (!scriptTargetsByQuest.TryGetValue(questGraph, out Dictionary<string, Transform> targets) ||
                !targets.TryGetValue(targetKey, out Transform targetTransform))
            {
                return false;
            }

            if (targetTransform == null)
            {
                targets.Remove(targetKey);
                return false;
            }

            position = targetTransform.position;
            return true;
        }

        private Dictionary<string, QuestMapTarget> GetOrCreateSceneTargets(QuestGraph questGraph)
        {
            if (!sceneTargetsByQuest.TryGetValue(questGraph, out Dictionary<string, QuestMapTarget> targets))
            {
                targets = new Dictionary<string, QuestMapTarget>();
                sceneTargetsByQuest[questGraph] = targets;
            }

            return targets;
        }

        private Dictionary<string, Transform> GetOrCreateScriptTargets(QuestGraph questGraph)
        {
            if (!scriptTargetsByQuest.TryGetValue(questGraph, out Dictionary<string, Transform> targets))
            {
                targets = new Dictionary<string, Transform>();
                scriptTargetsByQuest[questGraph] = targets;
            }

            return targets;
        }
    }
}
