﻿using System;
using System.Collections.Generic;
using System.Security.Policy;
using System.Threading.Tasks;
using Adventurer.Game.Actors;
using Adventurer.Game.Combat;
using Adventurer.Game.Exploration;
using Adventurer.Game.Quests;
using Adventurer.Util;
using Zeta.Common;
using Zeta.Game.Internals.Actors;
using Logger = Adventurer.Util.Logger;

namespace Adventurer.Coroutines.BountyCoroutines.Subroutines
{
    public class InteractWithGizmoCoroutine : IBountySubroutine
    {
        private readonly int _questId;
        private readonly int _worldId;
        private readonly int _actorId;
        private readonly int _marker;
        private readonly int _interactAttemps;
        private readonly int _secondsToSleepAfterInteraction;
        private readonly int _secondsToTimeout;

        private bool _isDone;
        private States _state;
        private InteractionCoroutine _interactionCoroutine;
        private int _objectiveScanRange = 5000;

        #region State

        public enum States
        {
            NotStarted,
            Searching,
            Moving,
            Interacting,
            Completed,
            Failed
        }

        public States State
        {
            get { return _state; }
            protected set
            {
                if (_state == value) return;
                if (value != States.NotStarted)
                {
                    Logger.Info("[InteractWithGizmo] " + value);
                }
                _state = value;
            }
        }

        #endregion

        public bool IsDone
        {
            get { return _isDone || AdvDia.CurrentWorldId != _worldId && _worldId != -1; }
        }


        public InteractWithGizmoCoroutine(int questId, int worldId, int actorId, int marker, int interactAttemps = 1, int secondsToSleepAfterInteraction = 1, int secondsToTimeout = 10, bool useAll = false)
        {
            _questId = questId;
            _worldId = worldId;
            _actorId = actorId;
            _marker = marker;
            _interactAttemps = interactAttemps;
            _secondsToSleepAfterInteraction = secondsToSleepAfterInteraction;
            _secondsToTimeout = secondsToTimeout;
            _useAll = useAll;
        }

        public async Task<bool> GetCoroutine()
        {
            switch (State)
            {
                case States.NotStarted:
                    return await NotStarted();
                case States.Searching:
                    return await Searching();
                case States.Moving:
                    return await Moving();
                case States.Interacting:
                    return await Interacting();
                case States.Completed:
                    return await Completed();
                case States.Failed:
                    return await Failed();
            }
            return false;
        }

        public void Reset()
        {
            _isDone = false;
            _state = States.NotStarted;
            _objectiveScanRange = 5000;
            _objectiveLocation = Vector3.Zero;
        }

        public void DisablePulse()
        {
        }

        public BountyData BountyData
        {
            get { return _bountyData ?? (_bountyData = BountyDataFactory.GetBountyData(_questId)); }
        }

        private async Task<bool> NotStarted()
        {
            SafeZerg.Instance.DisableZerg();
            State = States.Searching;
            return false;
        }

        private async Task<bool> Searching()
        {
            if (_objectiveLocation == Vector3.Zero)
            {
                ScanForObjective();
            }
            if (_objectiveLocation != Vector3.Zero)
            {
                // Special case for cursed chest events.
                if (_objectiveLocation.Distance(AdvDia.MyPosition) < 16f && _actorId == 365097 && ActorFinder.FindGizmo(364559) != null)
                {
                    Logger.Debug("Target gizmo has transformed into invulnerable event gizmo. Ending.");
                    State = States.Failed;
                    return false;
                }

                State = States.Moving;
                return false;
            }
            if (!await ExplorationCoroutine.Explore(BountyData.LevelAreaIds)) return false;
            ScenesStorage.Reset();
            return false;
        }


        private async Task<bool> Moving()
        {
            if (!await NavigationCoroutine.MoveTo(_objectiveLocation, 10)) return false;
            if (NavigationCoroutine.LastResult == CoroutineResult.Failure)
            {
                _objectiveLocation = Vector3.Zero;
                _objectiveScanRange = ActorFinder.LowerSearchRadius(_objectiveScanRange);
                if (_objectiveScanRange <= 0)
                {
                    _objectiveScanRange = 50;
                }
                State = States.Searching;
                return false;
            }
            var actor =  ActorFinder.FindGizmo(_actorId);
            if (actor == null)
            {
                State = States.Searching;
                return false;
            }
            State = States.Interacting;
            _interactionCoroutine = new InteractionCoroutine(actor.ActorSnoId, new TimeSpan(0, 0, _secondsToTimeout),
                new TimeSpan(0, 0, _secondsToSleepAfterInteraction), _interactAttemps);
            if (!actor.IsInteractableQuestObject())
            {
                ActorFinder.InteractWhitelist.Add(actor.ActorSnoId);
            }
            return false;
        }

        private async Task<bool> Interacting()
        {
            //if (_interactionCoroutine.State == InteractionCoroutine.States.NotStarted)
            //{
            //    var portalGizmo = BountyHelpers.GetPortalNearMarkerPosition(_markerPosition);
            //    if (portalGizmo == null)
            //    {
            //        Logger.Debug("[Bounty] No portal nearby, keep exploring .");
            //        State = States.SearchingForDestinationWorld;
            //        return false;
            //    }
            //    _interactionCoroutine.DiaObject = portalGizmo;
            //}

            if (await _interactionCoroutine.GetCoroutine())
            {
                ActorFinder.InteractWhitelist.Remove(_actorId);
                if (_interactionCoroutine.State == InteractionCoroutine.States.TimedOut)
                {
                    Logger.Debug("[InteractWithGizmo] Interaction timed out.");
                    State = States.Failed;
                    return false;
                }

                if (_useAll)
                {
                    var nextGizmo = ActorFinder.FindGizmo(_actorId, gizmo => gizmo.IsInteractableQuestObject());
                    if (nextGizmo != null)
                    {
                        Logger.Warn("Found another actor that needs some interaction. Dist={0}", nextGizmo.Distance);
                        State = States.Searching;
                        return false;
                    }                        
                }

                State = States.Completed;
                _interactionCoroutine = null;
                return false;
            }
            return false;
        }

        private async Task<bool> Completed()
        {
            _isDone = true;
            return false;
        }

        private async Task<bool> Failed()
        {
            _isDone = true;
            return false;
        }

        private Vector3 _objectiveLocation = Vector3.Zero;
        private Vector3 _lastSeenLocation = Vector3.Zero;
        private long _lastScanTime;
        private BountyData _bountyData;
        private bool _useAll;

        private void ScanForObjective()
        {
            if (PluginTime.ReadyToUse(_lastScanTime, 1000))
            {
                _lastScanTime = PluginTime.CurrentMillisecond;
                if (_marker != 0)
                {
                    _objectiveLocation = BountyHelpers.ScanForMarkerLocation(_marker, _objectiveScanRange);
                }
                if (_objectiveLocation == Vector3.Zero && _actorId != 0)
                {
                    _objectiveLocation = BountyHelpers.ScanForActorLocation(_actorId, _objectiveScanRange);
                }
                if (_objectiveLocation != Vector3.Zero)
                {
                    Logger.Info("[InteractWithGizmo] Found the objective at distance {0}", AdvDia.MyPosition.Distance2D(_objectiveLocation));
                }
            }
        }
    }
}
