using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Adventurer.Game.Exploration;
using Adventurer.Util;
using Zeta.Common.Helpers;

namespace Adventurer.Coroutines
{
    public sealed class ExplorationCoroutine
    {
        private static ExplorationCoroutine _explorationCoroutine;
        private static HashSet<int> _exploreLevelAreaIds;

        public static async Task<bool> Explore(HashSet<int> levelAreaIds, List<string> ignoreScenes = null)
        {
            if (_explorationCoroutine == null || (_exploreLevelAreaIds != null && levelAreaIds != null && !_exploreLevelAreaIds.SetEquals(levelAreaIds)))
            {
                _explorationCoroutine = new ExplorationCoroutine(levelAreaIds);
                _exploreLevelAreaIds = levelAreaIds;
            }
            if (await _explorationCoroutine.GetCoroutine())
            {
                _explorationCoroutine = null;
                return true;
            }
            return false;
        }


        private readonly HashSet<int> _levelAreaIds;

        private enum States
        {
            NotStarted,
            Exploring,
            Completed
        }

        private States _state;
        private States State
        {
            get { return _state; }
            set
            {
                if (_state == value) return;
                if (value != States.NotStarted)
                {
                    Logger.Debug("[Exploration] " + value);
                }
                _state = value;
            }
        }

        private ExplorationCoroutine(HashSet<int> levelAreaIds, List<string> ignoreScenes = null)
        {
            _levelAreaIds = levelAreaIds;// ?? new HashSet<int> { AdvDia.CurrentLevelAreaSnoId };
            //ExplorationHelpers.SyncronizeNodesWithMinimap();
        }


        private async Task<bool> GetCoroutine()
        {
            switch (State)
            {
                case States.NotStarted:
                    return NotStarted();
                case States.Exploring:
                    return await Exploring();
                case States.Completed:
                    return Completed();
            }
            return false;
        }

        private ExplorationNode _currentDestination;

        private bool NotStarted()
        {
            State = States.Exploring;
            return false;
        }

        //private readonly WaitTimer _newNodePickTimer = new WaitTimer(TimeSpan.FromSeconds(60));

        private async Task<bool> Exploring()
        {
            if (_currentDestination == null || _currentDestination.IsVisited)// || _newNodePickTimer.IsFinished)
            {
                //_newNodePickTimer.Stop();
                //_currentDestination = ExplorationHelpers.NearestWeightedUnvisitedNodeLocation(_levelAreaIds);
                if (_currentDestination != null)
                {
                    _currentDestination.IsCurrentDestination = false;
                }
                _currentDestination = ExplorationHelpers.NearestWeightedUnvisitedNode(_levelAreaIds);
                if (_currentDestination != null) _currentDestination.IsCurrentDestination = true;
                //_newNodePickTimer.Reset();
            }
            if (_currentDestination != null)
            {
                if (await NavigationCoroutine.MoveTo(_currentDestination.NavigableCenter, 3))
                {
                    _currentDestination.IsVisited = true;
                    _currentDestination.IsCurrentDestination = false;
                    _currentDestination = null;
                }
                return false;
            }
            State = States.Completed;
            return false;
        }

        private bool Completed()
        {
            return true;
        }

    }


}
