using System;
using System.IO;
using PersonalLogistics.ModPlayer;

namespace PersonalLogistics.Nebula.Packets
{
    public class ClientState
    {
        public byte[] data { get; set; }

        public ClientState()
        {
        }

        public ClientState(PlogPlayerId playerId, byte[] state)
        {
            using var memoryStream = new MemoryStream();
            using var w = new BinaryWriter(memoryStream);
            w.Write(playerId.gameSeed);
            w.Write(playerId.assignedId.ToByteArray());
            w.Write(state.Length);
            w.Write(state);
            data = memoryStream.ToArray();
        }

        public static (PlogPlayerId playerId, byte[] state) DecodePacket(ClientState clientState)
        {
            using var memoryStream = new MemoryStream(clientState.data);
            using var r = new BinaryReader(memoryStream);
            var gameSeed = r.ReadInt32();
            var guid = new Guid(r.ReadBytes(16));
            PlogPlayerId playerId = new PlogPlayerId(gameSeed, guid);
            var length = r.ReadInt32();
            var stateBytes = r.ReadBytes(length);

            return (playerId, stateBytes);
        }
    }
}