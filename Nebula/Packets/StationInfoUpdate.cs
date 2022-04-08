using System.IO;
using PersonalLogistics.Logistics;
using PersonalLogistics.ModPlayer;

namespace PersonalLogistics.Nebula.Packets
{
    public class StationInfoUpdate
    {
        public byte[] data { get; set; }

        public StationInfoUpdate()
        {
        }

        public StationInfoUpdate(StationInfo stationInfo)
        {
            var memoryStream = new MemoryStream();
            var w = new BinaryWriter(memoryStream);
            stationInfo.Export(w);
            data = memoryStream.ToArray();
        }
        
    }
}