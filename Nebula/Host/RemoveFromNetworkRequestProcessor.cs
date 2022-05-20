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
            INetworkProvider network = NebulaModAPI.MultiplayerSession.Network;
            var nebulaPlayer = network.PlayerManager.GetPlayer(conn);

            if (stationInfo == null || removed.ItemCount == 0)
            {
                Log.Warn($"Did not find station to remove items from for player. ItemId: {packet.itemId} {removed.ItemCount}");
                nebulaPlayer.SendPacket(new RemoveFromNetworkResponse(
                    packet.clientId, 
                    0,
                    packet.requestGuid,
                    0,
                    0,
                    0,
                    0, 
                    0, 
                    0, 
                    true));
                return;
            }
            Log.Debug($"Removing items from network on behalf of remote player {packet.itemId} {removed.ItemCount}");
            var (energyCost, warperNeeded) = StationStorageManager.CalculateTripEnergyCost(stationInfo, distance);
            var energyFromStation = StationStorageManager.RemoveEnergyFromStation(stationInfo, energyCost);
            if (StationStorageManager.RemoveWarperFromStation(stationInfo))
            {
                warperNeeded = false;
            }
            nebulaPlayer.SendPacket(new RemoveFromNetworkResponse(
                packet.clientId, 
                stationInfo.StationGid,
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