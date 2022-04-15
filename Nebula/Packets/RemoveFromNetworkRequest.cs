using NebulaAPI;
using UnityEngine;

namespace PersonalLogistics.Nebula.Packets
{
    public class RemoveFromNetworkRequest
    {
        public string clientId { get; set; }
        public string requestGuid { get; set; }
        public Double3 playerUPosition { get; set; }
        public Float3 playerPosition { get; set; }
        public int itemId { get; set; }
        public int itemCount { get; set; }

        public RemoveFromNetworkRequest()
        {
        }

        public RemoveFromNetworkRequest(string clientId, string requestGuid, VectorLF3 playerUPosition, Vector3 playerPosition, int itemId, int itemCount)
        {
            this.clientId = clientId;
            this.requestGuid = requestGuid;
            this.playerUPosition = new Double3(playerUPosition.x, playerUPosition.y, playerUPosition.z);
            this.playerPosition = new Float3(playerPosition.x, playerPosition.y, playerPosition.z);
            this.itemId = itemId;
            this.itemCount = itemCount;
        }
    }
}