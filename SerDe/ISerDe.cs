using System.IO;

namespace PersonalLogistics.SerDe
{
    public interface ISerDe
    {
        void Import(BinaryReader reader);
        void Export(BinaryWriter writer);
    }
}