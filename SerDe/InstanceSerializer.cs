using System.IO;
using PersonalLogistics.ModPlayer;

namespace PersonalLogistics.SerDe
{
    public abstract class InstanceSerializer : IPlayerContext
    {
        public abstract void ExportData(BinaryWriter w);

        public abstract void ImportData(BinaryReader reader);
        public abstract PlogPlayerId GetPlayerId();

        public PlogPlayer GetPlayer()
        {
            return PlogPlayerRegistry.Get(GetPlayerId());
        }

        public abstract string GetExportSectionId();

        public abstract void InitOnLoad();
        public abstract string SummarizeState();
    }
}