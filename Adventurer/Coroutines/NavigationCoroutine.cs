using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Adventurer.Game.Actors;
using Adventurer.Game.Events;
using Adventurer.Game.Exploration;
using Adventurer.Util;
using Zeta.Bot.Coroutines;
using Zeta.Bot.Navigation;
using Zeta.Common;
using Zeta.Common.Helpers;
using Zeta.Game.Internals.Actors;
using Logger = Adventurer.Util.Logger;

namespace Adventurer.Coroutines
{

    public sealed class NavigationCoroutine
    {
        private static NavigationCoroutine _navigationCoroutine;
        private static Vector3 _moveToDestination = Vector3.Zero;
        private static int _moveToDistance;
        private int _unstuckAttemps;
        private readonly Vector3 _destination;
        private readonly int _distance;
        public MoveResult MoveResult { get; private set; }

        public static CoroutineResult LastResult;

        public static async Task<bool> MoveTo(Vector3 destination, int distance)
        {
            if (_navigationCoroutine == null || _moveToDestination != destination || _moveToDistance != distance)
            {
                _navigationCoroutine = new NavigationCoroutine(destination, distance);
                _moveToDestination = destination;
                _moveToDistance = distance;
            }
            if (await _navigationCoroutine.GetCoroutine())
            {
                LastResult = _navigationCoroutine.State == States.Completed ? CoroutineResult.Success : CoroutineResult.Failure;
                _navigationCoroutine = null;
                return true;
            }
            return false;
        }

        private enum States
        {
            NotStarted,
            Moving,
            MovingToDeathGate,
            InteractingWithDeathGate,
            Completed,
            Failed
        }

        private States _state;
        private States State
        {
            get { return _state; }
            set
            {
                if (_state == value) return;

                switch (value)
                {
                    case States.NotStarted:
                        break;
                    case States.Moving:
                    case States.MovingToDeathGate:
                        break;
                    case States.InteractingWithDeathGate:
                    case States.Completed:
                    case States.Failed:
                        Logger.Debug("[Navigation] " + value);
                        break;
                }
                if (value != States.NotStarted)
                {

                }
                _state = value;
            }
        }

        private NavigationCoroutine(Vector3 destination, int distance)
        {
            _destination = destination;
            _distance = distance;
            if (_distance < 2)
            {
                _distance = 2;
            }
        }

        public async Task<bool> GetCoroutine()
        {
            switch (State)
            {
                case States.NotStarted:
                    return await NotStarted();
                case States.Moving:
                    return await Moving();
                case States.MovingToDeathGate:
                    return await MovingToDeathGate();
                case States.InteractingWithDeathGate:
                    return await InteractingWithDeathGate();
                case States.Completed:
                    return Completed();
                case States.Failed:
                    return Failed();
            }
            return false;
        }

        private Mover _mover;
        private long _lastRaywalkCheck;
        private readonly WaitTimer _pathGenetionTimer = new WaitTimer(TimeSpan.FromSeconds(1));

        private async Task<bool> NotStarted()
        {
            var distanceToDestination = AdvDia.MyPosition.Distance(_destination);
            if (PluginEvents.CurrentProfileType == ProfileType.Rift &&
                distanceToDestination < 100 &&
                NavigationGrid.Instance.CanRayWalk(AdvDia.MyPosition, _destination))
            {
                _mover = Mover.StraightLine;
                _lastRaywalkCheck = PluginTime.CurrentMillisecond;
                await Navigator.MoveTo(_destination);
            }
            else
            {
                _mover = Mover.Navigator;
            }
            Logger.Debug("[Navigation] {0} {1} (Distance: {2})", (_mover == Mover.StraightLine ? "Moving towards" : "Moving to"), _destination, distanceToDestination);
            State = States.Moving;
            _pathGenetionTimer.Reset();
            return false;
        }

        private async Task<bool> Moving()
        {
            var distanceToDestination = AdvDia.MyPosition.Distance(_destination);
            if (_destination != Vector3.Zero)
            {
                if (_distance != 0 && distanceToDestination <= _distance)
                {
                    Navigator.PlayerMover.MoveStop();
                    MoveResult = MoveResult.ReachedDestination;
                }
                else
                {

                    if (_mover == Mover.StraightLine && PluginTime.ReadyToUse(_lastRaywalkCheck, 200))
                    {
                        if (!NavigationGrid.Instance.CanRayWalk(AdvDia.MyPosition, _destination))
                        {
                            _mover = Mover.Navigator;
                        }
                        _lastRaywalkCheck = PluginTime.CurrentMillisecond;
                    }
                    switch (_mover)
                    {
                        case Mover.StraightLine:
                            Navigator.PlayerMover.MoveTowards(_destination);
                            MoveResult = MoveResult.Moved;
                            return false;
                        case Mover.Navigator:
                            MoveResult = await Navigator.MoveTo(_destination);
                            break;
                    }

                }
                switch (MoveResult)
                {
                    case MoveResult.ReachedDestination:
                        if ((_distance != 0 && distanceToDestination <= _distance) || distanceToDestination <= 7f)
                        {
                            Logger.Debug("[Navigation] Completed (Distance to destination: {0})", distanceToDestination);
                            State = States.Completed;
                        }
                        else
                        {
                            _deathGate = ActorFinder.FindNearestDeathGate(_deathGateIgnoreList);
                            if (_deathGate != null)
                            {
                                State = States.MovingToDeathGate;
                            }
                            else
                            {

                                State = States.Failed;
                                MoveResult = MoveResult.Failed;
                            }
                        }
                        return false;
                    case MoveResult.Failed:
                        break;
                    case MoveResult.PathGenerationFailed:

                        var navProvider = Navigator.NavigationProvider as DefaultNavigationProvider;
                        if (navProvider != null)
                        {
                            Logger.Debug("[Navigation] Path generation failed.");
                            navProvider.Clear();
                        }
                        if (distanceToDestination < 100 &&
                            NavigationGrid.Instance.CanRayWalk(AdvDia.MyPosition, _destination))
                        {
                            _mover = Mover.StraightLine;
                            return false;
                        }
                        State = States.Failed;
                        return false;
                    case MoveResult.UnstuckAttempt:
                        if (_unstuckAttemps > 1)
                        {
                            State = States.Failed;
                            return false;
                        }
                        _unstuckAttemps++;
                        Logger.Debug("[Navigation] Unstuck attempt #{0}", _unstuckAttemps);
                        break;
                    case MoveResult.PathGenerated:
                    case MoveResult.Moved:
                        break;
                    case MoveResult.PathGenerating:
                        if (_pathGenetionTimer.IsFinished)
                        {
                            Logger.Info("Patiently waiting for the Navigation Server");
                            _pathGenetionTimer.Reset();
                        }
                        break;
                }
                return false;
            }
            State = States.Completed;
            return false;
        }

        private DiaGizmo _deathGate;
        private List<int> _deathGateIgnoreList = new List<int>();

        private InteractionCoroutine _interactionCoroutine;

        private async Task<bool> MovingToDeathGate()
        {
            if (_deathGate != null && _deathGate.IsFullyValid())
            {
                if (AdvDia.MyPosition.Distance(_deathGate.Position) <= 7f)
                {
                    Navigator.PlayerMover.MoveStop();
                    MoveResult = MoveResult.ReachedDestination;
                }
                else
                {
                    _deathGate = ActorFinder.FindNearestDeathGate(_deathGateIgnoreList);
                    if (_deathGate == null)
                    {
                        MoveResult = MoveResult.Failed;
                        State = States.Failed;
                        return false;
                    }
                    MoveResult = await CommonCoroutines.MoveTo(_deathGate.Position);
                }
                switch (MoveResult)
                {
                    case MoveResult.ReachedDestination:
                        var distance = AdvDia.MyPosition.Distance(_deathGate.Position);
                        if (distance <= 7f)
                        {
                            _interactionCoroutine = new InteractionCoroutine(_deathGate.ActorSnoId, new TimeSpan(0, 0, 1), new TimeSpan(0, 0, 3));
                            State = States.InteractingWithDeathGate;
                        }
                        else
                        {
                            _deathGateIgnoreList.Add(_deathGate.ACDId);
                            State = States.Moving;
                        }
                        break;
                    case MoveResult.Failed:
                    case MoveResult.PathGenerationFailed:
                        State = States.Failed;
                        break;
                    case MoveResult.PathGenerated:
                        break;
                    case MoveResult.UnstuckAttempt:
                        if (_unstuckAttemps > 1)
                        {
                            State = States.Failed;
                            return false;
                        }
                        _unstuckAttemps++;
                        Logger.Debug("[Navigation] Unstuck attempt #{0}", _unstuckAttemps);
                        break;
                    case MoveResult.Moved:
                    case MoveResult.PathGenerating:
                        break;
                }
                return false;
            }
            State = States.Failed;
            return false;
        }

        private async Task<bool> InteractingWithDeathGate()
        {
            if (await _interactionCoroutine.GetCoroutine())
            {
                if (_interactionCoroutine.State == InteractionCoroutine.States.TimedOut)
                {
                    Logger.Debug("[Bounty] Near death gate, but interaction timed out.");
                    State = States.Failed;
                    _interactionCoroutine = null;
                }
                else
                {
                    _deathGate = null;
                    _deathGateIgnoreList = new List<int>();
                    State = States.Moving;
                    _interactionCoroutine = null;
                }
            }

            return false;
        }

        private bool Completed()
        {
            return true;
        }

        private bool Failed()
        {
            Logger.Debug("[Navigation] Navigation Error (MoveResult: {0}, Distance: {1})", MoveResult, AdvDia.MyPosition.Distance2D(_destination));
            return true;
        }

        private enum Mover
        {
            Navigator,
            StraightLine
        }
    }


}
