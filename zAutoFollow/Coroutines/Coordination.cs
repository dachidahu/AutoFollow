﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AutoFollow.Coroutines.Resources;
using AutoFollow.Events;
using AutoFollow.Networking;
using AutoFollow.Resources;
using Buddy.Coroutines;
using Zeta.Bot;
using Zeta.Bot.Logic;
using Zeta.Game;

namespace AutoFollow.Coroutines
{
    public static class Coordination
    {
        private static DateTime? _startedWaiting;
        private static DateTime _ignoreOtherBotsUntilTime;

        private static Stopwatch _teleportTimer = new Stopwatch();

        /// <summary>
        /// Time to wait before starting the profile/ opening the rift etc.
        /// WaitForGameStartDelay() will start sooner than this if the bots are nearby.
        /// </summary>
        public static DateTime StartAllowedTime = DateTime.MinValue;

        private static DateTime _waitUntil = DateTime.MinValue;

        public static DateTime WaitUntil
        {
            get { return _waitUntil; }
        }

        public static void WaitFor(TimeSpan time)
        {
            var newTime = DateTime.UtcNow + time;
            if (DateTime.UtcNow + time > _waitUntil)
                _waitUntil = newTime;
        }

        static Coordination()
        {
            BotMain.OnStart += bot => Reset();
        }

        private static void Reset()
        {
            _startedWaiting = null;
            _ignoreOtherBotsUntilTime = DateTime.MinValue;
        }

        /// <summary>
        /// If the bot is locked out of the current greater rift, wait around until the rift is finished.
        /// </summary>
        public static async Task<bool> WaitForGreaterRiftInProgress()
        {
            if (RiftHelper.IsLockedOutOfRift)
            {
                Log.Info("Locked out of rift, waiting for it to finish.");
                await Coroutine.Sleep(5000);
                return true;
            }
            return false;
        }

        

        /// <summary>
        /// Waits after changing world until all bots are nearby or the duration has elapsed.
        /// </summary>
        public static async Task<bool> WaitAfterChangingWorlds(int durationSeconds = 10)
        {
            var secsSinceWorldChanged = DateTime.UtcNow.Subtract(ChangeMonitor.LastWorldChange).TotalSeconds;
            if (secsSinceWorldChanged < durationSeconds)
            {
                var allFollowersNearby = AutoFollow.CurrentFollowers.All(f => f.IsInSameWorld && f.Distance < 50f);
                if (!allFollowersNearby && !Data.Monsters.Any(m => m.Distance < 30f))
                {
                    await Coroutine.Sleep(1000);
                    return true;
                }
            }
            return false;
        }

        public static async Task<bool> WaitForGemUpgraded()
        {
            if (RiftHelper.RiftQuest.Step == RiftQuest.RiftStep.UrshiSpawned) 
            {
                Log.Warn("Need to wait others upgrade gem.");
                 if (Player.IsInTown)
                 {
                    Log.Warn("Go back to the rift.");
                    await ReturnToGreaterRift();
                    await Coroutine.Sleep(2000);
                    return true;
                 }
                 else
                 {
                    Log.Warn("Ops, already in rift.");
                    await Coroutine.Sleep(1000);
                    return true;
                 }
                 Questing.UpgradeGems();

            }
            return false;
        }

        private static async Task<bool> ReturnToGreaterRift()
        {
            //ActorId: 191492, Type: Gizmo, Name: hearthPortal-46321, Distance2d: 6.981126, CollisionRadius: 8.316568, MinimapActive: 0, MinimapIconOverride: -1, MinimapDisableArrow: 0 
            var returnPortal = Data.Portals.FirstOrDefault(p => p.ActorSnoId == 191492);
            if (returnPortal != null)
            {
                Log.Info("Entering the return portal back to rift... ");
                await Movement.MoveToAndInteract(returnPortal);
            }
            return true;
        }


        /// <summary>
        /// Prevents a greater rift from being started until all bots are ready.
        /// </summary>
        public static async Task<bool> WaitBeforeStartingRift()
        {
            if (!RiftHelper.IsGreaterRiftProfile || RiftHelper.IsGreaterRiftStarted)
                return false;

            if (AutoFollow.CurrentFollowers.Any(f => f.IsVendoring))
            {
                Log.Info("Waiting for followers to finish vendoring.");

                var obelisk = Town.Actors.RiftObelisk;
                if (obelisk != null)
                    await Movement.MoveTo(obelisk.Position);

                await Coroutine.Sleep(3000);
                return true;
            }

            if (_ignoreOtherBotsUntilTime > DateTime.UtcNow)
                return false;

            if (AutoFollow.NumberOfConnectedBots == 0 || !AutoFollow.CurrentFollowers.All(f => f.IsInSameGame))
            {
                if (!_startedWaiting.HasValue)
                {
                    _startedWaiting = DateTime.UtcNow;
                    Log.Info("Waiting for bots to connect/join.");
                    await Coroutine.Sleep(8000);
                    return true;
                }

                var secondsWaiting = DateTime.UtcNow.Subtract(_startedWaiting.Value).TotalSeconds;
                if (secondsWaiting < 60)
                {
                    Log.Info("Waiting for bots to connect/join. Waited {0} seconds", secondsWaiting);
                    await Coroutine.Sleep(8000);
                    return true;
                }

                Log.Info("Waited for {0}s bots to connect.. starting without them.", secondsWaiting);
                await Coroutine.Sleep(5000);
                _startedWaiting = null;
                _ignoreOtherBotsUntilTime = DateTime.UtcNow.AddMinutes(2);
            }

            return false;
        }

        /// <summary>
        /// Waits after joining a game based on the "Leader Start Delay" setting, or until all followers are ready and nearby.
        /// This is to prevent combat starting before all the bots are properly into the game.
        /// </summary>
        public static async Task<bool> WaitForGameStartDelay()
        {
            var time = ChangeMonitor.LastGameJoinedTime > ChangeMonitor.LastLoadedProfileTime
                ? ChangeMonitor.LastGameJoinedTime : ChangeMonitor.LastLoadedProfileTime;

            StartAllowedTime = time.Add(TimeSpan.FromSeconds(Settings.Coordination.DelayAfterJoinGame));

            if (DateTime.UtcNow < StartAllowedTime && Player.IsInTown &&
                !AutoFollow.CurrentParty.All(
                    b => b.IsInTown && b.IsInGame && b.IsInSameWorld && b.Distance < 60f && !b.IsVendoring))
            {
                Log.Debug("Waiting for game start delay to finish {0}",
                    StartAllowedTime.Subtract(DateTime.UtcNow).TotalSeconds);

                await Coroutine.Sleep(500);
                return true;
            }
            return false;
        }

        /// <summary>
        /// If the leader is currently vendoring, start a town run on this bot as well.
        /// </summary>
        public static async Task<bool> StartTownRunWithLeader()
        {
            if (AutoFollow.CurrentLeader.IsVendoring && !Player.IsVendoring)
            {
                BrainBehavior.ForceTownrun("Townrun with Leader");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Teleport to player if possible.
        /// </summary>
        public static async Task<bool> TeleportToPlayer(Message playerMessage, bool safeMode=true)
        {
            if (!CanTeleportToPlayer(playerMessage)) 
            {
                return false;
            }

            if (!_teleportTimer.IsRunning)
                _teleportTimer.Restart();

            var portalNearby = Data.Portals.Any(p => p.Distance < 50f);

            // Allow time for leader to resolve in new world.
            if (!Player.IsInTown && portalNearby && _teleportTimer.ElapsedMilliseconds < 6000)
            {
                Log.Debug("{0} is in a different world... waiting before teleport", playerMessage.HeroName);
                return false;
            }

            _teleportTimer.Stop();

            // Safety check.
            var actor = Data.GetPlayerActor(playerMessage);

            if (actor != null && actor.Distance <= 100f)
            {

                return false;
            }


            Log.Warn("Teleporting to player {0} SameGame={1} SameWorld={2}",
                playerMessage.HeroName, playerMessage.IsInSameGame, playerMessage.IsInSameWorld);

            
            if (!Player.IsInTown && safeMode)
            {
                Log.Warn("Using TownPortal!!!!! {0}", Player.HeroName);
                ZetaDia.Me.UseTownPortal();

                await Coroutine.Sleep(250);

                while (ZetaDia.IsLoadingWorld || ZetaDia.Me.LoopingAnimationEndTime != 0)
                {
                    await Coroutine.Sleep(250);
                    await Coroutine.Yield();
                }
            }

            ZetaDia.Me.TeleportToPlayerByIndex(playerMessage.Index);

            await Coroutine.Sleep(250);

            while (ZetaDia.IsLoadingWorld || ZetaDia.Me.LoopingAnimationEndTime != 0)
            {
                await Coroutine.Sleep(250);
                await Coroutine.Yield();
            }
            Log.Warn("Teleport successfully.");
            return true;
        }

        private static bool CanTeleportToPlayer(Message playerMessage)
        {
            if (!ZetaDia.IsInGame || ZetaDia.IsLoadingWorld) {
                Log.Warn("Can't telport 1");
                return false;
            }

            if (playerMessage.IsInSameWorld && playerMessage.Distance < 100f) {
                Log.Warn("Can't telport 2");
                return false;
            }

            if (playerMessage.IsInGreaterRift && !(RiftHelper.RiftQuest.Step == RiftQuest.RiftStep.UrshiSpawned 
                    || RiftHelper.RiftQuest.Step == RiftQuest.RiftStep.Completed || RiftHelper.RiftQuest.Step == RiftQuest.RiftStep.Cleared))
            {
                Log.Warn("Can't telport 3 {0}", RiftHelper.RiftQuest.Step);
                return false;
            }

            if (ZetaDia.Service.Party.NumPartyMembers <= 1)
            {
                Log.Info("Cant teleport because you are not in a party!");
                return false;
            }

            if (Player.IsCasting)
                return false;

            return true;
        }

        /// <summary>
        /// If we're in a different world to the leader, teleport to him.
        /// </summary>
        public static async Task<bool> TeleportWhenInDifferentWorld(Message player)
        {
            if (player.WorldSnoId <= 0 || Player.CurrentWorldSnoId <= 0)
                return false;

            if ((RiftHelper.IsInRift || player.IsInRift) && RiftHelper.IsGreaterRiftStarted)
                return false;

            if (Player.IsFollower && player.WorldSnoId != Player.CurrentMessage.WorldSnoId && player.IsInSameGame &&
                !player.IsInCombat)
            {
                await Coordination.TeleportToPlayer(player);
                return true;
            }
            return false;
        }

        /// <summary>
        /// If we're too far away from the leader, teleport to him.
        /// </summary>
        public static async Task<bool> TeleportWhenTooFarAway(Message playerMessage)
        {
            if (RiftHelper.IsInGreaterRift || Player.IsInBossEncounter || playerMessage.IsInBossEncounter)
                return false;

            if (Player.IsFollower && playerMessage.WorldSnoId == Player.CurrentMessage.WorldSnoId && playerMessage.IsInSameGame &&
                !playerMessage.IsInCombat && playerMessage.Distance > Settings.Coordination.TeleportDistance)
            {
                Log.Info("{0} is getting quite far away... attempting teleport!", playerMessage.HeroName);
                await Coordination.TeleportToPlayer(playerMessage);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Use portal if it's the one our leader used last.
        /// </summary>
        /// <returns></returns>
        public async static Task<bool> FollowLeaderThroughPortal()
        {
            if (RiftHelper.IsInGreaterRift || RiftHelper.RiftQuest.Step == RiftQuest.RiftStep.UrshiSpawned)
                return false;
            var leaderWasLastInMyCurrentWorld = AutoFollow.CurrentLeader.PreviousWorldSnoId == Player.CurrentWorldSnoId;
            var lastWorldPosition = AutoFollow.CurrentLeader.LastPositionInPreviousWorld;
            if (leaderWasLastInMyCurrentWorld)
            {
                var portalUsed = Data.Portals.Where(p => p.Position.Distance(lastWorldPosition) < 25f).OrderBy(p => p.Position.Distance(lastWorldPosition)).FirstOrDefault();
                if (portalUsed != null)
                {
                    Log.Info("Leader {0} appears to have used this portal here: '{1}' Dist={2}. Following.",
                        AutoFollow.CurrentLeader.HeroName, portalUsed.Name, portalUsed.Distance);

                    if (await Movement.MoveToAndInteract(portalUsed))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// If there is a portal nearby and the bot is standing around doing nothing, take the portal.
        /// </summary>
        public static async Task<bool> UseNearbyPortalWhenIdle()
        {
            if (!Player.IsIdle || Player.IsInTown)
                return false;

            if(RiftHelper.IsInGreaterRift)
                return false;

            if (AutoFollow.CurrentLeader.IsInSameWorld && AutoFollow.CurrentLeader.Distance < 30f)
                return false;

            var nearbyPortal = Data.Portals.FirstOrDefault(p => p.Distance < 30f);
            if (nearbyPortal == null)
                return false;

            if (Navigation.CanRayCast(ZetaDia.Me.Position, nearbyPortal.Position))
                return false;

            Log.Info("Lets use this nearby portal, what could go wrong? Id={0} Distance={1}",
            nearbyPortal.Name, nearbyPortal.Distance);

            await Movement.MoveToAndInteract(nearbyPortal);
            return true;
        }

    }
}