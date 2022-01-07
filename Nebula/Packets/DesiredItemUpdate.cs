using PersonalLogistics.ModPlayer;

namespace PersonalLogistics.Nebula.Packets
{
    public class DesiredItemUpdate
    {
        public string clientId { get; set; }
        public int itemId { get; set; }
        public int requestMin { get; set; }
        public int recycleMax { get; set; }

        public DesiredItemUpdate()
        {
        }

        public DesiredItemUpdate(PlogPlayerId playerId, int itemId, int requestMin, int recycleMax)
        {   
            clientId = playerId.ToString();
            this.itemId = itemId;
            this.requestMin = requestMin;
            this.recycleMax = recycleMax;
        }
    }
}