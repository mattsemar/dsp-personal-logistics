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
            var memoryStream = new MemoryStream();
            var w = new BinaryWriter(memoryStream);
            w.Write(playerId.gameSeed);
            w.Write(playerId.assignedId.ToByteArray());
            w.Write(state.Length);
            w.Write(state);
            data = memoryStream.ToArray();
        }

        public static (PlogPlayerId playerId, byte[] state) DecodePacket(ClientState clientState)
        {
            var memoryStream = new MemoryStream(clientState.data);
            var r = new BinaryReader(memoryStream);
            var gameSeed = r.ReadInt32();
            var guid = new Guid(r.ReadBytes(16));
            PlogPlayerId playerId = new PlogPlayerId(gameSeed, guid);
            var length = r.ReadInt32();
            var stateBytes = r.ReadBytes(length);

            return (playerId, stateBytes);


            // using IReaderProvider p = NebulaModAPI.GetBinaryReader(clientState.data);
            // // var gameSeed = p.BinaryReader.ReadInt32();
            // // var guid = new Guid(p.BinaryReader.ReadBytes(16));
            // // PlogPlayerId playerId = new PlogPlayerId(gameSeed, guid);
            // // var length = p.BinaryReader.ReadInt32();
            // // var stateBytes = p.BinaryReader.ReadBytes(length);
            //
            // return (PlogPlayerId.FromString(clientState.playerId), clientState.data);
        }
    }
}