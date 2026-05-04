using System.Collections.Generic;
using Quests.Graph;
using Quests.Graph.Model;
using UnityEngine;

namespace Quests.MapTargets
{
    public static class QuestMapTargetRegistry
    {
        private static readonly Dictionary<QuestGraph, Dictionary<string, QuestMapTarget>> sceneTargetsByQuest = new();
        private static readonly Dictionary<QuestGraph, Dictionary<string, Transform>> scriptTargetsByQuest = new();

        public static Transform GetTarget(QuestNodeData nodeData)
        {
            if (nodeData == null || nodeData.OwnerGraph == null)
            {
                return null;
            }

            return nodeData.MapTargetSource switch
            {
                QuestMapTargetSourceType.SceneTarget => GetSceneTarget(nodeData.OwnerGraph, nodeData.SceneMapTargetId),
                QuestMapTargetSourceType.ScriptTarget => GetScriptTarget(nodeData.OwnerGraph, nodeData.ScriptMapTargetKey),
                _ => null
            };
        }

        public static void SetScriptTarget(QuestGraph questGraph, string targetKey, Transform targetTransform)
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

        public static void SetScriptTarget(QuestNodeData nodeData, Transform targetTransform)
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

        public static void ClearScriptTarget(QuestGraph questGraph, string targetKey)
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

        public static void ClearScriptTarget(QuestNodeData nodeData)
        {
            if (nodeData == null || nodeData.OwnerGraph == null || string.IsNullOrWhiteSpace(nodeData.ScriptMapTargetKey))
            {
                return;
            }

            ClearScriptTarget(nodeData.OwnerGraph, nodeData.ScriptMapTargetKey);
        }

        public static void Register(QuestMapTarget questMapTarget)
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

        public static void Unregister(QuestMapTarget questMapTarget)
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

        private static Transform GetSceneTarget(QuestGraph questGraph, string targetId)
        {
            if (questGraph == null || string.IsNullOrWhiteSpace(targetId))
            {
                return null;
            }

            if (!sceneTargetsByQuest.TryGetValue(questGraph, out Dictionary<string, QuestMapTarget> targets) ||
                !targets.TryGetValue(targetId, out QuestMapTarget questMapTarget) ||
                questMapTarget == null)
            {
                targets?.Remove(targetId);
                return null;
            }

            return questMapTarget.TargetTransform;
        }

        private static Transform GetScriptTarget(QuestGraph questGraph, string targetKey)
        {
            if (questGraph == null || string.IsNullOrWhiteSpace(targetKey))
            {
                return null;
            }

            if (!scriptTargetsByQuest.TryGetValue(questGraph, out Dictionary<string, Transform> targets) ||
                !targets.TryGetValue(targetKey, out Transform targetTransform))
            {
                return null;
            }

            if (targetTransform == null)
            {
                targets.Remove(targetKey);
                return null;
            }

            return targetTransform;
        }

        private static Dictionary<string, QuestMapTarget> GetOrCreateSceneTargets(QuestGraph questGraph)
        {
            if (!sceneTargetsByQuest.TryGetValue(questGraph, out Dictionary<string, QuestMapTarget> targets))
            {
                targets = new Dictionary<string, QuestMapTarget>();
                sceneTargetsByQuest[questGraph] = targets;
            }

            return targets;
        }

        private static Dictionary<string, Transform> GetOrCreateScriptTargets(QuestGraph questGraph)
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
