using PersonalLogistics.ModPlayer;

namespace PersonalLogistics.Nebula.Packets
{
    public class BufferedItemUpsert
    {
        public string playerId { get; set; }
        public int itemId { get; set; }
        public int itemCount { get; set; }
        public int proliferatorPoints { get; set; }
        public long gameTick { get; set; }

        public BufferedItemUpsert()
        {
        }

        public BufferedItemUpsert(PlogPlayerId playerId, int itemId, int itemCount, int proliferatorPoints, long gameTick)
        {
            this.playerId = playerId.ToString();
            this.itemId = itemId;
            this.itemCount = itemCount;
            this.proliferatorPoints = proliferatorPoints;
            this.gameTick = gameTick;
        }
    }
}