using System;
using System.Collections.Generic;
using Zeta.Common;

namespace Adventurer.Game.Exploration
{

    public delegate void UpdatedEventHandler(object sender, List<INode> newNodes);

    public interface IGrid<T> where T : INode
    {
        T NearestNode { get; }
        T GetNearestNodeToPosition(Vector3 position);
        List<T> GetNeighbors(T node, int distance = 1);
        List<T> FindNodes(T node, int minDistance, int maxDistance, Func<T, bool> condition = null);
        List<T> GetNodesInRadius(Vector3 center, float radius, bool circular = false);
        List<T> GetNodesInRadius(T node, float radius, bool circular = false);
        T GetNodeInDirection(T node, Direction direction);
        GridPoint ToGridPoint(Vector3 position);
        Vector3 GetWorldPoint(GridPoint gridPoint);
        bool CanRayCast(Vector3 from, Vector3 to);
        bool CanRayWalk(Vector3 from, Vector3 to);
    }
}