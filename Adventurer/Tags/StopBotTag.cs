using System.Threading.Tasks;
using Adventurer.Util;
using Zeta.Bot;
using Zeta.Bot.Profile;
using Zeta.TreeSharp;
using Zeta.XmlEngine;

namespace Adventurer.Tags
{
    [XmlElement("StopBot")]
    public class StopBotTag : ProfileBehavior
    {
        private bool _isDone;
        public override bool IsDone
        {
            get
            {
                return _isDone;
            }
        }

        protected override Composite CreateBehavior()
        {
            Logger.Info("Stopping Bot");
            return new ActionRunCoroutine(ctx => StopBot());
        }

        private async Task<bool> StopBot()
        {
            BotMain.Stop();
            _isDone = true;
            return true;
        }

        public override void ResetCachedDone(bool force = false)
        {
            _isDone = false;
        }


    }
}
