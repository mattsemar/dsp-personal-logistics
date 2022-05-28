using System;
using System.IO;
using PersonalLogistics.ModPlayer;

namespace PersonalLogistics.Nebula.Packets
{
    public class ClientStateRequest
    {
        public byte[] data { get; set; }

        public ClientStateRequest()
        {
        }

        public ClientStateRequest(PlogPlayerId playerId)
        {
            using var memoryStream = new MemoryStream();
            using var w = new BinaryWriter(memoryStream);
            w.Write(playerId.gameSeed);
            w.Write(playerId.assignedId.ToByteArray());
            data = memoryStream.ToArray();
        }

        public static PlogPlayerId DecodePlayerId(ClientStateRequest request)
        {
            using var memoryStream = new MemoryStream(request.data);
            var r = new BinaryReader(memoryStream);
            var gameSeed = r.ReadInt32();
            var guid = new Guid(r.ReadBytes(16));
            return new PlogPlayerId(gameSeed, guid);
        }
    }
}