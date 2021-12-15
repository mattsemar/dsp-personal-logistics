using System;
using System.Collections.Generic;
using System.IO;
using PersonalLogistics.Model;
using PersonalLogistics.PlayerInventory;
using PersonalLogistics.Scripts;
using PersonalLogistics.Shipping;
using PersonalLogistics.Util;

namespace PersonalLogistics.SerDe
{
    /// <summary>Try and isolate failures from affecting other parts by building a map of offsets similar to how the plugin does </summary>
    public class SerDeV3 : ISerDe
    {
        private const int VERSION = 3;

        private static readonly List<Type> parts = new List<Type>
        {
            typeof(PersonalLogisticManager),
            typeof(ShippingManager),
            typeof(DesiredInventoryState),
            typeof(RecycleWindow)
        };

        // use this as a way to avoid our section headers changing if one of these classes is renamed
        private static readonly Dictionary<Type, string> typeNames = new Dictionary<Type, string>
        {
            { typeof(PersonalLogisticManager), "PLM" },
            { typeof(ShippingManager), "SM" },
            { typeof(DesiredInventoryState), "DINV" },
            { typeof(RecycleWindow), "RW" }
        };

        private static readonly Dictionary<Type, Action<BinaryWriter>> exportActions = new Dictionary<Type, Action<BinaryWriter>>
        {
            { typeof(PersonalLogisticManager), PersonalLogisticManager.Export },
            { typeof(ShippingManager), ShippingManager.Export },
            { typeof(DesiredInventoryState), DesiredInventoryState.Export },
            { typeof(RecycleWindow), RecycleWindow.Export }
        };

        private static readonly Dictionary<Type, Action<BinaryReader>> importActions = new Dictionary<Type, Action<BinaryReader>>
        {
            { typeof(PersonalLogisticManager), PersonalLogisticManager.Import },
            { typeof(ShippingManager), ShippingManager.Import },
            { typeof(DesiredInventoryState), DesiredInventoryState.Import },
            { typeof(RecycleWindow), RecycleWindow.Import }
        };

        // used when imports fail
        private static readonly Dictionary<Type, Action> initActions = new Dictionary<Type, Action>
        {
            { typeof(PersonalLogisticManager), PersonalLogisticManager.InitOnLoad },
            { typeof(ShippingManager), ShippingManager.InitOnLoad },
            { typeof(DesiredInventoryState), DesiredInventoryState.InitOnLoad },
            { typeof(RecycleWindow), RecycleWindow.InitOnLoad }
        };


        public void Import(BinaryReader r)
        {
            var tableOfContents = TableOfContents.Import(r);
            try
            {
                foreach (var contentsItem in tableOfContents.GetItems())
                {
                    // create a stream for each section and process them one by one
                    try
                    {
                        var type = contentsItem.GetType();
                        Log.Debug($"starting to read {type} at {contentsItem.startIndexAbsolute}, {contentsItem.length}");
                        var memoryStream = new MemoryStream(r.ReadBytes(contentsItem.length));
                        var sectionReader = new BinaryReader(memoryStream);
                        try
                        {
                            var section = sectionReader.ReadString();
                            if (section != typeNames[type])
                            {
                                throw new InvalidDataException($"section name was: {section}, expected {typeNames[type]}");
                            }
                            importActions[type](sectionReader);
                            Log.Debug($"successful read in section: {type}");
                        }
                        catch (Exception e)
                        {
                            Log.Warn($"Import failure for section: {contentsItem.sectionName}. Falling back to init");
                            initActions[type]();
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
            w.Write(VERSION);
            var tableOfContents = new TableOfContents();

            
            var partList = new List<byte[]>();
            try
            {
                foreach (var partType in parts)
                {
                    var sectionData = Write(partType);
                    partList.Add(sectionData);
                    tableOfContents.AddItem(sectionData, typeNames[partType]);
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

        private byte[] Write(Type type)
        {
            var memoryStream = new MemoryStream();
            try
            {
                var writer = new BinaryWriter(memoryStream);
                var typeName = typeNames[type];
                writer.Write(typeName);
                try
                {
                    exportActions[type](writer);
                    Log.Debug($"stored {typeName}");
                }
                catch (Exception e)
                {
                    Log.Warn($"Falling back to init for {typeName}.");
                    initActions[type]();
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

            public Type GetType()
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
            private readonly List<TableOfContentsItem> _items = new List<TableOfContentsItem>();

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