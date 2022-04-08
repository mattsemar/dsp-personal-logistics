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
        // private static readonly ConcurrentDictionary<int, ConcurrentDictionary<int, StationInfo>> pool = new();
        private static readonly ConcurrentDictionary<int, StationInfo> pool = new();
        public int StationId;
        public int StationGid;
        public Vector3 LocalPosition;
        public PlanetInfo PlanetInfo;
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
        private StationInfo(int productCount)
        {
            _products = new StationProductInfo[productCount];
            _localExports = new StationProductInfo[productCount];
            _remoteExports = new StationProductInfo[productCount];
            _requestedItems = new StationProductInfo[productCount];
            _suppliedItems = new StationProductInfo[productCount];
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

            if (!pool.TryGetValue(station.gid, out var stationInfo))
            {
                stationInfo = new StationInfo(station.storage.Length)
                {
                    PlanetName = nullablePlanet == null ? "Planet Name Unknown" : nullablePlanet.displayName,
                    StationType = station.isStellar ? StationType.ILS : StationType.PLS,
                    StationId = station.id,
                    StationGid = station.gid,
                    IsOrbitalCollector = station.isCollector
                };
                pool[station.id] = stationInfo;
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

            stationInfo.PlanetInfo = new PlanetInfo
            {
                lastLocation = nullablePlanet?.uPosition ?? VectorLF3.zero,
                Name = nullablePlanet?.displayName,
                PlanetId = station.id
            };
            stationInfo.LocalPosition = station.shipDockPos;

            for (int i = 0; i < station.storage.Length; i++)
            {
                var store = station.storage[i];
                if (store.itemId < 1)
                {
                    if (stationInfo._indexToItem.ContainsKey(i))
                    {
                        var oldItemId = stationInfo._indexToItem[i];
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
                stationInfo._products[i] = productInfo;

                if (store.totalOrdered < 0)
                {
                    // these are already spoken for so take them from total
                    productInfo.ItemCount = Math.Max(0, productInfo.ItemCount + store.totalOrdered);
                }

                var isSupply = false;
                bool isDemand = store.remoteLogic == ELogisticStorage.Demand;

                if (store.remoteLogic == ELogisticStorage.Supply)
                {
                    isSupply = true;
                    stationInfo._remoteExports[i] = productInfo;
                }
                else
                {
                    stationInfo._remoteExports[i] = null;
                }

                if (store.localLogic == ELogisticStorage.Supply)
                {
                    isSupply = true;
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
            /*
             productLen (int)
            StationGid;
            StationId;
            LocalPosition;
            PlanetInfo;
            PlanetName;
            IsOrbitalCollector;
            StationType;
            double WarpEnableDistance;
*/
            w.Write(_products.Length);
            w.Write(StationGid);
            w.Write(StationId);
            w.Write(LocalPosition.x);
            w.Write(LocalPosition.y);
            w.Write(LocalPosition.z);
            PlanetInfo.Export(w);
            w.Write(PlanetName);
            w.Write(IsOrbitalCollector);
            w.Write((int)StationType);
            w.Write(WarpEnableDistance);
            /*
             *  private readonly StationProductInfo[] _localExports = new StationProductInfo[15];
        private readonly StationProductInfo[] _remoteExports = new StationProductInfo[15];

        private readonly StationProductInfo[] _requestedItems = new StationProductInfo[15];
        private readonly StationProductInfo[] _suppliedItems = new StationProductInfo[15];
        private readonly ConcurrentDictionary<int, int> _itemToIndex = new();
        private readonly ConcurrentDictionary<int, int> _indexToItem = new();
             
            */
            WriteStationProductInfo(_products, w);
            WriteStationProductInfo(_localExports, w);
            WriteStationProductInfo(_remoteExports, w);
            WriteStationProductInfo(_requestedItems, w);
            WriteStationProductInfo(_suppliedItems, w);
        }

        private static void WriteStationProductInfo(StationProductInfo[] stationProductInfos, BinaryWriter w)
        {
            w.Write(stationProductInfos.Length);
            foreach (var product in stationProductInfos)
            {
                if (product == null)
                {
                    new StationProductInfo().Export(w);
                } else
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
            var result = new StationInfo(prodCount)
            {
                StationGid = r.ReadInt32(),
                StationId = r.ReadInt32(),
                LocalPosition = new Vector3(r.ReadSingle(), r.ReadSingle(),r.ReadSingle()),
                PlanetInfo = PlanetInfo.Import(r),
                IsOrbitalCollector = r.ReadBoolean(),
                StationType = (StationType)r.ReadInt32(),
                WarpEnableDistance = r.ReadDouble(),
            };
            {
                int count = r.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    var stationProductInfo = StationProductInfo.Import(r);
                    if (stationProductInfo.ItemId == 0)
                        continue;
                    result._indexToItem[i] = stationProductInfo.ItemId;
                    result._indexToItem[stationProductInfo.ItemId] = i;
                    result._products[i] = stationProductInfo;
                }
            }
            ReadStationProductInfoArray(r, result._localExports);
            ReadStationProductInfoArray(r, result._remoteExports);
            ReadStationProductInfoArray(r, result._requestedItems);
            ReadStationProductInfoArray(r, result._suppliedItems);
            pool[result.StationGid] = result;
            return result;
        }

        private static void ReadStationProductInfoArray(BinaryReader r, StationProductInfo[] stationProductInfos)
        {
            int count = r.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var stationProductInfo = StationProductInfo.Import(r);
                if (stationProductInfo.ItemId == 0)
                    continue;
                stationProductInfos[i] = stationProductInfo;
            }
        }
    }
}