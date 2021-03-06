﻿using System;
using Adventurer.Game.Events;
using Adventurer.Util;
using Zeta.Game;

namespace Adventurer.Game.Stats
{
    public class ExperienceTracker : PulsingObject
    {
        public double CurrentExperience;
        public double MinExperience = 0;
        public double MaxExperience = 0;
        public double MinExperienceRun = 0;
        public double MaxExperienceRun = 0;
        public double TotalExperience = 0;
        public TimeSpan CurrentTime { get { return DateTime.UtcNow - _startTime; } }
        private DateTime _startTime;
        private DateTime _tickStartTime;
        private TimeSpan _bestTime = TimeSpan.Zero;
        private TimeSpan _worseTime;
        private long _lastSeen;
        private int _tick = 0;
        public bool IsStarted { get; private set; }

        public void Start()
        {
            if (_startTime == DateTime.MinValue)
                _startTime = DateTime.UtcNow;
            _tickStartTime = DateTime.UtcNow; ;
            CurrentExperience = 0;
            _tick++;
            _lastSeen = GetLastSeen();
            EnablePulse();
            IsStarted = true;
            Logger.Info("[XPTracker] Starting a new experience tracking session.");
        }

        public void StopAndReport(string reporterName)
        {
            DisablePulse();
            if (IsStarted)
            {
                UpdateExperience();
                ReportExperience(reporterName);
            }
            IsStarted = false;
        }

        private void ReportExperience(string reporterName)
        {
            var TimeOfLastRun = DateTime.UtcNow - _tickStartTime;
            var TimeOfSession = DateTime.UtcNow - _startTime;
            var AverageRunTime = new TimeSpan(TimeOfSession.Ticks / _tick);
            var ThisExpHour = CurrentExperience / TimeOfLastRun.TotalHours;
            if (_bestTime == TimeSpan.Zero || _bestTime > TimeOfLastRun)
                _bestTime = TimeOfLastRun;
            if (_worseTime < TimeOfLastRun)
                _worseTime = TimeOfLastRun;
            if (MaxExperienceRun < CurrentExperience)
                MaxExperienceRun = CurrentExperience;
            if (MinExperienceRun == 0 || MinExperienceRun > CurrentExperience)
                MinExperienceRun = CurrentExperience;
            if (MaxExperience < ThisExpHour)
                MaxExperience = ThisExpHour;
            if (MinExperience == 0 || MinExperience > ThisExpHour)
                MinExperience = ThisExpHour;
            Logger.Warn("[{0}] Runs count: {1}", reporterName, _tick);
            Logger.Warn("[{0}] This run time: {1}:{2:D2}:{3:D2}", reporterName, TimeOfLastRun.Hours, TimeOfLastRun.Minutes, TimeOfLastRun.Seconds);
            Logger.Warn("[{0}] Average run time: {1}:{2:D2}:{3:D2}", reporterName, AverageRunTime.Hours, AverageRunTime.Minutes, AverageRunTime.Seconds);
            Logger.Warn("[{0}] Best run time: {1}:{2:D2}:{3:D2}", reporterName, _bestTime.Hours, _bestTime.Minutes, _bestTime.Seconds);
            Logger.Warn("[{0}] Worse run time: {1}:{2:D2}:{3:D2}", reporterName, _worseTime.Hours, _worseTime.Minutes, _worseTime.Seconds);
            Logger.Warn("[{0}] This run XP Gained: {1:0,0}", reporterName, CurrentExperience);
            Logger.Warn("[{0}] This run / Hour: {1:0,0}", reporterName, ThisExpHour);
            Logger.Warn("[{0}] Total XP Gained: {1:0,0}", reporterName, TotalExperience);
            Logger.Warn("[{0}] Total XP / Hour: {1:0,0}", reporterName, TotalExperience / TimeOfSession.TotalHours);
            Logger.Warn("[{0}] Best XP / Hour (single run): {1:0,0}", reporterName, MaxExperience);
            Logger.Warn("[{0}] Worse XP / Hour (single run): {1:0,0}", reporterName, MinExperience);
            Logger.Warn("[{0}] Best XP / Single run: {1:0,0}", reporterName, MaxExperienceRun);
            Logger.Warn("[{0}] Worse XP / Single run: {1:0,0}", reporterName, MinExperienceRun);
        }

        private void UpdateExperience()
        {
            var currentLastSeen = GetLastSeen();
            if (_lastSeen < currentLastSeen)
            {
                CurrentExperience += (currentLastSeen - _lastSeen);
                TotalExperience += (currentLastSeen - _lastSeen);
            }
            _lastSeen = currentLastSeen;
        }

        private static long GetLastSeen()
        {
            return ZetaDia.Me.Level == 70
                ? ZetaDia.Me.ParagonCurrentExperience
                : ZetaDia.Me.CurrentExperience;
        }

        protected override void OnPulse()
        {
            UpdateExperience();
        }
    }
}
