﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Adventurer.Game.Actors;
using Adventurer.Game.Exploration;
using Adventurer.Game.Rift;
using Adventurer.Settings;
using Adventurer.Util;
using Buddy.Coroutines;
using Zeta.Bot;
using Zeta.Bot.Coroutines;
using Zeta.Bot.Logic;
using Zeta.Bot.Navigation;
using Zeta.Common;
using Zeta.Common.Helpers;
using Zeta.Game;
using Zeta.Game.Internals.Actors;
using Logger = Adventurer.Util.Logger;

namespace Adventurer.Coroutines.RiftCoroutines
{
    public sealed class UpgradeGemsCoroutine : ICoroutine
    {
        public UpgradeGemsCoroutine()
        {
            Id = Guid.NewGuid();
        }

        private readonly WaitTimer _pulseTimer = new WaitTimer(TimeSpan.FromMilliseconds(250));
        private int _gemUpgradesLeft;
        private bool _enableGemUpgradeLogs;
        private bool _isPulsing;
        private Vector3 _urshiLocation;

        private readonly InteractionCoroutine _interactWithUrshiCoroutine = new InteractionCoroutine(RiftData.UrshiSNO, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1), 3);

        #region State

        private enum States
        {
            NotStarted,
            UrshiSpawned,
            SearchingForUrshi,
            MovingToUrshi,
            InteractingWithUrshi,
            UpgradingGems,
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
                    Logger.Debug("[UpgradeGems] " + value);
                }
                _state = value;
            }
        }


        private async Task<bool> GetCoroutine()
        {
            if (_isPulsing)
            {
                PulseChecks();
            }

            switch (State)
            {
                case States.NotStarted:
                    return NotStarted();
                case States.UrshiSpawned:
                    return UrshiSpawned();
                case States.SearchingForUrshi:
                    return await SearchingForUrshi();
                case States.MovingToUrshi:
                    return await MovingToUrshi();
                case States.InteractingWithUrshi:
                    return await InteractingWithUrshi();
                case States.UpgradingGems:
                    return await UpgradingGems();
                case States.Completed:
                    return Completed();
                case States.Failed:
                    return Failed();
            }
            return false;
        }

        public Guid Id { get; set; }

        #endregion

        private bool NotStarted()
        {
            State = States.UrshiSpawned;
            return false;
        }

        private bool UrshiSpawned()
        {
            EnablePulse();
            State = States.SearchingForUrshi;
            return false;
        }

        private async Task<bool> SearchingForUrshi()
        {
            EnablePulse();
            if (_urshiLocation != Vector3.Zero)
            {
                State = States.MovingToUrshi;
                return false;
            }
            if (!await ExplorationCoroutine.Explore(new HashSet<int> {AdvDia.CurrentLevelAreaId}))
            {
                Logger.Info("[UpgradeGems] Exploration for urshi has failed, the sadness!");
                State = States.Failed;
                return false;
            }

            Logger.Info("[UpgradeGems] Where are you, my dear Urshi!");
            ScenesStorage.Reset();
            return false;
        }

        private async Task<bool> MovingToUrshi()
        {
            EnablePulse();
            if (!await NavigationCoroutine.MoveTo(_urshiLocation, 5)) return false;
            _urshiLocation = Vector3.Zero;
            State = States.InteractingWithUrshi;
            return false;
        }

        private async Task<bool> InteractingWithUrshi()
        {
            DisablePulse();
            if (RiftData.VendorDialog.IsVisible)
            {
                State = States.UpgradingGems;
                return false;
            }

            var urshi = ZetaDia.Actors.GetActorsOfType<DiaUnit>().FirstOrDefault(a => a.IsFullyValid() && a.ActorSnoId == RiftData.UrshiSNO);
            if (urshi == null)
            {
                Logger.Debug("[UpgradeGems] Can't find the Urshi lady :(");
                State = States.Failed;
                return false;
            }

            if (!await _interactWithUrshiCoroutine.GetCoroutine())
                return false;

            await Coroutine.Wait(2500, () => RiftData.VendorDialog.IsVisible);

            _interactWithUrshiCoroutine.Reset();

            if (!RiftData.VendorDialog.IsVisible)
            {
                return false;
            }

            _gemUpgradesLeft = 3;
            _enableGemUpgradeLogs = false;
            State = States.UpgradingGems;
            return false;
        }        

        private async Task<bool> UpgradingGems()
        {
            if (RiftData.VendorDialog.IsVisible && RiftData.ContinueButton.IsVisible && RiftData.ContinueButton.IsEnabled)
            {
                Logger.Debug("[UpgradeGems] Clicking to Continue button.");
                RiftData.ContinueButton.Click();
                RiftData.VendorCloseButton.Click();
                await Coroutine.Sleep(250);
                return false;
            }

            var gemToUpgrade = GetUpgradeTarget(_enableGemUpgradeLogs);
            if (gemToUpgrade == null)
            {
                Logger.Info("[UpgradeGems] I couldn't find any gems to upgrade, failing.");
                State = States.Failed;
                return false;

            }
            _enableGemUpgradeLogs = false;
            if (AdvDia.RiftQuest.Step == RiftStep.Cleared)
            {
                Logger.Debug("[UpgradeGems] Rift Quest is completed, returning to town");
                State = States.Completed;
                return false;
            }

            Logger.Debug("[UpgradeGems] Gem upgrades left before the attempt: {0}", ZetaDia.Me.JewelUpgradesLeft);
            if (!await CommonCoroutines.AttemptUpgradeGem(gemToUpgrade))
            {
                Logger.Debug("[UpgradeGems] Gem upgrades left after the attempt: {0}", ZetaDia.Me.JewelUpgradesLeft);
                return false;
            }
            var gemUpgradesLeft = ZetaDia.Me.JewelUpgradesLeft;
            if (_gemUpgradesLeft != gemUpgradesLeft)
            {
                _gemUpgradesLeft = gemUpgradesLeft;
                _enableGemUpgradeLogs = true;
            }
            if (gemUpgradesLeft == 0)
            {
                Logger.Debug("[UpgradeGems] Finished all upgrades, returning to town.");

                State = States.Completed;
                return false;
            }


            return false;
        }

        private ACDItem GetUpgradeTarget(bool enableLog)
        {
            ZetaDia.Actors.Update();
            var gems = PluginSettings.Current.Gems;
            gems.UpdateGems(ZetaDia.Me.InTieredLootRunLevel + 1, PluginSettings.Current.GreaterRiftPrioritizeEquipedGems);

            var minChance = PluginSettings.Current.GreaterRiftGemUpgradeChance;
            var upgradeableGems =
                gems.Gems.Where(g => g.UpgradeChance >= minChance && !g.MaxRank).ToList();
            if (upgradeableGems.Count == 0)
            {
                if (enableLog) Logger.Info("[UpgradeGems] Couldn't find any gems which is over the minimum upgrade change, upgrading the gem with highest upgrade chance");
                upgradeableGems = gems.Gems.Where(g => !g.MaxRank).OrderByDescending(g => g.UpgradeChance).ToList();
            }
            if (upgradeableGems.Count == 0)
            {
                if (enableLog) Logger.Info("[UpgradeGems] Looks like you have no legendary gems, failing.");
                State = States.Failed;
                return null;
            }
            var gemToUpgrade = upgradeableGems.First();
            if (enableLog) Logger.Info("[UpgradeGems] Attempting to upgrade {0}", gemToUpgrade.DisplayName);
            var acdGem =
                ZetaDia.Actors.GetActorsOfType<ACDItem>()
                    .FirstOrDefault(
                        i =>
                            i.ItemType == ItemType.LegendaryGem && i.ActorSnoId == gemToUpgrade.SNO &&
                            i.JewelRank == gemToUpgrade.Rank);
            return acdGem;
        }

        private void EnablePulse()
        {
            if (!_isPulsing)
            {
                Logger.Debug("[UpgradeGems] Registered to pulsator.");
                Pulsator.OnPulse += OnPulse;
                _isPulsing = true;
            }
        }

        private void DisablePulse()
        {
            if (_isPulsing)
            {
                Logger.Debug("[UpgradeGems] Unregistered from pulsator.");
                Pulsator.OnPulse -= OnPulse;
                _isPulsing = false;
            }
        }

        private void OnPulse(object sender, EventArgs e)
        {
            if (_pulseTimer.IsFinished)
            {
                _pulseTimer.Stop();
                Scans();
                _pulseTimer.Reset();
            }
        }

        private void Scans()
        {
            switch (State)
            {
                case States.SearchingForUrshi:
                    ScanForUrshi();
                    return;
            }
        }

        private void ScanForUrshi()
        {
            var urshi =
                ZetaDia.Actors.GetActorsOfType<DiaUnit>()
                    .FirstOrDefault(a => a.IsFullyValid() && a.ActorSnoId == RiftData.UrshiSNO);
            if (urshi != null)
            {
                _urshiLocation = urshi.Position;
            }
            if (_urshiLocation != Vector3.Zero)
            {
                Logger.Info("[UpgradeGems] Urshi is near.");
                State = States.MovingToUrshi;
                Logger.Debug("[UpgradeGems] Found Urshi at distance {0}", AdvDia.MyPosition.Distance2D(_urshiLocation));
            }
        }

        private void PulseChecks()
        {
            if (BrainBehavior.IsVendoring || !ZetaDia.IsInGame || ZetaDia.IsLoadingWorld || ZetaDia.IsPlayingCutscene)
            {
                DisablePulse();
            }            
        }

        public void Dispose()
        {
            DisablePulse();
        }

        Task<bool> ICoroutine.GetCoroutine()
        {
            return GetCoroutine();
        }

        public void Reset()
        {
            DisablePulse();
            State = States.NotStarted;
        }

        private bool Completed()
        {
            PluginCommunicator.BroadcastGemUpgradRequest();
            DisablePulse();
            return true;
        }
        private bool Failed()
        {
            DisablePulse();
            return true;
        }
    }
}
