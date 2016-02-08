using System.Runtime.Serialization;
using Zeta.Game;
using Zeta.Game.Internals.Actors;

namespace Adventurer.Settings
{
    [DataContract]
    public class AdventurerGem
    {
        public int Guid { get; set; }
        [DataMember]
        public int SNO { get; set; }
        [DataMember]
        public int Rank { get; set; }

        public string DisplayRank
        {
            get
            {
                return MaxRank ? "MAX" : Rank.ToString();
            }
        }

        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public int UpgradeChance { get; set; }
        [DataMember]
        public bool IsEquiped { get; set; }
        [DataMember]
        public bool MaxRank { get; set; }

        public string DisplayName
        {
            get { return string.Format("{0} (Rank: {1}, Upgrade Chance: {2}%)", Name, Rank, UpgradeChance); }
        }

        public AdventurerGem(ACDItem gem, int griftLevel)
        {
            Guid = gem.ACDId;
            SNO = gem.ActorSnoId;
            Rank = gem.JewelRank;
            Name = gem.Name;
            MaxRank = (Rank == 50 && (SNO == 428355 || SNO == 405796 || SNO == 405797 || SNO == 405803));
            IsEquiped = !MaxRank && gem.InventorySlot == InventorySlot.Socket;
            UpgradeChance = MaxRank ? 0 : CalculateUpgradeChance(griftLevel);
        }

        public void UpdateUpgradeChance(int griftLevel)
        {
            UpgradeChance = CalculateUpgradeChance(griftLevel);
        }

        private int CalculateUpgradeChance(int griftLevel)
        {
            var result = griftLevel - Rank;
            if (result >= 10) return 100;
            if (result >= 9) return 90;
            if (result >= 8) return 80;
            if (result >= 7) return 70;
            if (result >= 0) return 60;
            if (result >= -1) return 30;
            if (result >= -2) return 15;
            if (result >= -3) return 8;
            if (result >= -4) return 4;
            if (result >= -5) return 2;
            if (result >= -15) return 1;
            return 0;
        }
    }
}