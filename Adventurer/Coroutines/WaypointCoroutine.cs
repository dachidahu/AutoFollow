﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Adventurer.Game.Actors;
using Buddy.Coroutines;
using Zeta.Bot;
using Zeta.Bot.Navigation;
using Zeta.Common;
using Zeta.Game;
using Zeta.Game.Internals;
using Zeta.Game.Internals.Actors.Gizmos;
using Logger = Adventurer.Util.Logger;

namespace Adventurer.Coroutines
{
    public sealed class WaypointCoroutine
    {

        private static WaypointCoroutine _waypointCoroutine;
        private static int _useWaypointWaypointNumber;

        public static async Task<bool> UseWaypoint(int waypointNumber)
        {
            if (_waypointCoroutine == null || _useWaypointWaypointNumber != waypointNumber)
            {
                _waypointCoroutine = new WaypointCoroutine(waypointNumber);
                _useWaypointWaypointNumber = waypointNumber;
            }

            if (await _waypointCoroutine.GetCoroutine())
            {
                _waypointCoroutine = null;
                return true;
            }
            return false;
        }


        private readonly int _waypointNumber;
        private Vector3 _startingPosition;


        private enum States
        {
            NotStarted,
            ClearingArea,
            TogglingWaypointMap,
            WaypointMapVisible,
            WorldTransferStartFired,
            UsingWaypoint,
            UsedWaypoint,
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
                if (value != States.NotStarted)
                {
                    Logger.Debug("[Waypoint] " + value);
                }
                _state = value;
            }
        }



        private WaypointCoroutine(int waypointNumber)
        {
            _waypointNumber = waypointNumber;
        }

        private async Task<bool> GetCoroutine()
        {
            switch (State)
            {
                case States.NotStarted:
                    return NotStarted();
                case States.ClearingArea:
                    return await ClearingArea();
                case States.TogglingWaypointMap:
                    return await TogglingWaypointMap();
                case States.WaypointMapVisible:
                    return await WaypointMapVisible();
                case States.WorldTransferStartFired:
                    return WorldTransferStartFired();
                case States.UsingWaypoint:
                    return await UsingWaypoint();
                case States.UsedWaypoint:
                    return await UsedWaypoint();
                case States.Completed:
                    return Completed();
                case States.Failed:
                    return Failed();
            }
            return false;
        }

        private bool NotStarted()
        {
            _startingPosition = AdvDia.MyPosition;
            State = States.ClearingArea;
            return false;
        }

        private async Task<bool> ClearingArea()
        {
            if (await ClearAreaCoroutine.Clear(_startingPosition,45))
            {
                State = States.TogglingWaypointMap;
            }
            return false;
        }

        private async Task<bool> TogglingWaypointMap()
        {
            if (!UIElements.WaypointMap.IsVisible)
            {
                State = States.TogglingWaypointMap;
                UIManager.ToggleWaypointMap();
                await Coroutine.Sleep(100);
            }
            else
            {
                State = States.WaypointMapVisible;
            }
            return false;
        }

        private async Task<bool> WaypointMapVisible()
        {
            if (!UIElements.WaypointMap.IsVisible)
            {
                State = States.TogglingWaypointMap;
                return false;
            }
            State = States.WorldTransferStartFired;
            GameEvents.FireWorldTransferStart();
            await Coroutine.Sleep(100);
            return false;
        }

        private bool WorldTransferStartFired()
        {
            if (!UIElements.WaypointMap.IsVisible)
            {
                State = States.TogglingWaypointMap;
                return false;
            }
            State = States.UsingWaypoint;
            return false;
        }

        private bool _usedWaypoint;
        private async Task<bool> UsingWaypoint()
        {
            if (!_usedWaypoint)
            {
                Logger.Debug("[Waypoint] Using waypoint {0}", _waypointNumber);
                // Checking for near by waypoint gizmos.
                var gizmoWaypoint =
                    ZetaDia.Actors.GetActorsOfType<GizmoWaypoint>().OrderBy(g => g.Distance).FirstOrDefault();
                if (gizmoWaypoint != null && gizmoWaypoint.IsFullyValid())
                {
                    // Already there
                    if (gizmoWaypoint.WaypointNumber == _waypointNumber && gizmoWaypoint.Distance <= 150)
                    {
                        Logger.Info("[Waypoint] Already near the destination waypoint");
                        State = States.Completed;
                        return false;
                    }

                    // To far away to interact
                    if (gizmoWaypoint.Distance < 10)
                    {
                        _usedWaypoint = true;
                        gizmoWaypoint.UseWaypoint(_waypointNumber);

                    }
                    else
                    {
                        gizmoWaypoint = null;
                    }
                }
                if (gizmoWaypoint == null)
                {
                    _usedWaypoint = true;
                    ZetaDia.Me.UseWaypoint(_waypointNumber);
                }
            }
            await Coroutine.Sleep(2000);
            if (UIElements.WaypointMap.IsVisible)
            {
                UIManager.ToggleWaypointMap();
            }
            await Coroutine.Sleep(1000);
            State = States.UsedWaypoint;
            return false;

        }

        private static readonly List<int> TransportStates = new List<int> { 3, 13 };
        private async Task<bool> UsedWaypoint()
        {
            if (!ZetaDia.IsInGame || ZetaDia.IsLoadingWorld || ZetaDia.IsPlayingCutscene)
                return false;

            if (ZetaDia.Me == null || ZetaDia.Me.CommonData == null || !ZetaDia.Me.IsValid || !ZetaDia.Me.CommonData.IsValid)
                return false;

            if (TransportStates.Contains((int)ZetaDia.Me.CommonData.AnimationState))
            {
                return false;
            }

            await Coroutine.Sleep(3000);
            ZetaDia.Actors.Update();
            _usedWaypoint = false;

            State = HasReachedDestionation ? States.Completed : States.NotStarted;

            Navigator.Clear();
            return false;


        }

        private bool Completed()
        {
            return true;
        }
        private bool Failed()
        {
            return true;
        }

        private bool HasReachedDestionation
        {
            get
            {
                var gizmoWaypoint = ZetaDia.Actors.GetActorsOfType<GizmoWaypoint>().OrderBy(g => g.Distance).FirstOrDefault();
                if (gizmoWaypoint != null)
                {
                    if(gizmoWaypoint.WaypointNumber == _waypointNumber)
                        return true;
                    if (_waypointNumber == 42 && gizmoWaypoint.WaypointNumber == 28) // A4/A3 Bastians Keep
                        return true;
                }
                return false;
            }
        }
    }
}
