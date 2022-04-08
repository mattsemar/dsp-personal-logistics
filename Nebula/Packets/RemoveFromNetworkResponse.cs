namespace PersonalLogistics.Nebula.Packets
{
    public class RemoveFromNetworkResponse
    {
        public string clientId { get; set; }
        public string requestGuid { get; set; }
        public double distance { get; set; }
        public int removedCount { get; set; }
        public int removedAcc { get; set; }
        public int planetId { get; set; }
        public int stationId { get; set; }
        public long tripEnergyCost { get; set; }
        public bool warperNeeded { get; set; }

        public RemoveFromNetworkResponse()
        {
        }

        public RemoveFromNetworkResponse(string clientId, string requestGuid, double distance, int removedCount, int removedAcc, int planetId, int stationId, long tripEnergyCost, bool warperNeeded)
        {
            this.clientId = clientId;
            this.requestGuid = requestGuid;
            this.distance = distance;
            this.removedCount = removedCount;
            this.removedAcc = removedAcc;
            this.planetId = planetId;
            this.stationId = stationId;
            this.tripEnergyCost = tripEnergyCost;
            this.warperNeeded = warperNeeded;
        }
    }
}