using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using PersonalLogistics.Util;
using UnityEngine;

namespace PersonalLogistics.Logistics
{
    public class StationInfo
    {
        private static readonly ConcurrentDictionary<int, StationInfo> pool = new();
        public int StationId;
        public int StationGid;
        public Vector3 LocalPosition;
        public readonly PlanetInfo PlanetInfo;
        public string PlanetName;
        public bool IsOrbitalCollector;
        public StationType StationType;
        public double WarpEnableDistance;

        private readonly StationProductInfo[] _products;
        private readonly StationProductInfo[] _localExports;
        private readonly StationProductInfo[] _remoteExports;

        private readonly StationProductInfo[] _requestedItems;
        private readonly StationProductInfo[] _suppliedItems;

        private readonly ConcurrentDictionary<int, int> _itemToIndex = new();
        private readonly ConcurrentDictionary<int, int> _indexToItem = new();

        private StationInfo(int productCount, PlanetInfo planetInfo)
        {
            _products = new StationProductInfo[productCount];
            _localExports = new StationProductInfo[productCount];
            _remoteExports = new StationProductInfo[productCount];
            _requestedItems = new StationProductInfo[productCount];
            _suppliedItems = new StationProductInfo[productCount];
            if (planetInfo == null)
                throw new Exception("Constructor for StationInfo received null planetInfo");
            PlanetInfo = planetInfo;
        }


        public List<StationProductInfo> Products
        {
            get
            {
                var result = new List<StationProductInfo>();
                for (int i = 0; i < _products.Length; i++)
                {
                    var stationProductInfo = _products[i];
                    if (stationProductInfo == null)
                        continue;
                    result.Add(stationProductInfo);
                }

                return result;
            }
        }

        public static (StationInfo stationInfo, bool changed) Build(StationComponent station, PlanetData nullablePlanet)
        {
            var changedResult = false;

            var stationGid = station.planetId * 10000 + station.id;
            if (!pool.TryGetValue(stationGid, out var stationInfo))
            {
                stationInfo = new StationInfo(Math.Max(station.storage.Length, 15), new PlanetInfo
                {
                    lastLocation = nullablePlanet?.uPosition ?? VectorLF3.zero,
                    Name = nullablePlanet?.displayName,
                    PlanetId = station.planetId
                })
                {
                    PlanetName = nullablePlanet == null ? "Planet Name Unknown" : nullablePlanet.displayName,
                    StationType = station.isStellar ? StationType.ILS : StationType.PLS,
                    StationId = station.id,
                    StationGid = stationGid,
                    IsOrbitalCollector = station.isCollector && station.isStellar,
                };
                pool[stationGid] = stationInfo;
                changedResult = true;
            }

            if (Math.Abs(stationInfo.WarpEnableDistance - station.warpEnableDist) > 0.0001)
            {
                stationInfo.WarpEnableDistance = station.warpEnableDist;
                changedResult = true;
            }

            if (nullablePlanet != null && stationInfo.PlanetInfo?.lastLocation != nullablePlanet.uPosition)
            {
                changedResult = true;
            }

            stationInfo.PlanetInfo.lastLocation = nullablePlanet?.uPosition ?? VectorLF3.zero;
            stationInfo.PlanetInfo.Name = nullablePlanet?.displayName;
            
            stationInfo.LocalPosition = station.shipDockPos;
            if (station.storage.Length > stationInfo._products.Length)
            {
                Log.Warn($"Station storage len {station.storage.Length} vs info {stationInfo._products.Length}");
            }
            for (int i = 0; i < station.storage.Length; i++)
            {
                var store = station.storage[i];
                if (store.itemId < 1)
                {
                    if (stationInfo._indexToItem.TryGetValue(i, out var oldItemId))
                    {
                        stationInfo._indexToItem.TryRemove(i, out _);
                        stationInfo._itemToIndex.TryRemove(oldItemId, out _);
                        changedResult = true;
                    }

                    continue;
                }

                stationInfo._indexToItem[i] = store.itemId;
                stationInfo._itemToIndex[store.itemId] = i;

                if (stationInfo._products[i]?.ItemCount != store.count)
                    changedResult = true;
                var productInfo = new StationProductInfo
                {
                    ItemId = store.itemId,
                    ItemCount = store.count,
                    ProliferatorPoints = store.inc
                };
                if (stationInfo._products[i]?.ItemId != productInfo.ItemId)
                {
                    changedResult = true;
                }

                stationInfo._products[i] = productInfo;

                if (store.totalOrdered < 0)
                {
                    // these are already spoken for so take them from total
                    productInfo.ItemCount = Math.Max(0, productInfo.ItemCount + store.totalOrdered);
                }

                var isSupply = store.remoteLogic == ELogisticStorage.Supply || store.localLogic == ELogisticStorage.Supply;
                bool isDemand = store.remoteLogic == ELogisticStorage.Demand;

                if (store.remoteLogic == ELogisticStorage.Supply)
                {
                    stationInfo._remoteExports[i] = productInfo;
                }
                else
                {
                    stationInfo._remoteExports[i] = null;
                }

                if (store.localLogic == ELogisticStorage.Supply)
                {
                    stationInfo._localExports[i] = productInfo;
                }
                else
                {
                    stationInfo._localExports[i] = null;
                }

                if (store.localLogic == ELogisticStorage.Demand)
                {
                    isDemand = true;
                }

                stationInfo._suppliedItems[i] = null;
                stationInfo._requestedItems[i] = null;
                if (isSupply)
                {
                    if (productInfo.ItemCount > 0)
                    {
                        stationInfo._suppliedItems[i] = productInfo;
                    }
                }

                if (isDemand)
                {
                    stationInfo._requestedItems[i] = productInfo;
                }
            }


            return (stationInfo, changedResult);
        }


        public bool HasItem(int itemId) => _itemToIndex.ContainsKey(itemId);

        public static StationInfo ByStationGid(int stationGid)
        {
            if (!pool.TryGetValue(stationGid, out var stationInfo))
            {
                Log.Warn($"Failed to load station {stationGid}  pool {pool.Count}");
            }
            else
            {
                return stationInfo;
            }


            Log.Warn($"Trying to get station from gid {stationGid} {pool.Count}");
            var stationComp = StationStorageManager.GetStationComp(stationGid);
            if (stationComp != null)
            {
                var stationAndPlanet = StationStorageManager.GetStationComp(stationComp.planetId, stationComp.id);

                if (stationAndPlanet.station == null || stationAndPlanet.planet == null)
                {
                    Log.Warn($"2nd attempt failed get station {stationComp.planetId} {stationComp.id}");
                    return null;
                }

                return Build(stationAndPlanet.station, stationAndPlanet.planet).stationInfo;
            }

            return null;
        }


        // use this for testing
        public static StationInfo GetAnyStationWithItem(int itemId)
        {
            lock (pool)
            {
                foreach (var stationGid in pool.Keys)
                {
                    var stationInfo = pool[stationGid];
                    {
                        if (stationInfo.HasItem(itemId))
                        {
                            return stationInfo;
                        }
                    }
                }
            }

            return null;
        }

        public bool HasAnyExport(int itemId)
        {
            if (_itemToIndex.TryGetValue(itemId, out int index))
            {
                return (_localExports[index] != null && _localExports[index].ItemCount > 0)
                       || (_remoteExports[index] != null && _remoteExports[index].ItemCount > 0);
            }

            return false;
        }

        public bool HasLocalExport(int itemId)
        {
            if (_itemToIndex.TryGetValue(itemId, out int index))
            {
                return _localExports[index] != null && _localExports[index].ItemCount > 0;
            }

            return false;
        }

        public bool HasRemoteExport(int itemId)
        {
            if (_itemToIndex.TryGetValue(itemId, out int index))
            {
                return _remoteExports[index] != null && _remoteExports[index].ItemCount > 0;
            }

            return false;
        }

        public StationProductInfo GetProductInfo(int itemId)
        {
            if (_itemToIndex.TryGetValue(itemId, out int index))
            {
                return _products[index];
            }

            return null;
        }

        public bool IsSupplied(int itemId)
        {
            if (_itemToIndex.TryGetValue(itemId, out int index))
            {
                return _suppliedItems[index] != null && _suppliedItems[index].ItemCount > 0;
            }

            return false;
        }

        public bool IsRequested(int itemId)
        {
            if (_itemToIndex.TryGetValue(itemId, out int index))
            {
                return _requestedItems[index] != null;
            }

            return false;
        }

        public void Export(BinaryWriter w)
        {
            w.Write(_products.Length);
            PlanetInfo.Export(w);
            w.Write(StationGid);
            w.Write(StationId);
            w.Write(LocalPosition.x);
            w.Write(LocalPosition.y);
            w.Write(LocalPosition.z);
            w.Write(PlanetName);
            w.Write(IsOrbitalCollector);
            w.Write((int)StationType);
            w.Write(WarpEnableDistance);
            WriteStationProductInfo(_products, w);
            WriteStationProductInfo(_localExports, w);
            WriteStationProductInfo(_remoteExports, w);
            WriteStationProductInfo(_requestedItems, w);
            WriteStationProductInfo(_suppliedItems, w);
        }

        private static void WriteStationProductInfo(StationProductInfo[] stationProductInfos, BinaryWriter w)
        {
            foreach (var product in stationProductInfos)
            {
                if (product == null)
                {
                    new StationProductInfo().Export(w);
                }
                else
                    product.Export(w);
            }
        }

        public static StationInfo Import(BinaryReader r)
        {
            int prodCount = r.ReadInt32();
            if (prodCount > 15)
            {
                throw new Exception("Invalid stationInfo import. First value should be product count, got " + prodCount);
            }

            var planetInfo = PlanetInfo.Import(r);
            var stationGid = r.ReadInt32();

            if (!pool.TryGetValue(stationGid, out var stationInfo))
            {
                stationInfo = new StationInfo(prodCount, planetInfo)
                {
                    StationGid = stationGid,
                    StationId = r.ReadInt32(),
                    LocalPosition = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()),
                    PlanetName = r.ReadString(),
                    IsOrbitalCollector = r.ReadBoolean(),
                    StationType = (StationType)r.ReadInt32(),
                    WarpEnableDistance = r.ReadDouble(),
                };
                pool[stationGid] = stationInfo;
            }
            else
            {
                stationInfo.StationId = r.ReadInt32();
                stationInfo.LocalPosition = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                stationInfo.PlanetName = r.ReadString();
                stationInfo.IsOrbitalCollector = r.ReadBoolean();
                stationInfo.StationType = (StationType)r.ReadInt32();
                stationInfo.WarpEnableDistance = r.ReadDouble();
                stationInfo.PlanetInfo.lastLocation = planetInfo.lastLocation;
            }
        
            ReadStationProductInfoArray(r, stationInfo._products, stationInfo);
            ReadStationProductInfoArray(r, stationInfo._localExports, stationInfo);
            ReadStationProductInfoArray(r, stationInfo._remoteExports, stationInfo);
            ReadStationProductInfoArray(r, stationInfo._requestedItems, stationInfo);
            ReadStationProductInfoArray(r, stationInfo._suppliedItems, stationInfo);
            return stationInfo;
        }

        private static void ReadStationProductInfoArray(BinaryReader r, StationProductInfo[] stationProductInfos, StationInfo stationInfo)
        {
            for (int i = 0; i < stationProductInfos.Length; i++)
            {
                var stationProductInfo = StationProductInfo.Import(r);
                if (stationProductInfo.ItemId == 0)
                    continue;
                stationProductInfos[i] = stationProductInfo;
                stationInfo._indexToItem.TryAdd(i, stationProductInfo.ItemId);
                stationInfo._itemToIndex.TryAdd(stationProductInfo.ItemId, i);
            }
        }

        public static void Clear()
        {
            pool.Clear();
        }
    }
}