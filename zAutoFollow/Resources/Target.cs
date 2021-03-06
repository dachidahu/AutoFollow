﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zeta.Common;
using Zeta.Game;
using Zeta.Game.Internals.Actors;

namespace AutoFollow.Resources
{
    [Serializable]
    public class Target : ITargetable
    {
        public Target()
        {
        }
        public Target(DiaObject actor)
        {
            if (!Data.IsValid(actor))
                return;

            Id = actor.ActorSnoId;
            AcdId = actor.ACDId;
            Name = actor.Name;
            WorldSnoId = Player.CurrentWorldSnoId;

            var quality = actor.CommonData.MonsterQualityLevel;
            if (!Enum.IsDefined(typeof(MonsterQuality), quality) || (int) quality == -1)
                quality = MonsterQuality.Normal;

            Quality = quality;
            Position = actor.Position;            
        }

        public int Id { get; set; }
        public int AcdId { get; set; }
        public string Name { get; set; }
        public MonsterQuality Quality { get; set; }
        public Vector3 Position { get; set; }
        public int WorldSnoId { get; set; }

        public float Distance
        {
            get { return Player.Position.Distance(Position); }
        }

        public override string ToString()
        {
            return string.Format(
                "( Id: {0} AcdId: {1} Name: {2} Quality: {3} Position: {4} )",
                Id, AcdId, Name, Quality, Position
                );
        }

    }
}
