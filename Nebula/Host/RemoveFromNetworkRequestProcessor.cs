using NebulaAPI;
using PersonalLogistics.Logistics;
using PersonalLogistics.Nebula.Packets;
using PersonalLogistics.Util;

namespace PersonalLogistics.Nebula.Host
{
    [RegisterPacketProcessor]
    public class RemoveFromNetworkRequestProcessor : BasePacketProcessor<RemoveFromNetworkRequest>
    {
        public override void ProcessPacket(RemoveFromNetworkRequest packet, INebulaConnection conn)
        {
            if (IsClient)
                return;
            var (distance, removed, stationInfo) = LogisticsNetwork.RemoveItem(packet.playerUPosition.ToVectorLF3(), packet.playerPosition.ToVector3(), packet.itemId, packet.itemCount);
            Log.Debug($"Removing items from network on behalf of remote player {packet.itemId}");
            INetworkProvider network = NebulaModAPI.MultiplayerSession.Network;
            var nebulaPlayer = network.PlayerManager.GetPlayer(conn);
            var (energyCost, warperNeeded) = StationStorageManager.CalculateTripEnergyCost(stationInfo, distance);
            var energyFromStation = StationStorageManager.RemoveEnergyFromStation(stationInfo, energyCost);
            if (StationStorageManager.RemoveWarperFromStation(stationInfo))
            {
                warperNeeded = false;
            }
            nebulaPlayer.SendPacket(new RemoveFromNetworkResponse(
                packet.clientId, 
                packet.requestGuid,
                distance,
                removed.ItemCount,
                removed.ProliferatorPoints,
                stationInfo.PlanetInfo.PlanetId, 
                stationInfo.StationId, 
                energyCost  - energyFromStation, 
                warperNeeded));
        }
    }
}