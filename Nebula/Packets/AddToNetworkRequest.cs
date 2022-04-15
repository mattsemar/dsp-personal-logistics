using NebulaAPI;
using PersonalLogistics.Model;
using PersonalLogistics.ModPlayer;

namespace PersonalLogistics.Nebula.Packets
{
    public class AddToNetworkRequest
    {
        public string clientId { get; set; }
        public Double3 playerUPosition { get; set; }
        public int itemId { get; set; }
        public int itemCount { get; set; }
        public int proliferatorPoints { get; set; }

        public AddToNetworkRequest()
        {
        }

        public AddToNetworkRequest(PlogPlayerId clientId, VectorLF3 playerUPosition, int itemId, ItemStack stack)
        {
            this.clientId = clientId.ToString();
            this.playerUPosition = new Double3(playerUPosition.x, playerUPosition.y, playerUPosition.z);
            this.itemId = itemId;
            itemCount = stack.ItemCount;
            proliferatorPoints = stack.ProliferatorPoints;
        }
    }
}