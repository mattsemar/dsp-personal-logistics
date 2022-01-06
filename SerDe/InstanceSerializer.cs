using System;
using System.IO;
using PersonalLogistics.ModPlayer;

namespace PersonalLogistics.SerDe
{
    public abstract class InstanceSerializer<T> : IPlayerContext
    {
        public static T ImportData(BinaryReader r)
        {
            T inst = Init();
            (inst as ISerDe)?.Import(r);
            return inst;
        }

        private static T Init()
        {
            return Activator.CreateInstance<T>();
        }

        public abstract void ExportData(BinaryWriter w);
        // public abstract void Import(BinaryReader reader);
        //
        // public abstract void Export(BinaryWriter writer);
        public abstract PlogPlayerId GetPlayerId();

        public PlogPlayer GetPlayer()
        {
            return PlogPlayerRegistry.Get(GetPlayerId());
        }
    }
}