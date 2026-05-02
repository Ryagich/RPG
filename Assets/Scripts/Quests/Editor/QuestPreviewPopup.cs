using Quests.Graph;
using Quests.Graph.Model;
using UnityEditor;
using UnityEngine;

namespace Quests.Editor
{
    public sealed class QuestPreviewPopup : PopupWindowContent
    {
        private readonly QuestGraph questGraph;
        private readonly QuestNodeData nodeData;
        private readonly QuestTransition transition;
        private readonly PreviewMode mode;

        private enum PreviewMode
        {
            Quest,
            Node,
            Transition
        }

        private QuestPreviewPopup(QuestGraph questGraph)
        {
            this.questGraph = questGraph;
            mode = PreviewMode.Quest;
        }

        private QuestPreviewPopup(QuestNodeData nodeData)
        {
            this.nodeData = nodeData;
            mode = PreviewMode.Node;
        }

        private QuestPreviewPopup(QuestGraph questGraph, QuestTransition transition)
        {
            this.questGraph = questGraph;
            this.transition = transition;
            mode = PreviewMode.Transition;
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(320f, mode switch
            {
                PreviewMode.Quest => 120f,
                PreviewMode.Node => 132f,
                _ => 96f
            });
        }

        public override void OnGUI(Rect rect)
        {
            switch (mode)
            {
                case PreviewMode.Quest:
                    QuestPreviewUtility.DrawQuestGraphPreview(questGraph, "Quest");
                    break;
                case PreviewMode.Node:
                    QuestPreviewUtility.DrawQuestNodePreview(nodeData, "Quest Node");
                    break;
                case PreviewMode.Transition:
                    QuestPreviewUtility.DrawQuestTransitionPreview(questGraph, transition, "Transition");
                    break;
            }
        }

        public static void ShowQuest(Rect activatorRect, QuestGraph questGraph)
        {
            if (questGraph != null)
            {
                PopupWindow.Show(GetCursorRect(activatorRect), new QuestPreviewPopup(questGraph));
            }
        }

        public static void ShowNode(Rect activatorRect, QuestNodeData nodeData)
        {
            if (nodeData != null)
            {
                PopupWindow.Show(GetCursorRect(activatorRect), new QuestPreviewPopup(nodeData));
            }
        }

        public static void ShowTransition(Rect activatorRect, QuestGraph questGraph, QuestTransition transition)
        {
            if (questGraph != null && transition != null)
            {
                PopupWindow.Show(GetCursorRect(activatorRect), new QuestPreviewPopup(questGraph, transition));
            }
        }

        private static Rect GetCursorRect(Rect fallbackRect)
        {
            Vector2 screenPoint;
            if (Event.current != null)
            {
                screenPoint = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            }
            else
            {
                screenPoint = GUIUtility.GUIToScreenPoint(new Vector2(fallbackRect.xMax, fallbackRect.yMax));
            }

            return new Rect(screenPoint.x + 12f, screenPoint.y + 12f, 1f, 1f);
        }
    }
}
