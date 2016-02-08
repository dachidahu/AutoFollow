using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Adventurer.Coroutines;
using Adventurer.Coroutines.KeywardenCoroutines;
using Adventurer.Game.Actors;
using Adventurer.Game.Events;
using Adventurer.Util;
using Zeta.Game;
using Zeta.Game.Internals.Actors;

namespace Adventurer.Game.Combat
{
    public class SafeZerg : PulsingObject
    {

        private static SafeZerg _instance;
        public static SafeZerg Instance
        {
            get { return _instance ?? (_instance = new SafeZerg()); }
        }

        private SafeZerg() { }

        private bool _zergEnabled;

        public void EnableZerg()
        {
            EnablePulse();
            _zergEnabled = true;
        }

        public void DisableZerg()
        {
            DisablePulse();
            _zergEnabled = false;
            TargetingHelper.TurnCombatOn();
        }

        protected override void OnPulse()
        {
            ZergCheck();
        }

        private void ZergCheck()
        {
            if (!_zergEnabled) { return; }
            var corruptGrowthDetectionRadius = ZetaDia.Me.ActorClass == ActorClass.Barbarian ? 30 : 20;
            var combatState = false;

            if (!combatState && ZetaDia.Me.HitpointsCurrentPct <= 0.8f)
            {
                combatState = true;
            }

            if (!combatState && 
                
                ZetaDia.Actors.GetActorsOfType<DiaUnit>(true).Any(u => u.IsFullyValid() && u.IsAlive && ( 
//                u.CommonData.IsElite || u.CommonData.IsRare || u.CommonData.IsUnique ||
                KeywardenDataFactory.GoblinSNOs.Contains(u.ActorSnoId) || (KeywardenDataFactory.A4CorruptionSNOs.Contains(u.ActorSnoId) && u.IsAlive & u.Position.Distance(AdvDia.MyPosition) <= corruptGrowthDetectionRadius))
                ))
               
            {
                combatState = true;
            }

            var keywarden = KeywardenDataFactory.Items.FirstOrDefault(kw => kw.Value.WorldId == AdvDia.CurrentWorldId);
            if (!combatState && keywarden.Value != null && keywarden.Value.IsAlive)
            {
                var kwActor = ActorFinder.FindUnit(keywarden.Value.KeywardenSNO);
                if (kwActor != null && kwActor.Distance < 80f)
                {
                    Logger.Verbose("Turning off zerg because {0} is nearby. Distance={1}", kwActor.Name, kwActor.Distance);
                    combatState = true;
                }
            }

            var closeUnitsCount = ZetaDia.Actors.GetActorsOfType<DiaUnit>(true).Count(u => u.IsFullyValid() && u.IsHostile && u.IsAlive && u.Position.Distance(AdvDia.MyPosition) <= 15f);
            if (!combatState && (closeUnitsCount >= 8 || closeUnitsCount >= 3 && ZetaDia.Me.HitpointsCurrentPct <= 0.6))
            {
                combatState = true;
            }

            if (combatState)
            {
                TargetingHelper.TurnCombatOn();
            }
            else
            {
                TargetingHelper.TurnCombatOff();
            }
        }
    }
}
