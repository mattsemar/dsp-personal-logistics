using NebulaAPI;
using PersonalLogistics.Model;

namespace PersonalLogistics.Nebula.Packets
{
    public class AddToNetworkResponse
    {
        public string clientId { get; set; }
        public int itemId { get; set; }
        public int remainingCount { get; set; }
        public int remainingProliferatorPoints { get; set; }

        public AddToNetworkResponse()
        {
        }

        public AddToNetworkResponse(string clientId, int itemId, ItemStack stack)
        {
            this.clientId = clientId;
            this.itemId = itemId;
            remainingCount = stack.ItemCount;
            remainingProliferatorPoints = stack.ProliferatorPoints;
        }
    }
}