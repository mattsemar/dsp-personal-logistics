using System.Collections.Generic;
using System.IO;
using System.Linq;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.Util;

namespace PersonalLogistics.SerDe
{
    public static class SerDeManager
    {
        private static readonly Dictionary<int, ISerDe> versions = new()
        {
            { 1, new SerDeV1() },
            { 2, new SerDeV2() },
            { 3, new SerDeV3() },
            { 4, new SerDeV4() }
        };

        public static readonly int Latest = versions.Keys.Max();

        public static void Import(BinaryReader r)
        {
            var version = r.ReadInt32();
            Log.Debug($"(SerDe) importing version {version}");
            versions[version].Import(r);
        }

        public static void Export(BinaryWriter w)
        {
            Export(w, Latest);
        }

        public static void Export(BinaryWriter w, int versionToUse)
        {
            Log.Debug($"(SerDe) exporting version {versionToUse}");

            versions[versionToUse].Export(w);
        }

        public static byte[] ExportRemoteUserData(PlogRemotePlayer player)
        {
            var serDeRemoteUserState = new SerDeRemoteUserState(player);
            using var memoryStream = new MemoryStream();
            using var writer = new BinaryWriter(memoryStream);
            serDeRemoteUserState.Export(writer);
            return memoryStream.ToArray();
        }

        public static PlogPlayer ImportRemoteUser(PlogPlayerId playerId, byte[] playerData)
        {
            var plogRemotePlayer = new PlogRemotePlayer(playerId);
            var serDeRemoteUserState = new SerDeRemoteUserState(plogRemotePlayer);
            using var memoryStream = new MemoryStream(playerData);
            using var reader = new BinaryReader(memoryStream);
            serDeRemoteUserState.Import(reader);
            return plogRemotePlayer;
        }
    }
}