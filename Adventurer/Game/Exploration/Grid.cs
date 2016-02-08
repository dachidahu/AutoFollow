using System;
using System.Collections.Generic;
using System.Linq;
using Adventurer.Util;
using Demonbuddy;
using Zeta.Bot.Navigation;
using Zeta.Common;
using Logger = Adventurer.Util.Logger;

namespace Adventurer.Game.Exploration
{
    public abstract class Grid : IGrid<INode>
    {

        public event UpdatedEventHandler Updated;
        public SplitArray<INode> InnerGrid;

        public abstract float BoxSize { get; }
        public abstract int GridBounds { get; }
        protected abstract bool MarkNodesNearWall { get; }

        public int MinX = int.MaxValue;
        public int MaxX = 0;
        public int MinY = int.MaxValue;
        public int MaxY = 0;
        internal int GridMaxX;
        internal int GridMaxY;
        internal int BaseSize;

        protected Grid()
        {
            CreateGrid();
        }

        private void CreateGrid()
        {
            if (InnerGrid == null)
            {
                Logger.Debug("[{0}] Creating grid [{1},{1}]", GetType().Name, GridBounds);
                //InnerGrid = new INode[GridBounds, GridBounds];                
                InnerGrid = new SplitArray<INode>(GridBounds, GridBounds);
                WorldDynamicId = AdvDia.CurrentWorldDynamicId;
            }
        }

        public void Update(List<INode> nodes)
        {
            Logger.Verbose("[{0}] Updating grid with {1} new nodes", GetType().Name, nodes.Count);

            nodes = nodes.OrderBy(n => n.Center.X).ThenBy(n => n.Center.Y).ToList();
            foreach (var node in nodes)
            {
                if (node.DynamicWorldId != AdvDia.CurrentWorldDynamicId)
                {
                    Logger.Debug("[{0}] A node has different worldId than current world, skipping", GetType().Name);
                    return;
                }
                var nodeX = ToGridDistance(node.Center.X);  //(int)Math.Round((node.Center.X - MinX - boxSize / 2) / boxSize);
                var nodeY = ToGridDistance(node.Center.Y);  //(int)Math.Round((node.Center.Y - MinY - boxSize / 2) / boxSize);
                InnerGrid[nodeX, nodeY] = node;
                if (MinX > nodeX) MinX = nodeX;
                if (MaxX < nodeX) MaxX = nodeX;
                if (MinY > nodeY) MinY = nodeY;
                if (MaxY < nodeY) MaxY = nodeY;
                node.GridPoint = new GridPoint(nodeX, nodeY);
            }

            GridMaxX = InnerGrid.GetLength(0);
            GridMaxY = InnerGrid.GetLength(1);
            BaseSize = (int)Math.Round(BoxSize / 4, MidpointRounding.AwayFromZero);

            if (this is ExplorationGrid)
            {
                foreach (var node in nodes)
                {
                    var expNode = node as ExplorationNode;
                    if (expNode != null)
                    {
                        expNode.AStarValue = (byte)(expNode.HasEnoughNavigableCells ? 1 : 2);
                    }
                }
            }

            if (this is NavigationGrid)
            {
                foreach (var node in nodes.Where(n => n.NodeFlags.HasFlag(NodeFlags.AllowWalk)))
                {
                    var navNode = node as NavigationNode;
                    if (navNode != null)
                    {
                        navNode.AStarValue = 1;
                        if (MarkNodesNearWall)
                        {
                            if (GetNeighbors(node).Any(n => (n.NodeFlags & NodeFlags.AllowWalk) == 0))
                            {
                                node.NodeFlags |= NodeFlags.NearWall;

                                navNode.AStarValue = 2;
                                if (navNode == navNode.ExplorationNode.NavigableCenterNode)
                                {
                                    var newCenterNode = GetNeighbors(node).FirstOrDefault(n =>
                                                     n.NodeFlags.HasFlag(NodeFlags.AllowWalk) &&
                                                     !n.NodeFlags.HasFlag(NodeFlags.NearWall));
                                    if (newCenterNode != null && newCenterNode is NavigationNode)
                                    {
                                        navNode.ExplorationNode.NavigableCenterNode = newCenterNode as NavigationNode;
                                    }
                                }
                            }
                        }

                    }
                }
            }

            OnUpdated(nodes);
        }

        public INode NearestNode
        {
            get { return GetNearestNodeToPosition(AdvDia.MyPosition); }
        }

        public INode GetNearestNodeToPosition(Vector3 position)
        {
            var x = ToGridDistance(position.X);
            var y = ToGridDistance(position.Y);

            var gridMaxX = InnerGrid.GetLength(0);
            var gridMaxY = InnerGrid.GetLength(1);

            if (x < 0 || x > gridMaxX) return default(INode);
            if (y < 0 || y > gridMaxY) return default(INode);
            return InnerGrid[x, y];
        }

        public int ToGridDistance(float value)
        {
            return (int)Math.Round((value - BoxSize / 2) / BoxSize, MidpointRounding.AwayFromZero);
        }

        public float ToWorldDistance(int gridValue)
        {
            return gridValue * BoxSize + BoxSize / 2;
        }

        public List<INode> GetNeighbors(INode node, int distance = 1)
        {
            var neighbors = new List<INode>();
            if (node == null)
            {
                return neighbors;
            }
            var gridPoint = node.GridPoint;
            if (gridPoint == default(GridPoint)) return neighbors;

            for (var x = gridPoint.X - distance; x <= gridPoint.X + distance; x++)
            {
                if (x < 0 || x > GridMaxX) continue;
                for (var y = gridPoint.Y - distance; y <= gridPoint.Y + distance; y++)
                {
                    if (y < 0 || y > GridMaxY) continue;

                    // Excluding itself
                    if (x == gridPoint.X && y == gridPoint.Y) continue;
                    var gridNode = InnerGrid[x, y];
                    if (gridNode != null)
                    {
                        neighbors.Add(gridNode);
                    }
                }
            }
            return neighbors;
        }

        public List<INode> FindNodes(INode node, int minDistance, int maxDistance, Func<INode, bool> condition = null)
        {
            var neighbors = new List<INode>();
            if (node == null)
                return neighbors;

            var gridX = ToGridDistance(node.Center.X);
            var gridY = ToGridDistance(node.Center.Y);
            var roundWidth = maxDistance >= 3 ? (int)Math.Round(maxDistance * 0.35, 0, MidpointRounding.AwayFromZero) : 1;
            var edgelength = maxDistance * 2 + 1;
            var v2Center = node.Center;
            var worldDistanceMax = maxDistance * BoxSize;
            var worldDistanceMin = minDistance * BoxSize;
            var worldDistanceMaxSqr = worldDistanceMax * worldDistanceMax;
            var worldDistanceMinSqr = worldDistanceMin * worldDistanceMin;

            var row = -1;
            for (var x = gridX - maxDistance; x <= gridX + maxDistance; x++)
            {
                row++;

                if (x < 0 || x > GridMaxX)
                    continue;

                var col = 0;
                for (var y = gridY - maxDistance; y <= gridY + maxDistance; y++)
                {
                    col++;
                    if (y < 0 || y > GridMaxY)
                        continue;

                    if (x == gridX && y == gridY)
                        continue;

                    var gridNode = InnerGrid[x, y];
                    if (gridNode != null)
                    {
                        if (minDistance > 0 && (x > gridX - minDistance && x < gridX + minDistance && y > gridY - minDistance && y < gridY + minDistance))
                        {
                            if (gridNode.Center.DistanceSqr(v2Center) <= worldDistanceMinSqr)
                                continue;
                        }

                        if (row <= roundWidth || col <= roundWidth || edgelength - row <= roundWidth || edgelength - col <= roundWidth)
                        {
                            if (gridNode.Center.DistanceSqr(v2Center) >= worldDistanceMaxSqr)
                                continue;
                        }

                        if (condition == null || condition(gridNode))
                            neighbors.Add(gridNode);
                    }
                }
            }
            return neighbors;
        }

        public List<INode> GetNodesInRadius(Vector3 center, float radius, bool circular = false)
        {
            using (new PerformanceLogger(string.Format("[{0}] GetNodesInRadius (Radius: {1})", GetType().Name, radius), true, false))
            {
                var node = GetNearestNodeToPosition(center);
                if (node != null) // 
                {
                    return GetNodesInRadius(node, radius, circular);
                }

                return new List<INode>();
            }
        }

        public List<INode> GetNodesInRadius(INode node, float radius, bool circular = false)
        {
            //using (new PerformanceLogger(string.Format("[{0}] GetNodesInRadius (Radius: {1})", GetType().Name, radius), true, true))
            //{
            var gridRadius = ToGridDistance(radius);
            var neighbors = FindNodes(node, 0, gridRadius);
            neighbors.Add(node);
            //if (!circular)
            //{
            return neighbors;
            //}
            //var v2Center = node.Center;
            //var radiusSqr = radius*radius;
            //return neighbors.Where(n => n.Center.DistanceSqr(v2Center) <= radiusSqr).ToList();
            //}
        }

        public INode GetNodeInDirection(INode node, Direction direction)
        {
            var x = ToGridDistance(node.Center.X);
            var y = ToGridDistance(node.Center.Y);

            switch (direction)
            {
                case Direction.West: x -= BaseSize; break;
                case Direction.North: y += BaseSize; break;
                case Direction.East: x += BaseSize; break;
                case Direction.South: y -= BaseSize; break;
                case Direction.NorthWest: x -= BaseSize; y += BaseSize; break;
                case Direction.SouthWest: x -= BaseSize; y -= BaseSize; break;
                case Direction.SouthEast: x += BaseSize; y -= BaseSize; break;
                case Direction.NorthEast: x += BaseSize; y += BaseSize; break;
            }

            return InnerGrid[x, y];
        }


        public GridPoint ToGridPoint(Vector3 position)
        {
            var x = ToGridDistance(position.X);
            var y = ToGridDistance(position.Y);
            return new GridPoint(x, y);
        }

        public Vector3 GetWorldPoint(GridPoint gridPoint)
        {
            return new Vector3(ToWorldDistance(gridPoint.X), ToWorldDistance(gridPoint.Y), 0);
        }

        public abstract bool CanRayCast(Vector3 @from, Vector3 to);

        public abstract bool CanRayWalk(Vector3 @from, Vector3 to);

        public int WorldDynamicId { get; set; }

        protected virtual void OnUpdated(List<INode> nodes)
        {
            var handler = Updated;
            if (handler != null) handler(this, nodes);
        }
    }

}
