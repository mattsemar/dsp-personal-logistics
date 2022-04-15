using NebulaAPI;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.Nebula.Packets;
using PersonalLogistics.SerDe;
using PersonalLogistics.Util;

namespace PersonalLogistics.Nebula.Client
{
    [RegisterPacketProcessor]
    public class RemoveFromNetworkResponseProcessor : BasePacketProcessor<RemoveFromNetworkResponse>
    {
        public override void ProcessPacket(RemoveFromNetworkResponse packet, INebulaConnection conn)
        {
            if (PlogPlayerRegistry.LocalPlayer().playerId.ToString() != packet.clientId)
                return;
            PlogPlayerRegistry.LocalPlayer().shippingManager.CompleteRemoteRequestRemove(packet);
        }
    }
}