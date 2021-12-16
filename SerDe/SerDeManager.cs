using System.Collections.Generic;
using System.IO;
using System.Linq;
using PersonalLogistics.Util;

namespace PersonalLogistics.SerDe
{
    public class SerDeManager
    {
        private static Dictionary<int, ISerDe> versions = new Dictionary<int, ISerDe>
        {
            { 1, new SerDeV1() },
            { 2, new SerDeV2() },
            { 3, new SerDeV3() },
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
    }
}