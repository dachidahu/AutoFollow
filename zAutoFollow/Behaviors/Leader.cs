﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoFollow.Behaviors.Structures;
using AutoFollow.Coroutines;
using AutoFollow.Coroutines.Resources;
using AutoFollow.Events;
using AutoFollow.Networking;
using AutoFollow.Resources;
using Buddy.Coroutines;
using Zeta.Bot;
using Zeta.Bot.Logic;
using Zeta.Common;
using Zeta.Game;
using Zeta.Game.Internals;
using Zeta.Game.Internals.Actors;
using Zeta.Game.Internals.Service;
using Zeta.TreeSharp;
using Action = Zeta.TreeSharp.Action;

namespace AutoFollow.Behaviors
{
    public class Leader : BaseBehavior
    {
        public override BehaviorCategory Category
        {
            get { return BehaviorCategory.Leader; }
        }

        public override BehaviorType Type
        {
            get { return BehaviorType.Lead; }
        }

        public override string Name
        {
            get { return "Leader"; }
        }

        public override void OnPulse()
        {

        }

        public override async Task<bool> OutOfGameTask()
        {
            if (await base.OutOfGameTask())
                return true;

            if (await Party.StartGameWhenPartyReady())
                return true;

            if (Settings.Misc.AvoidUnknownPlayers && await Party.LeavePartyUnknownPlayersInGame())
                return true;

            if (await Party.WaitForPlayersToLeaveGame())
                return true;

            if (AutoFollow.CurrentParty.Any(b => b.IsLoadingWorld))
            {
                Log.Info("Waiting for bots to finish loading...");
                await Coroutine.Sleep(500);
                return true;
            }

            if (AutoFollow.CurrentParty.Any(b => !b.IsInParty))
            {
                Log.Info("Waiting for bots to join party...");
                return true;
            }

            var forcedWaitUntil = ChangeMonitor.LastBotStartedTime + TimeSpan.FromSeconds(10);
            if (DateTime.UtcNow < forcedWaitUntil)
            {
                Log.Info("Waiting after bot has just started. Remaining={0}s", forcedWaitUntil.Subtract(DateTime.UtcNow).TotalSeconds);
                await Coroutine.Sleep(1000);
                return true;
            }

            // Allow DB to run normal out of game hook to start a new game
            return false;            
        }
        
        public override async Task<bool> InGameTask()
        {
            // Returning True => go to next tick immediately, execution starts again from top of the tree.
            // Returning False => allow execution to continue to lower hooks. Such as profiles, Adventurer.

            if (await Coordination.WaitForGemUpgraded()) 
            {
                Log.Info("Leader is going to upgrade gem.");
                return false;            
            }


            if (!AutoFollow.CurrentLeader.IsValid)
                return false;

            if (!Service.IsConnected || AutoFollow.NumberOfConnectedBots == 0)
                return false;

            if (await Coordination.WaitForGameStartDelay())
                return true;

            if (await Coordination.WaitBeforeStartingRift())
                return true;

            if (await Coordination.WaitAfterChangingWorlds())
                return true;

            return false;
        }

        public override async Task<bool> OnInTrouble(Message sender, EventData e)
        {
            if (e.IsFollowerEvent && !Data.Monsters.Any(m => m.Distance <= 80f) && sender.IsInSameWorld)
            {
                Log.Info("My minion needs help! Teleporting to {0}. Distance={1}", sender.HeroName, sender.Distance);
                await Coordination.TeleportToPlayer(sender);
            }
            return false;
        }

        public override async Task<bool> OnWorldAreaChanged(Message sender, EventData e)
        {
            if (e.IsFollowerEvent)
            {
                Log.Info("My minion {0} changed world from {1} to {2}", sender.HeroName, e.OldValue, e.NewValue);
            }
            return false;
        }

        public override async Task<bool> OnEngagedElite(Message sender, EventData e)
        {
            if (e.IsFollowerEvent)
            {
                Log.Info("My minion {0} is attacking a Unique! {1} at {2} DistanceFromMe={3}",
                    sender.HeroName, sender.CurrentTarget.Name, sender.Position, ZetaDia.Me.Position.Distance(sender.CurrentTarget.Position));
            }
            return true;
        }

        public override async Task<bool> OnInviteRequest(Message sender, EventData e)
        {
            if (e.IsFollowerEvent)
            {
                Log.Info("My minion {0} is requesting a party invite!", sender.HeroName);
                await Party.InviteFollower(sender);
                
                if(DateTime.UtcNow.Subtract(Coordination.StartAllowedTime).TotalSeconds <= 30)
                    Coordination.StartAllowedTime = DateTime.UtcNow.Add(TimeSpan.FromSeconds(30));
            }
            return true;
        }
    }
}