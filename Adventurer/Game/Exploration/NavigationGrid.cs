using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Adventurer.Game.Exploration.Algorithms;
using Zeta.Bot;
using Zeta.Common;
using Logger = Adventurer.Util.Logger;

namespace Adventurer.Game.Exploration
{
    public sealed class NavigationGrid : Grid
    {
        private const int GRID_BOUNDS = 2500;

        private static Lazy<NavigationGrid> _currentGrid;

        public static NavigationGrid GetWorldGrid(int worldDynamicId)
        {
            if (_currentGrid == null)
            {
                _currentGrid = new Lazy<NavigationGrid>(() => new NavigationGrid());
                return _currentGrid.Value;
            }
  
            if (_currentGrid == null || _currentGrid.Value == null || AdvDia.CurrentWorldDynamicId != _currentGrid.Value.WorldDynamicId)
                _currentGrid = new Lazy<NavigationGrid>(() => new NavigationGrid());

            return _currentGrid.Value;
        }        

        public static NavigationGrid Instance
        {
            get { return GetWorldGrid(AdvDia.CurrentWorldDynamicId); }
        }

        public override float BoxSize
        {
            get { return ExplorationData.NavigationNodeBoxSize; }
        }

        public override int GridBounds
        {
            get { return GRID_BOUNDS; }
        }

        protected override bool MarkNodesNearWall
        {
            get { return true; }
        }

        public static void ResetAll()
        {
            _currentGrid = null;
        }

        public override bool CanRayCast(Vector3 from, Vector3 to)
        {
            return GetRayLine(from, to).Select(point => InnerGrid[point.X, point.Y]).All(node => node != null && node.NodeFlags.HasFlag(NodeFlags.AllowProjectile));
        }

        public override bool CanRayWalk(Vector3 from, Vector3 to)
        {
            return GetRayLine(from, to).Select(point => InnerGrid[point.X, point.Y]).All(node => node != null && node.NodeFlags.HasFlag(NodeFlags.AllowWalk));
        }

        private IEnumerable<GridPoint> GetRayLine(Vector3 from, Vector3 to)
        {
            var gridFrom = ToGridPoint(from);
            var gridTo = ToGridPoint(to);
            return Bresenham.GetPointsOnLine(gridFrom, gridTo);
        }


    }
}
