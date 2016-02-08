using System;
using System.Collections.Generic;
using Adventurer.Util;
using Zeta.Common;
using Zeta.Game;
using Zeta.Game.Internals;
using Logger = Adventurer.Util.Logger;

namespace Adventurer.Game.Exploration
{
    public class WorldScene
    {
        private readonly float _boxSize;
        private readonly float _boxTolerance;

        public Scene Scene { get; private set; }
        public Vector2 Center { get; private set; }
        public List<ExplorationNode> Nodes = new List<ExplorationNode>();
        public string Name { get; private set; }
        public string HashName { get; private set; }
        public Vector2 Min { get; private set; }
        public Vector2 Max { get; private set; }
        //public Rect Rect { get; private set; }
        public int LevelAreaId { get; set; }
        public bool IsIgnored { get; private set; }
        public bool HasParent { get; set; }
        public bool HasChild { get; set; }
        public bool IsTopLevel { get { return !HasParent; } }
        public bool GridCreated { get; private set; }
        public int DynamicWorldId { get; private set; }
        public int SceneId { get; private set; }

        public List<WorldSceneCell> Cells { get; private set; }

        public WorldScene SubScene { get; set; }
        public DateTime GridCreatedTime { get; set; }

        public WorldScene(Scene scene, float boxSize, float boxTolerance)
        {
            using (new PerformanceLogger("[WorldScene] ctor", false))
            {
                //                Logger.Debug("[WorldScene] Scene GridSquare Size: {0} X:{1} Y:{2}", scene.Mesh.Zone.NavZoneDef.GridSquareSize,scene.Mesh.Zone.NavZoneDef.NavGridSquareCountX, scene.Mesh.Zone.NavZoneDef.NavGridSquareCountY);
                _boxSize = boxSize;
                _boxTolerance = boxTolerance;
                Scene = scene;
                Name = scene.Name;
                HashName = scene.GetSceneNameString();
                LevelAreaId = Scene.Mesh.LevelAreaSnoId;
                Min = Scene.Mesh.Zone.ZoneMin;
                Max = Scene.Mesh.Zone.ZoneMax;
                Center = (Max + Min) / 2;
                //Rect = new Rect(new Point(Center.X, Center.Y), new Size(_boxSize, _boxSize));
                HasChild = Scene.Mesh.SubSceneId > 0;
                HasParent = Scene.Mesh.ParentSceneId > 0;
                IsIgnored = ExplorationData.IgnoreScenes.Contains(Scene.Name);
                DynamicWorldId = Scene.Mesh.WorldId;
                SceneId = scene.Mesh.SceneId;
                if (HasChild)
                {
                    SubScene = new WorldScene(Scene.Mesh.SubScene, boxSize, boxTolerance);
                    if (SubScene.HasChild)
                    {
                        Logger.Error("[ScenesStorage] Found sub sub scene!!!");
                        SubScene.SubScene = new WorldScene(SubScene.Scene.Mesh.SubScene, boxSize, boxTolerance);
                    }
                }
                Logger.Verbose("[WorldScene] Created a new world scene. Name: {0} LevelArea: {1} ({2})", Name, (SNOLevelArea)LevelAreaId, LevelAreaId);
                if (LevelAreaId != AdvDia.CurrentLevelAreaId && !ExplorationData.OpenWorldIds.Contains(AdvDia.CurrentWorldId))
                {
                    Logger.Verbose("[WorldScene] The scene LevelAreaID is different than the CurrentLevelAreaID");
                    Logger.Verbose("[WorldScene] Scene Name: {0}", Name);
                    Logger.Verbose("[WorldScene] Scene: {0} ({1})", (SNOLevelArea)LevelAreaId, LevelAreaId);
                    Logger.Verbose("[WorldScene] Current: {0} ({1})", (SNOLevelArea)AdvDia.CurrentLevelAreaId, AdvDia.CurrentLevelAreaId);
                }
                CreateGrid();
            }
        }

        public Vector3 GetRelativePosition(Vector3 worldPosition)
        {
            var v2Diff = worldPosition.ToVector2() - Min;
            return new Vector3(v2Diff.X, v2Diff.Y, worldPosition.Z);
        }

        public Vector3 GetWorldPosition(Vector3 relativePosition)
        {
            var v2Diff = relativePosition.ToVector2() + Min;
            return new Vector3(v2Diff.X, v2Diff.Y, relativePosition.Z);
        }

        public bool IsInScene(Vector3 position)
        {
            return position.X >= Min.X && position.X <= Max.X && position.Y >= Min.Y && position.Y <= Max.Y;
        }

        private void CreateGrid()
        {
            if (GridCreated) return;

            Cells = new List<WorldSceneCell>();

            foreach (var navCell in Scene.Mesh.Zone.NavZoneDef.NavCells)
            {
                //if (navCell.Flags.HasFlag(NavCellFlags.AllowWalk))
                //{
                Cells.Add(new WorldSceneCell(navCell, Min));
                //}
            }
            if (SubScene != null)
            {
                foreach (var navCell in SubScene.Scene.Mesh.Zone.NavZoneDef.NavCells)
                {
                    //if (navCell.Flags.HasFlag(NavCellFlags.AllowWalk))
                    //{
                    Cells.Add(new WorldSceneCell(navCell, SubScene.Min));
                    //}
                }
                if (SubScene.SubScene != null)
                {
                    foreach (var navCell in SubScene.SubScene.Scene.Mesh.Zone.NavZoneDef.NavCells)
                    {
                        //if (navCell.Flags.HasFlag(NavCellFlags.AllowWalk))
                        //{
                        Cells.Add(new WorldSceneCell(navCell, SubScene.SubScene.Min));
                        //}
                    }
                }
            }


            var navBoxSize = ExplorationData.ExplorationNodeBoxSize;
            var searchBeginning = navBoxSize / 2;
            //var cellCount = _boxSize / navBoxSize;
            //var maxCellsCount = cellCount * cellCount;


            for (var x = Min.X + searchBeginning; x <= Max.X; x = x + navBoxSize)
            {
                for (var y = Min.Y + searchBeginning; y <= Max.Y; y = y + navBoxSize)
                {
                    var navNode = new ExplorationNode(new Vector2(x, y), _boxSize, _boxTolerance, this);
                    Nodes.Add(navNode);
                }
            }


            //var width = (int)(Max.X - Min.X);
            //var height = (int)(Max.Y - Min.Y);
            //var gridSizeX = width / _boxSize;
            //var gridSizeY = height / _boxSize;
            ////var grid = new Node[gridSizeX, gridSizeY];

            //for (var x = 0; x < gridSizeX; x++)
            //{
            //    for (var y = 0; y < gridSizeY; y++)
            //    {
            //        var center = Min + new Vector2(x * _boxSize + _boxSize / 2, y * _boxSize + _boxSize / 2);
            //        Nodes.Add(new ExplorationNode(center, _boxSize, _boxTolerance, this));
            //    }
            //}

            GridCreated = true;
            GridCreatedTime = DateTime.UtcNow;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}