using System;
using System.Collections.Generic;
using System.IO;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.Util;

namespace PersonalLogistics.SerDe
{
    /// <summary>Try and isolate failures from affecting other parts by building a map of offsets similar to how the plugin does </summary>
    public abstract class TocBasedSerDe : ISerDe
    {
        // the parts that will be used for the table of contents
        protected abstract List<Type> GetParts();

        // use this as a way to avoid our section headers changing if one of these classes is renamed
        //             { typeof(PersonalLogisticManager), "PLM" },
        protected abstract Dictionary<Type, string> GetTypeNames();

        // what methods to call for each type on export
        //             { typeof(PersonalLogisticManager), PersonalLogisticManager.Export },
        protected abstract Dictionary<Type, Action<BinaryWriter>> GetExportActions();

        // what methods to call for imports
        // { typeof(ShippingManager), ShippingManager.Import },
        protected abstract Dictionary<Type, Action<BinaryReader>> GetImportActions();

        // what to do when an import of a type fails
        //  { typeof(PersonalLogisticManager), PersonalLogisticManager.InitOnLoad },
        protected abstract Dictionary<Type, Action> GetInitActions();

        public void Import(BinaryReader r)
        {
            PlogPlayerRegistry.ClearLocal();
            PlogPlayerRegistry.RegisterLocal(PlogPlayerId.ComputeLocalPlayerId());
            var tableOfContents = TableOfContents.Import(r);
            try
            {
                foreach (var contentsItem in tableOfContents.GetItems())
                {
                    // create a stream for each section and process them one by one
                    try
                    {
                        var type = contentsItem.GetType(GetTypeNames());
                        Log.Debug($"starting to read {type} at {contentsItem.startIndexAbsolute}, {contentsItem.length}");
                        var memoryStream = new MemoryStream(r.ReadBytes(contentsItem.length));
                        var sectionReader = new BinaryReader(memoryStream);
                        try
                        {
                            var section = sectionReader.ReadString();
                            if (section != GetTypeNames()[type])
                            {
                                throw new InvalidDataException($"section name was: {section}, expected {GetTypeNames()[type]}");
                            }
                            GetImportActions()[type](sectionReader);
                            Log.Debug($"successful read in section: {type}");
                        }
                        catch (Exception e)
                        {
                            Log.Warn($"Import failure for section: {contentsItem.sectionName}. Falling back to init");
                            GetInitActions()[type]();
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Warn($"failed to import contents item: {contentsItem.sectionName}");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn($"failed to read data section");
            }
        }

        /// <summary>
        /// version, table of contents, datalen, data
        /// </summary>
        /// <param name="w"></param>
        public void Export(BinaryWriter w)
        {
            w.Write(getVersion());
            var tableOfContents = new TableOfContents();

            
            var partList = new List<byte[]>();
            try
            {
                foreach (var partType in GetParts())
                {
                    var sectionData = WriteType(partType);
                    partList.Add(sectionData);
                    tableOfContents.AddItem(sectionData, GetTypeNames()[partType]);
                }
            }
            catch (Exception e)
            {
                Log.Warn($"got exception while writing parts to mem: {e.Message}");
            }

            // write TOC next
            tableOfContents.Export(w);

            foreach (var part in partList)
            {
                w.Write(part);
            }
            w.Flush();
        }

        public void Clear()
        {
            
        }

        protected abstract int getVersion();

        private byte[] WriteType(Type type)
        {
            var memoryStream = new MemoryStream();
            try
            {
                var writer = new BinaryWriter(memoryStream);
                var typeName = GetTypeNames()[type];
                writer.Write(typeName);
                try
                {
                    GetExportActions()[type](writer);
                    Log.Debug($"stored {typeName}");
                }
                catch (Exception e)
                {
                    Log.Warn($"Falling back to init for {typeName}.");
                    GetInitActions()[type]();
                }
            }
            catch (Exception e)
            {
                Log.Warn($"problem while writing {type}. {e.Message}");
            }

            return memoryStream.ToArray();
        }

        internal class TableOfContentsItem
        {
            public string sectionName;
            public int startIndexAbsolute;
            public int length;

            public Type GetType(Dictionary<Type, string> typeNames)
            {
                foreach (var sect in typeNames)
                {
                    if (sect.Value == sectionName)
                    {
                        return sect.Key;
                    }
                }

                throw new InvalidDataException($"no type for section: {sectionName}");
            }
        }

        internal class TableOfContents
        {
            private readonly List<TableOfContentsItem> _items = new();

            public int DataSectionSize;

            public void AddItem(byte[] data, string sectionName)
            {
                _items.Add(new TableOfContentsItem
                {
                    sectionName = sectionName,
                    startIndexAbsolute = DataSectionSize,
                    length = data.Length,
                });
                DataSectionSize += data.Length + 1;
            }

            // toc is: count, [(name, actualPos, length)]
            public void Export(BinaryWriter w)
            {
                w.Write(_items.Count);
                Log.Debug($"wrote {_items.Count} (begin of toc)");
                foreach (var item in _items)
                {
                    w.Write(item.sectionName);
                    w.Write(item.startIndexAbsolute);
                    w.Write(item.length);
                    Log.Debug($"wrote {item.sectionName}, {item.startIndexAbsolute}, {item.length} current toc item");
                }
                Log.Debug($"exported {_items.Count} sections");
                w.Flush();
            }

            public static TableOfContents Import(BinaryReader r)
            {
                var tableOfContents = new TableOfContents();
                var count = r.ReadInt32();
                Log.Debug($"importing {count} toc entries");
                for (int i = 0; i < count; i++)
                {
                    tableOfContents._items.Add(new TableOfContentsItem
                    {
                        sectionName = r.ReadString(),
                        startIndexAbsolute = r.ReadInt32(),
                        length = r.ReadInt32(),
                    });
                    // Log.Debug($"imported toc item: {tableOfContents._items[tableOfContents._items.Count - 1].sectionName}");
                }
                Log.Debug($"imported {tableOfContents._items.Count} items, last toc item: {tableOfContents._items[tableOfContents._items.Count - 1].sectionName}");

                return tableOfContents;
            }

            public List<TableOfContentsItem> GetItems()
            {
                return new List<TableOfContentsItem>(_items);
            }
        }
    }
}