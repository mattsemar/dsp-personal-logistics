using System.IO;
using NebulaAPI;
using PersonalLogistics.Logistics;
using PersonalLogistics.Nebula.Packets;

namespace PersonalLogistics.Nebula.Client
{
    [RegisterPacketProcessor]
    public class StationInfoUpdateProcessor : BasePacketProcessor<StationInfoUpdate>
    {
        public override void ProcessPacket(StationInfoUpdate packet, INebulaConnection conn)
        {
            if (IsHost || NebulaLoadState.IsMultiplayerHost())
            {
                return;
            }

            using var memoryStream = new MemoryStream(packet.data);
            using var r = new BinaryReader(memoryStream);
            var stationInfo = StationInfo.Import(r);
            LogisticsNetwork.CreateOrUpdateStation(stationInfo);
        }
    }
}