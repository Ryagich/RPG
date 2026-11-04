using System.Collections.Generic;
using UnityEngine;

namespace EditorTools
{
    /// <summary>Maps an authored connection selector to its stable position on a graph node.</summary>
    internal sealed class GraphEditorConnectionAnchorRegistry<TConnection> where TConnection : class
    {
        private readonly Dictionary<TConnection, Vector2> pickerOffsets = new();

        public void Clear() => pickerOffsets.Clear();

        public bool Record(TConnection connection, Rect pickerRect)
        {
            if (connection == null || Event.current.type != EventType.Repaint)
            {
                return false;
            }

            // The callback is invoked by the node's IMGUIContainer, so GUILayout already
            // reports a position in that node's unscaled local coordinate space. Converting
            // it through RuntimePanelUtils is invalid for EditorWindow panels and interrupts
            // the whole IMGUI draw when it throws.
            Vector2 offset = pickerRect.center;
            if (pickerOffsets.TryGetValue(connection, out Vector2 currentOffset) &&
                GraphConnectionGeometry.ApproximatelyEqual(currentOffset, offset))
            {
                return false;
            }

            pickerOffsets[connection] = offset;
            return true;
        }

        public bool Remove(TConnection connection) => connection != null && pickerOffsets.Remove(connection);

        public Vector2 Resolve(TConnection connection, Rect sourceRect, Vector2 fallback)
        {
            return connection != null && pickerOffsets.TryGetValue(connection, out Vector2 pickerOffset)
                ? GraphConnectionGeometry.GetNearestBorderPoint(sourceRect, sourceRect.position + pickerOffset)
                : fallback;
        }

        /// <summary>Returns the centre of the authored connection selector itself.</summary>
        public Vector2 ResolvePickerPoint(TConnection connection, Rect sourceRect, Vector2 fallback)
        {
            return connection != null && pickerOffsets.TryGetValue(connection, out Vector2 pickerOffset)
                ? sourceRect.position + pickerOffset
                : fallback;
        }
    }
}
