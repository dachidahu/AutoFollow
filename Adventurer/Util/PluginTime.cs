﻿using System.Diagnostics;

namespace Adventurer.Util
{
    public static class PluginTime
    {
        private static readonly Stopwatch Stopwatch = new Stopwatch();

        static PluginTime()
        {
            Stopwatch.Start();
        }

        public static long CurrentMillisecond
        {
            get { return Stopwatch.ElapsedMilliseconds; }
        }

        public static bool ReadyToUse(long lastCast, long cooldown)
        {
            //if (lastCast == 0) lastCast = long.MaxValue;
            return (CurrentMillisecond - lastCast > cooldown);
        }
    }
}
