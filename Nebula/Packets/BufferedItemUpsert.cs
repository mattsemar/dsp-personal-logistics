using PersonalLogistics.ModPlayer;

namespace PersonalLogistics.Nebula.Packets
{
    public class BufferedItemUpsert
    {
        public string playerId { get; set; }
        public int itemId { get; set; }
        public int itemCount { get; set; }
        public long gameTick { get; set; }

        public BufferedItemUpsert()
        {
        }

        public BufferedItemUpsert(PlogPlayerId playerId, int itemId, int itemCount, long gameTick)
        {
            this.playerId = playerId.ToString();
            this.itemId = itemId;
            this.itemCount = itemCount;
            this.gameTick = gameTick;
        }
    }
}