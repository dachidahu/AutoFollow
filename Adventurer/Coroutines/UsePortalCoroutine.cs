﻿using System;
using System.Threading.Tasks;
using Adventurer.Game.Actors;
using Adventurer.Util;
using Buddy.Coroutines;
using Zeta.Bot;
using Zeta.Game;
using Zeta.Game.Internals.Actors;
using Zeta.Game.Internals.SNO;

namespace Adventurer.Coroutines
{
    public sealed class UsePortalCoroutine
    {

        private static UsePortalCoroutine _usePortalCoroutine;
        private static int _usePortalActorSNO;
        private static int _usePortalSourceWorldDynamicId;

        public static async Task<bool> UsePortal(int actorSNO, int sourceWorldDynamicId)
        {
            if (_usePortalCoroutine == null || _usePortalActorSNO != actorSNO || _usePortalSourceWorldDynamicId != sourceWorldDynamicId)
            {
                _usePortalCoroutine = new UsePortalCoroutine(actorSNO, sourceWorldDynamicId);
                _usePortalActorSNO = actorSNO;
                _usePortalSourceWorldDynamicId = sourceWorldDynamicId;
            }

            if (await _usePortalCoroutine.GetCoroutine())
            {
                _usePortalCoroutine = null;
                return true;
            }
            return false;
        }

        #region State

        public enum States
        {
            NotStarted,
            Checking,
            Interacting,
            Completed,
            Failed
        }

        private States _state;
        public States State
        {
            get { return _state; }
            set
            {
                if (_state == value) return;
                if (value != States.NotStarted)
                {
                    Logger.Debug("[UsePortal] " + value);
                }
                _state = value;
            }
        }

        #endregion

        private readonly int _actorId;
        private readonly int _sourceWorldDynamicId;
        private readonly TimeSpan _sleepTime = new TimeSpan(0, 0, 1);

        private UsePortalCoroutine(int actorId, int sourceWorldDynamicId)
        {
            _actorId = actorId;
            _sourceWorldDynamicId = sourceWorldDynamicId;
        }


        private async Task<bool> GetCoroutine()
        {
            switch (State)
            {
                case States.NotStarted:
                    return NotStarted();
                case States.Checking:
                    return await Checking();
                case States.Interacting:
                    return await Interacting();
                case States.Completed:
                    return Completed();
                case States.Failed:
                    return Failed();
            }
            return false;
        }


        private bool NotStarted()
        {
            State = States.Checking;
            return false;
        }

        private async Task<bool> Checking()
        {
            var actor = ActorFinder.FindObject(_actorId);
            //if (actor != null && actor.Distance > actor.CollisionSphere.Radius + 2)
            //{
            //    await NavigationCoroutine.MoveTo(actor.Position, (int)actor.CollisionSphere.Radius + 1);
            //    return false;
            //}
            if (_sourceWorldDynamicId != AdvDia.CurrentWorldDynamicId)
            {
                Logger.Debug("[UsePortal] World has changed, assuming done.");
                State = States.Completed;
                return false;
            }
            if (actor == null)
            {
                Logger.Debug("[UsePortal] Nothing to interact, failing. ");
                State = States.Failed;
                return false;
            }
            if (_interactAttempts > 5)
            {
                await NavigationCoroutine.MoveTo(actor.Position, (int) actor.Distance - 1);
                _interactAttempts = 0;
            }
            State = States.Interacting;
            return false;
        }

        private int _interactAttempts = 0;

        private async Task<bool> Interacting()
        {
            _interactAttempts++;
            if (ZetaDia.IsLoadingWorld)
            {
                Logger.Debug("[UsePortal] Waiting for the world to load");
                await Coroutine.Sleep(250);
                return false;
            }

            var actor = ActorFinder.FindObject(_actorId);
            if (actor == null)
            {
                Logger.Debug("[UsePortal] Nothing to interact, failing. ");
                State = States.Failed;
                return false;
            }


            if (await Interact(actor))
            {
                State = States.Checking;
            }
            return false;
        }

        private static bool Completed()
        {
            return true;
        }

        private static bool Failed()
        {
            return true;
        }

        private async Task<bool> Interact(DiaObject actor)
        {
            Logger.Debug("[UsePortal] Attempting to use portal {0} at distance {1}", ((SNOActor)actor.ActorSnoId).ToString(), actor.Distance);
            bool retVal = false;
            switch (actor.ActorType)
            {
                case ActorType.Gizmo:
                    switch (actor.ActorInfo.GizmoType)
                    {
                        case GizmoType.BossPortal:
                        case GizmoType.Portal:
                        case GizmoType.ReturnPortal:
                            retVal = ZetaDia.Me.UsePower(SNOPower.GizmoOperatePortalWithAnimation, actor.Position);
                            break;
                        default:
                            retVal = ZetaDia.Me.UsePower(SNOPower.Axe_Operate_Gizmo, actor.Position);
                            break;
                    }
                    break;
                case ActorType.Monster:
                    retVal = ZetaDia.Me.UsePower(SNOPower.Axe_Operate_NPC, actor.Position);
                    break;
            }

            // Doubly-make sure we interact
            actor.Interact();
            GameEvents.FireWorldTransferStart();            
            await Coroutine.Sleep(_sleepTime);
            return retVal;
        }

    }
}
