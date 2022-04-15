using NebulaAPI;
using PersonalLogistics.Logistics;
using PersonalLogistics.Nebula.Packets;

namespace PersonalLogistics.Nebula.Client
{
    [RegisterPacketProcessor]
    public class ItemSummaryUpdateProcessor : BasePacketProcessor<ItemSummaryUpdate>
    {
        public override void ProcessPacket(ItemSummaryUpdate packet, INebulaConnection conn)
        {
            if (IsHost)
            {
                return;
            }
            LogisticsNetwork.UpdateItemSummary(packet.itemId, packet.ToByItemSummary());
        }
    }
}