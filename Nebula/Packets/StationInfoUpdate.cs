using System.IO;
using PersonalLogistics.Logistics;
using PersonalLogistics.Util;
using UnityEngine;

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
            using var memoryStream = new MemoryStream();
            using var w = new BinaryWriter(memoryStream);
            if (stationInfo.StationGid == 0 && stationInfo.StationId != 1)
            {
                Log.Warn($"Station info should not have 0 gid \n {JsonUtility.ToJson(stationInfo)}");
            }
            stationInfo.Export(w);
            data = memoryStream.ToArray();
        }
    }
}