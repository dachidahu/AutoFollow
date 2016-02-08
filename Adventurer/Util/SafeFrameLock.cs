using System;
using GreyMagic;
using Zeta.Bot;
using Zeta.Game;
using System.Threading;
using Adventurer.Game.Events;

namespace Adventurer.Util
{
    public static class SafeFrameLock
    {
        public static SafeFrameLockExecutionResult ExecuteWithinFrameLock(Action action, bool updateActors = false)
        {
            var result = new SafeFrameLockExecutionResult { Success = true };
            FrameLock frameLock = null;
            var frameLockAcquired = false;
            if (!BotEvents.IsBotRunning)
            {
                Logger.Verbose("Acquiring Framelock");
                if (updateActors) ZetaDia.Actors.Update();
                frameLock = ZetaDia.Memory.AcquireFrame(true);
                frameLockAcquired = true;
            }
            try
            {
                action.Invoke();
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Exception = ex;
            }
            finally
            {
                if (frameLockAcquired)
                {
                    Logger.Verbose("Releasing Framelock");
                    frameLock.Dispose();
                }
            }
            return result;
        }


    }

    public class SafeFrameLockExecutionResult
    {
        public bool Success { get; set; }
        public Exception Exception { get; set; }

    }
}
