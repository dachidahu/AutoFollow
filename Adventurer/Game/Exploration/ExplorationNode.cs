using System;
using System.Collections.Generic;
using System.Linq;
using Zeta.Common;
using Zeta.Game.Internals.SNO;

namespace Adventurer.Game.Exploration
{
    public class ExplorationNode : INode, IEquatable<ExplorationNode>
    {
        private bool _isIgnored;
        private readonly float _boxSize;
        private readonly float _boxTolerance;
        private readonly WorldScene _scene;

        public NavigationNode NavigableCenterNode { get; set; }
        public Vector3 NavigableCenter { get { return NavigableCenterNode != null ? NavigableCenterNode.NavigableCenter : Center.ToVector3(); } }
        public Vector2 NavigableCenter2D { get { return NavigableCenterNode != null ? NavigableCenterNode.NavigableCenter2D : Center; } }
        public Vector2 Center { get; private set; }
        public Vector2 TopLeft { get; private set; }
        public Vector2 BottomLeft { get; private set; }
        public Vector2 TopRight { get; private set; }
        public Vector2 BottomRight { get; set; }
        public int NavigableCellCount { get; private set; }
        //public float FillPercentage { get; private set; }
        public bool HasEnoughNavigableCells { get; private set; }
        public List<NavigationNode> Nodes { get; private set; }
        public WorldScene Scene { get { return _scene; } }
        public bool IsKnown { get; set; }
        public bool IsVisited { get; set; }
        public NodeFlags NodeFlags { get; set; }
        //public uint CustomFlags { get; set; }
        public GridPoint GridPoint { get; set; }

        public int LevelAreaId
        {
            get { return _scene.LevelAreaId; }
        }

        public float Distance2DSqr
        {
            get { return NavigableCenter2D.DistanceSqr(AdvDia.MyPosition.ToVector2()); }
        }

        public int DynamicWorldId
        {
            get { return _scene.DynamicWorldId; }
        }

        public ExplorationNode(Vector2 center, float boxSize, float boxTolerance, WorldScene scene)
        {
            _boxSize = boxSize;
            _boxTolerance = boxTolerance;
            _scene = scene;

            var halfSize = (float)boxSize / 2;
            //_maxCellsCount = _boxSize / 2.5 / 2;

            Center = center;
            TopLeft = Center + new Vector2(-(halfSize), -(halfSize));
            BottomLeft = Center + new Vector2(-(halfSize), halfSize);
            TopRight = Center + new Vector2(halfSize, -(halfSize));
            BottomRight = Center + new Vector2(halfSize, halfSize);

            NavigableCenterNode = null;

            Calculate(this);
        }

        private static void Calculate(ExplorationNode instance)
        {
            var navBoxSize = ExplorationData.NavigationNodeBoxSize;
            var searchBeginning = (float)navBoxSize / 2;
            var cellCount = instance._boxSize / navBoxSize;
            var maxCellsCount = cellCount * cellCount;

            instance.Nodes = new List<NavigationNode>();

            var walkableNodes = new List<NavigationNode>();
            for (var x = instance.TopLeft.X + searchBeginning; x <= instance.BottomRight.X; x = x + navBoxSize)
            {
                for (var y = instance.TopLeft.Y + searchBeginning; y <= instance.BottomRight.Y; y = y + navBoxSize)
                {
                    var cell = instance._scene.Cells.FirstOrDefault(c => c.IsInCell(x, y));
                    if (cell != null)
                    {
                        var navNode = new NavigationNode(new Vector3(x, y, cell.Z), navBoxSize, instance, cell);
                        instance.Nodes.Add(navNode);
                        if (cell.NavCellFlags.HasFlag(NavCellFlags.AllowWalk))
                        {
                            walkableNodes.Add(navNode);
                        }
                    }
                }
            }

            instance.NavigableCellCount = walkableNodes.Count;
            //instance.FillPercentage = instance.NavigableCellCount / (float)maxCellsCount;
            var fillPercetnage = instance.NavigableCellCount / (float)maxCellsCount;
            instance.HasEnoughNavigableCells = fillPercetnage >= instance._boxTolerance;
            if (walkableNodes.Count > 0)
            {
                instance.NavigableCenterNode = walkableNodes.OrderBy(ncp => ncp.Center.DistanceSqr(instance.Center)).First();
            }
        }

        public List<ExplorationNode> GetNeighbors(int distance, bool includeSelf = false)
        {
            var neighbors = ExplorationGrid.Instance.GetNeighbors(this, distance).Cast<ExplorationNode>().ToList();
            if (includeSelf)
            {
                neighbors.Add(this);
            }
            return neighbors;
        }

        public int UnvisitedWeight
        {
            get
            {
                return GetNeighbors(1, true).Count(n => n.HasEnoughNavigableCells && !n.IsVisited);
            }
        }


        public bool IsIgnored
        {
            get { return _isIgnored; }
            set
            {
                if (_isIgnored) return;
                _isIgnored = value;
            }
        }

        public bool IsCurrentDestination { get; set; }

        public bool Equals(ExplorationNode other)
        {
            return LevelAreaId == other.LevelAreaId && Center == other.Center;
        }

        public override int GetHashCode()
        {
            return Center.GetHashCode();
        }

        public byte AStarValue { get; set; }
    }
}