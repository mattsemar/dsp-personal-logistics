using System.IO;
using NebulaAPI;
using PersonalLogistics.Logistics;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.Nebula.Packets;
using PersonalLogistics.Util;

namespace PersonalLogistics.Nebula.Client
{
    [RegisterPacketProcessor]
    public class StationInfoUpdateProcessor : BasePacketProcessor<StationInfoUpdate>
    {
        public override void ProcessPacket(StationInfoUpdate packet, INebulaConnection conn)
        {
            if (IsHost)
            {
                return;
            }
            Log.Debug($"Got new station to update");
            var memoryStream = new MemoryStream(packet.data);
            var r = new BinaryReader(memoryStream);
            var stationInfo = StationInfo.Import(r);
            LogisticsNetwork.CreateOrUpdateStation(stationInfo);
            // next step is to add it into our local list of stations
        }
    }
}