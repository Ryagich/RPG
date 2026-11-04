using EditorTools;
using Quests.Graph.Model;
using UnityEditor;
using UnityEngine;

namespace Quests.Graph.Editor
{
    /// <summary>Window-facing operations required to draw and edit a quest graph node.</summary>
    internal interface IQuestNodeInspectorHost
    {
        QuestGraph CurrentGraph { get; }
        GraphEditorTheme Theme { get; }
        bool IsSelectingTargetNode { get; }
        QuestNode ActiveConnectionNode { get; }

        bool TrySelectTargetNode(QuestNode node);
        void SelectConnectionNode(QuestNode node);
        void DeleteNode(QuestNode node);
        bool TryAssignNodeData(QuestNode node, QuestNodeData nodeData);
        void EnsureNodeCollections(QuestNodeData nodeData);
        void DrawNodeDataEditor(QuestNode node);
        void DrawTransitionsSection(QuestNodeData nodeData, QuestNode ownerNode);
        bool IsStartNode(QuestNode node);
        void MoveNodeToFront(QuestNode node);
        bool IsOrphanNode(QuestNodeData nodeData);
    }

    /// <summary>Renders a quest node while the window retains graph lifecycle and commands.</summary>
    internal sealed class QuestNodeInspector
    {
        private readonly IQuestNodeInspectorHost host;

        public QuestNodeInspector(IQuestNodeInspectorHost host)
        {
            this.host = host;
        }

        public void Draw(QuestNode node, bool showInlineRemoveButton)
        {
            if (host.TrySelectTargetNode(node)) return;

            if (!host.IsSelectingTargetNode &&
                Event.current.rawType == EventType.MouseDown &&
                Event.current.button == 0 &&
                host.ActiveConnectionNode != node)
            {
                host.SelectConnectionNode(node);
            }

            EditorGUI.BeginDisabledGroup(host.IsSelectingTargetNode);
            if (showInlineRemoveButton && GUI.Button(new Rect(298f, 5f, 16f, 16f), "x", host.Theme.MiniButtonStyle))
            {
                host.DeleteNode(node);
                EditorGUI.EndDisabledGroup();
                return;
            }

            EditorGUI.BeginChangeCheck();
            var newNodeData = (QuestNodeData)EditorGUILayout.ObjectField(node.NodeData, typeof(QuestNodeData), false);
            if (EditorGUI.EndChangeCheck()) host.TryAssignNodeData(node, newNodeData);

            if (node.NodeData == null)
            {
                EditorGUILayout.HelpBox("No quest node asset assigned.", MessageType.Warning);
                EditorGUI.EndDisabledGroup();
                return;
            }

            host.EnsureNodeCollections(node.NodeData);

            if (node.NodeData.OwnerGraph != null && node.NodeData.OwnerGraph != host.CurrentGraph)
            {
                EditorGUILayout.HelpBox(
                    $"This node belongs to quest \"{node.NodeData.OwnerGraph.name}\" and cannot be edited here.",
                    MessageType.Error);
                EditorGUI.EndDisabledGroup();
                return;
            }

            host.DrawNodeDataEditor(node);
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Transitions", host.Theme.MiniBoldLabelStyle);
            host.DrawTransitionsSection(node.NodeData, node);

            if (GUILayout.Button(host.IsStartNode(node) ? "Start Node" : "Set As Start", host.Theme.ButtonStyle))
            {
                host.MoveNodeToFront(node);
            }

            if (host.IsOrphanNode(node.NodeData))
            {
                EditorGUILayout.HelpBox(
                    "This node has no incoming transitions and is not the start node.",
                    MessageType.Warning);
            }

            EditorGUI.EndDisabledGroup();
        }
    }

    /// <summary>Owns the node-level editing entry points for the quest graph canvas.</summary>
    public partial class QuestEditorWindow
    {
        private QuestNodeInspector nodeInspector;

        private QuestNodeInspector NodeInspector => nodeInspector ??= new QuestNodeInspector(this);

        private void DrawNodeWindow(QuestNode node, bool allowWindowDrag = true)
        {
            NodeInspector.Draw(node, allowWindowDrag);
            if (allowWindowDrag)
            {
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
            }
        }


        private bool TryHandleTargetNodeSelection(QuestNode node)
        {
            if (!isSelectingTargetNode || pendingTransition == null)
            {
                return false;
            }

            if (node == null || node == sourceNodeForSelection || node.NodeData == null)
            {
                return false;
            }

            if (Event.current.type != EventType.MouseDown || Event.current.button != 0)
            {
                return false;
            }

            pendingTransition.SetTargetNode(node.NodeData);
            MarkDirty(pendingTransition);
            CancelTargetSelection(false);
            Event.current.Use();
            GUIUtility.ExitGUI();
            return true;
        }

        QuestGraph IQuestNodeInspectorHost.CurrentGraph => currentGraph;
        GraphEditorTheme IQuestNodeInspectorHost.Theme => Theme;
        bool IQuestNodeInspectorHost.IsSelectingTargetNode => isSelectingTargetNode;
        QuestNode IQuestNodeInspectorHost.ActiveConnectionNode => activeConnectionNode;

        bool IQuestNodeInspectorHost.TrySelectTargetNode(QuestNode node) => TryHandleTargetNodeSelection(node);

        void IQuestNodeInspectorHost.SelectConnectionNode(QuestNode node)
        {
            activeConnectionNode = node;
            RefreshToolkitCanvas();
            Repaint();
        }

        void IQuestNodeInspectorHost.DeleteNode(QuestNode node) => DeleteNode(node);

        bool IQuestNodeInspectorHost.TryAssignNodeData(QuestNode node, QuestNodeData nodeData)
        {
            if (nodeData != null && currentGraph.Nodes.Exists(item => item != node && item.NodeData == nodeData))
            {
                EditorUtility.DisplayDialog(
                    "Duplicate Node Detected",
                    $"Node \"{nodeData.name}\" is already assigned to another graph node.",
                    "OK");
                return false;
            }

            if (!CanAssignNodeToCurrentGraph(nodeData, node.NodeData))
            {
                EditorUtility.DisplayDialog(
                    "Node Already Belongs To Another Quest",
                    $"Node \"{nodeData.name}\" already belongs to quest \"{nodeData.OwnerGraph.name}\".",
                    "OK");
                return false;
            }

            QuestNodeData oldNodeData = node.NodeData;
            ReplaceNodeReferences(oldNodeData, nodeData);
            node.NodeData = nodeData;
            AssignNodeToCurrentGraph(nodeData);
            ReleaseNodeOwnershipIfUnused(oldNodeData);
            MarkDirty(currentGraph);
            RefreshToolkitCanvas(true);
            return true;
        }

        void IQuestNodeInspectorHost.EnsureNodeCollections(QuestNodeData nodeData) => EnsureCollections(nodeData);

        void IQuestNodeInspectorHost.DrawNodeDataEditor(QuestNode node) => DrawNodeDataEditor(node);

        void IQuestNodeInspectorHost.DrawTransitionsSection(QuestNodeData nodeData, QuestNode ownerNode) =>
            DrawTransitionsSection(nodeData, ownerNode);

        bool IQuestNodeInspectorHost.IsStartNode(QuestNode node) => IsStartNode(node);
        void IQuestNodeInspectorHost.MoveNodeToFront(QuestNode node) => MoveNodeToFront(node);
        bool IQuestNodeInspectorHost.IsOrphanNode(QuestNodeData nodeData) => IsOrphanNode(nodeData);

    }
}
