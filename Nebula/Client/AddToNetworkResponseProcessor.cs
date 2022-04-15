using NebulaAPI;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.Nebula.Packets;

namespace PersonalLogistics.Nebula.Client
{
    [RegisterPacketProcessor]
    public class AddToNetworkResponseProcessor : BasePacketProcessor<AddToNetworkResponse>
    {
        public override void ProcessPacket(AddToNetworkResponse packet, INebulaConnection conn)
        {
            if (IsHost || PlogPlayerRegistry.LocalPlayer().playerId.ToString() != packet.clientId)
                return;
            PlogPlayerRegistry.LocalPlayer().shippingManager.CompleteRemoteAdd(packet);
        }
    }
}