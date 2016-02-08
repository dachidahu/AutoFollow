using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Adventurer.Game.Events;
using Adventurer.Game.Exploration.Algorithms;
using Zeta.Common;
using Logger = Zeta.Common.Logger;

namespace Adventurer.Game.Exploration
{
    public sealed class ExplorationGrid : Grid
    {
        private static readonly ConcurrentDictionary<int, List<Vector3>> KnownPositions = new ConcurrentDictionary<int, List<Vector3>>();
        private static readonly ConcurrentDictionary<int, Lazy<ExplorationGrid>> WorldGrids = new ConcurrentDictionary<int, Lazy<ExplorationGrid>>();

        private const int GRID_BOUNDS = 500;

        public static ExplorationGrid GetWorldGrid(int worldDynamicId)
        {
            return WorldGrids.GetOrAdd(worldDynamicId, new Lazy<ExplorationGrid>(() => new ExplorationGrid())).Value;
        }

        public static ExplorationGrid Instance
        {
            get { return GetWorldGrid(AdvDia.CurrentWorldDynamicId); }
        }

        public ExplorationGrid()
        {
            WalkableNodes = new List<ExplorationNode>();
            Updated += ExplorationGrid_Updated;
        }

        public List<ExplorationNode> WalkableNodes { get; private set; }

        private void ExplorationGrid_Updated(object sender, List<INode> newNodes)
        {
            WalkableNodes.AddRange(newNodes.Cast<ExplorationNode>().Where(n => !n.IsIgnored && n.HasEnoughNavigableCells));
        }

        public override float BoxSize
        {
            get { return ExplorationData.ExplorationNodeBoxSize; }
        }

        public override int GridBounds
        {
            get { return GRID_BOUNDS; }
        }

        protected override bool MarkNodesNearWall
        {
            get { return false; }
        }

        public override bool CanRayCast(Vector3 from, Vector3 to)
        {
            return false;
        }

        public override bool CanRayWalk(Vector3 from, Vector3 to)
        {
            return GetRayLineAsNodes(from, to).All(node => node.HasEnoughNavigableCells);
        }


        public static void ResetAll()
        {
            WorldGrids.Clear();
        }

        public ExplorationNode GetNearestWalkableNodeToPosition(Vector3 position)
        {
            var nodeLine = GetRayLineAsNodes(AdvDia.MyPosition, position);
            var lastNode = nodeLine.LastOrDefault(n => n.HasEnoughNavigableCells);
            return lastNode;
        }

        private IEnumerable<GridPoint> GetRayLine(Vector3 from, Vector3 to)
        {
            var gridFrom = ToGridPoint(from);
            var gridTo = ToGridPoint(to);
            return Bresenham.GetPointsOnLine(gridFrom, gridTo);
        }

        private IEnumerable<ExplorationNode> GetRayLineAsNodes(Vector3 from, Vector3 to)
        {
            var rayLine = GetRayLine(from, to);
            return rayLine.Select(point => InnerGrid[point.X, point.Y]).Where(n => n is ExplorationNode).Cast<ExplorationNode>();
        }

        public static List<ExplorationNode> GetExplorationNodesInRadius(ExplorationNode centerNode, float radius)
        {
            var gridDistance = Instance.ToGridDistance(radius);
            var neighbors = centerNode.GetNeighbors(gridDistance, true);
            return neighbors.Where(n => n.Center.DistanceSqr(centerNode.NavigableCenter2D) < radius * radius).ToList();
        }

        //public static void SetNodesVisited()
        //{
        //    using (new PerformanceLogger("[NodesStorage] SetNodesVisited", true))
        //    {
        //        //var counter = 0;
        //        Parallel.ForEach(ExplorationGrid.Instance.WalkableNodes.Where(n => n.IsKnown), n =>
        //        {
        //            n.IsVisited = true;
        //            foreach (var node in n.GetNeighbors(1))
        //            {
        //                node.IsVisited = true;
        //            }
        //        });
        //    }
        //}

        public static void ResetKnownPositions()
        {
            KnownPositions.Clear();
        }

        public static void PulseSetVisited()
        {
            var nearestNode = Instance.NearestNode as ExplorationNode;

            if (nearestNode != null && !nearestNode.IsKnown)
            {
                var currentWorldKnownPositions = KnownPositions.GetOrAdd(AdvDia.CurrentWorldDynamicId,
                    new List<Vector3>());
                currentWorldKnownPositions.Add(nearestNode.Center.ToVector3());
                nearestNode.IsKnown = true;
                nearestNode.IsVisited = true;
                var radius = 45;
                switch (PluginEvents.CurrentProfileType)
                {
                    case ProfileType.Rift:
                        radius = 45;
                        var worldScene = AdvDia.CurrentWorldScene;
                        if (worldScene != null && worldScene.Name.Contains("Exit"))
                        {
                            radius = 15;
                        }
                        break;
                    case ProfileType.Bounty:
                        radius = 45;
                        break;
                    case ProfileType.Keywarden:
                        radius = 70;
                        break;
                }
                foreach (var node in GetExplorationNodesInRadius(nearestNode, radius))
                {
                    node.IsVisited = true;
                }
            }
            //if (!PulseSetVisitedTimer.IsFinished) return;
            //if (!PluginEvents.IsValidForPulse) return;
            //PulseSetVisitedTimer.Reset();
        }


    }
}
