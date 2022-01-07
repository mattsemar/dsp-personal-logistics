using System;
using NebulaAPI;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.Nebula.Packets;
using PersonalLogistics.Util;

namespace PersonalLogistics.Nebula.Host
{
    [RegisterPacketProcessor]
    public class DesiredItemUpdateProcessor : BasePacketProcessor<DesiredItemUpdate>
    {
        public override void ProcessPacket(DesiredItemUpdate packet, INebulaConnection conn)
        {
            if (IsClient)
                return;
            Log.Debug($"Processing desiredItemUpdate request for client {packet.clientId}");
            var remotePlayerId = PlogPlayerId.FromString(packet.clientId);
            var plogPlayer = PlayerStateContainer.GetPlayer(remotePlayerId);
            plogPlayer.inventoryManager.SetDesiredAmount(packet.itemId, packet.requestMin, packet.recycleMax);
        }
    }
}