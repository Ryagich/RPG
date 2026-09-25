using System;
using System.Collections.Generic;
using UnityEngine;

namespace EditorTools
{
    /// <summary>
    /// Maintains the viewport and ownership indexes for retained graph connections.
    /// Rendering remains the responsibility of the connection layer.
    /// </summary>
    internal sealed class GraphConnectionSpatialIndex<TNode, TConnection>
        where TNode : class
        where TConnection : class
    {
        private const float SpatialCellSize = 800f;
        private const float RouteMargin = 100f;

        private readonly IReadOnlyList<RetainedGraphConnection<TNode, TConnection>> connections;
        private readonly Func<TNode, Rect?> getNodeRect;
        private readonly Dictionary<TNode, List<int>> connectionIndicesByNode = new();
        private readonly Dictionary<long, HashSet<int>> connectionIndicesByCell = new();
        private readonly Dictionary<int, List<long>> cellsByConnectionIndex = new();
        private readonly HashSet<int> spatialQueryIndices = new();

        public GraphConnectionSpatialIndex(
            IReadOnlyList<RetainedGraphConnection<TNode, TConnection>> connections,
            Func<TNode, Rect?> getNodeRect)
        {
            this.connections = connections;
            this.getNodeRect = getNodeRect;
            Build();
        }

        public bool IsConnectedTo(TNode node)
        {
            return node != null && connectionIndicesByNode.ContainsKey(node);
        }

        public void UpdateConnectionsFor(TNode node)
        {
            if (node == null || !connectionIndicesByNode.TryGetValue(node, out List<int> indices))
            {
                return;
            }

            foreach (int index in indices)
            {
                Reindex(index);
            }
        }

        public void FillConnectionsForNode(TNode node, List<int> results)
        {
            results.Clear();
            if (node != null && connectionIndicesByNode.TryGetValue(node, out List<int> related))
            {
                results.AddRange(related);
            }
        }

        public void FillVisibleConnections(Rect visibleRect, List<int> results)
        {
            results.Clear();
            spatialQueryIndices.Clear();
            int minX = Mathf.FloorToInt(visibleRect.xMin / SpatialCellSize);
            int maxX = Mathf.FloorToInt(visibleRect.xMax / SpatialCellSize);
            int minY = Mathf.FloorToInt(visibleRect.yMin / SpatialCellSize);
            int maxY = Mathf.FloorToInt(visibleRect.yMax / SpatialCellSize);
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (connectionIndicesByCell.TryGetValue(GetCellKey(x, y), out HashSet<int> indices))
                    {
                        spatialQueryIndices.UnionWith(indices);
                    }
                }
            }

            foreach (int index in spatialQueryIndices)
            {
                if (TryGetConnectionBounds(index, out Rect bounds) && bounds.Overlaps(visibleRect))
                {
                    results.Add(index);
                }
            }
        }

        private void Build()
        {
            for (int index = 0; index < connections.Count; index++)
            {
                RetainedGraphConnection<TNode, TConnection> connection = connections[index];
                AddNodeConnectionIndex(connection.Source, index);
                if (!ReferenceEquals(connection.Source, connection.Target))
                {
                    AddNodeConnectionIndex(connection.Target, index);
                }

                Reindex(index);
            }
        }

        private void AddNodeConnectionIndex(TNode node, int index)
        {
            if (!connectionIndicesByNode.TryGetValue(node, out List<int> indices))
            {
                indices = new List<int>();
                connectionIndicesByNode.Add(node, indices);
            }

            indices.Add(index);
        }

        private void Reindex(int index)
        {
            if (cellsByConnectionIndex.TryGetValue(index, out List<long> previousCells))
            {
                foreach (long cell in previousCells)
                {
                    if (connectionIndicesByCell.TryGetValue(cell, out HashSet<int> indices))
                    {
                        indices.Remove(index);
                        if (indices.Count == 0)
                        {
                            connectionIndicesByCell.Remove(cell);
                        }
                    }
                }
            }

            var cells = new List<long>();
            cellsByConnectionIndex[index] = cells;
            if (!TryGetConnectionBounds(index, out Rect bounds))
            {
                return;
            }

            int minX = Mathf.FloorToInt(bounds.xMin / SpatialCellSize);
            int maxX = Mathf.FloorToInt(bounds.xMax / SpatialCellSize);
            int minY = Mathf.FloorToInt(bounds.yMin / SpatialCellSize);
            int maxY = Mathf.FloorToInt(bounds.yMax / SpatialCellSize);
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    long key = GetCellKey(x, y);
                    if (!connectionIndicesByCell.TryGetValue(key, out HashSet<int> indices))
                    {
                        indices = new HashSet<int>();
                        connectionIndicesByCell.Add(key, indices);
                    }

                    indices.Add(index);
                    cells.Add(key);
                }
            }
        }

        private bool TryGetConnectionBounds(int index, out Rect bounds)
        {
            RetainedGraphConnection<TNode, TConnection> connection = connections[index];
            Rect? sourceRect = getNodeRect(connection.Source);
            Rect? targetRect = getNodeRect(connection.Target);
            if (!sourceRect.HasValue || !targetRect.HasValue)
            {
                bounds = default;
                return false;
            }

            bounds = Rect.MinMaxRect(
                Mathf.Min(sourceRect.Value.xMin, targetRect.Value.xMin) - RouteMargin,
                Mathf.Min(sourceRect.Value.yMin, targetRect.Value.yMin) - RouteMargin,
                Mathf.Max(sourceRect.Value.xMax, targetRect.Value.xMax) + RouteMargin,
                Mathf.Max(sourceRect.Value.yMax, targetRect.Value.yMax) + RouteMargin);
            return true;
        }

        private static long GetCellKey(int x, int y)
        {
            return ((long)x << 32) ^ (uint)y;
        }
    }
}
