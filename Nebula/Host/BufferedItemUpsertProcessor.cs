using NebulaAPI;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.Nebula.Packets;
using PersonalLogistics.Util;

namespace PersonalLogistics.Nebula.Host
{
    [RegisterPacketProcessor]
    public class BufferedItemUpsertProcessor : BasePacketProcessor<BufferedItemUpsert>
    {
        /// <summary>
        /// Handled by host, tell shipping manager that remote client's inv item has been updated
        /// </summary>
        public override void ProcessPacket(BufferedItemUpsert packet, INebulaConnection conn)
        {
            if (IsClient)
                return;
            var remotePlayerId = PlogPlayerId.FromString(packet.playerId);
            var plogPlayer = PlayerStateContainer.GetPlayer(remotePlayerId, true);
            if (plogPlayer is PlogRemotePlayer remotePlayer)
            {
                Log.Debug($"Processing buffer upsert on behalf of client {remotePlayerId}. Item: {packet.itemId} newCount: {packet.itemCount}");
                remotePlayer.shippingManager.UpsertBufferedItem(packet.itemId, packet.itemCount, packet.gameTick, packet.proliferatorPoints);
            }
            else
            {
                Log.Warn($"invalid state got a local player back while running as host");
            }
        }
    }
}