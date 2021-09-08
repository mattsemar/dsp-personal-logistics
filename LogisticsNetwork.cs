using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using static NetworkManager.Log;

namespace NetworkManager
{
    public class StationProductInfo
    {
        public int ItemId;
        public string ItemName;
        public int ItemCount;
        public int MaxCount;
    }

    public enum StationType
    {
        PLS,
        ILS
    }

    public class PlanetInfo
    {
        public string Name;
        public int PlanetId;
    }
    
    public class StationInfo
    {
        public string Name;
        public string PlanetName;
        public int PlanetId;
        public StationType StationType;
        public List<StationProductInfo> Products = new List<StationProductInfo>();
        public List<StationProductInfo> RemoteImports = new List<StationProductInfo>();
        public List<StationProductInfo> RemoteExports = new List<StationProductInfo>();
        public List<StationProductInfo> LocalExports = new List<StationProductInfo>();
        public List<StationProductInfo> LocalImports = new List<StationProductInfo>();
        public int stationId;
        public HashSet<int> ItemTypes = new HashSet<int>();
        private static Dictionary<int, Dictionary<int, StationInfo>> pool = new Dictionary<int, Dictionary<int, StationInfo>>();

        public static StationInfo Build(StationComponent station, PlanetData planet)
        {
            StationInfo stationInfo;
            if (pool.ContainsKey(planet.id) && pool[planet.id].ContainsKey(station.id))
            {
                stationInfo = pool[planet.id][station.id];
                stationInfo.ItemTypes.Clear();
                stationInfo.LocalExports.Clear();
                stationInfo.LocalImports.Clear();
                stationInfo.RemoteExports.Clear();
                stationInfo.RemoteImports.Clear();
            }
            else
            {
                stationInfo = new StationInfo
                {
                    Name = station.name ?? station.entityId.ToString(),
                    PlanetName = planet.displayName,
                    StationType = station.isStellar ? StationType.ILS : StationType.PLS
                };
            }

            foreach (var store in station.storage)
            {
                if (store.itemId < 1)
                {
                    continue;
                }

                var itemName = LDB._items.Select(store.itemId).Name.Translate();
                var productInfo = new StationProductInfo
                {
                    ItemId = store.itemId,
                    ItemName = itemName,
                    ItemCount = store.count,
                    MaxCount = store.max
                };
                stationInfo.ItemTypes.Add(store.itemId);
                stationInfo.Products.Add(productInfo);
                if (store.remoteLogic == ELogisticStorage.Demand)
                {
                    stationInfo.RemoteImports.Add(productInfo);
                }

                if (store.remoteLogic == ELogisticStorage.Supply)
                {
                    stationInfo.RemoteExports.Add(new StationProductInfo
                    {
                        ItemCount = store.count,
                        ItemName = itemName,
                        MaxCount = store.max
                    });
                }

                if (store.localLogic == ELogisticStorage.Supply)
                {
                    stationInfo.LocalExports.Add(productInfo);
                }

                if (store.localLogic == ELogisticStorage.Demand)
                {
                    stationInfo.LocalImports.Add(productInfo);
                }
            }

            stationInfo.stationId = station.id;
            stationInfo.PlanetId = planet.id;
            return stationInfo;
        }


        public bool HasItem(int itemId)
        {
            return ItemTypes.Contains(itemId);
        }

        public int ItemCount(int itemId)
        {
            if (!ItemTypes.Contains(itemId))
            {
                return 0;
            }

            var productInfo = Products.Find(product => product.ItemId == itemId);
            if (productInfo == null)
                return 0;
            return productInfo.ItemCount;
        }

        public string ProductNameList()
        {
            return string.Join(", ", Products.Select(p => p.ItemName));
        }
    }

    public class LogisticsNetworkSummaryItem
    {
        public int ItemId;
        public string ItemName;
        public int Count;
        public int StationCount;
        public List<string> Planets = new List<string>();
    }

    public class LogisticsNetwork
    {
        public static readonly List<StationInfo> stations = new List<StationInfo>();
        public static readonly Dictionary<int, List<StationInfo>> byPlanet = new Dictionary<int, List<StationInfo>>();
        public static readonly Dictionary<int, List<StationInfo>> byItem = new Dictionary<int, List<StationInfo>>();
        public static bool IsInitted = false;
        public static bool IsRunning = false;
        public static bool IsFirstLoadComplete = false;
        private static Timer _timer;
        private static Dictionary<int, LogisticsNetworkSummaryItem> summary = new Dictionary<int, LogisticsNetworkSummaryItem>();

        public static List<LogisticsNetworkSummaryItem> GetSummary()
        {
            return new List<LogisticsNetworkSummaryItem>(summary.Values);
        }

        public static LogisticsNetworkSummaryItem GetSummaryItem(int itemId)
        {
            if (!summary.ContainsKey(itemId))
            {
                return new LogisticsNetworkSummaryItem { Count = 0, ItemId = itemId, ItemName = "" };
            }

            return summary[itemId];
        }

        public static void Start()
        {
            _timer = new Timer(10_000);
            _timer.Elapsed += CollectStationInfos;
            _timer.AutoReset = true;
            _timer.Enabled = true;
            IsInitted = true;
        }

        private static void CollectStationInfos(Object source, ElapsedEventArgs e)
        {
            if (IsRunning)
            {
                logger.LogWarning($"Collect already running");
                return;
            }

            IsRunning = true;
            logger.LogDebug($"starting station collection {DateTime.Now}");
            try
            {
                var tmpSummary = new Dictionary<int, LogisticsNetworkSummaryItem>();

                foreach (StarData star in GameMain.universeSimulator.galaxyData.stars)
                {
                    foreach (PlanetData planet in star.planets)
                    {
                        if (planet.factory != null && planet.factory.factorySystem != null && planet.factory.transport != null &&
                            planet.factory.transport.stationCursor != 0)
                        {
                            var byPlanetList = new List<StationInfo>();
                            var transport = planet.factory.transport;
                            for (int i = 1; i < transport.stationCursor; i++)
                            {
                                var station = transport.stationPool[i];
                                if (station == null || station.id != i) continue;
                                var stationInfo = StationInfo.Build(station, planet);
                                stations.Add(stationInfo);
                                byPlanetList.Add(stationInfo);
                                foreach (var productInfo in stationInfo.Products)
                                {
                                    if (!tmpSummary.ContainsKey(productInfo.ItemId))
                                    {
                                        tmpSummary.Add(productInfo.ItemId, new LogisticsNetworkSummaryItem
                                        {
                                            Count = productInfo.ItemCount,
                                            ItemId = productInfo.ItemId,
                                            ItemName = productInfo.ItemName,
                                            StationCount = 1
                                        });
                                    }
                                    else
                                    {
                                        tmpSummary[productInfo.ItemId].Count += productInfo.ItemCount;
                                        tmpSummary[productInfo.ItemId].StationCount++;
                                    }

                                    if (!tmpSummary[productInfo.ItemId].Planets.Contains(planet.displayName))
                                    {
                                        tmpSummary[productInfo.ItemId].Planets.Add(planet.displayName);
                                    }

                                    if (!byItem.ContainsKey(productInfo.ItemId))
                                    {
                                        byItem[productInfo.ItemId] = new List<StationInfo>();
                                    }
                                    byItem[productInfo.ItemId].Add(stationInfo);
                                }
                            }

                            byPlanet[planet.id] = byPlanetList;
                        }
                    }
                }

                summary = tmpSummary;
            }
            catch (Exception err)
            {
                logger.LogWarning($"Collection task failed {err}");
            }
            finally
            {
                logger.LogDebug($"done with collection {DateTime.Now} stationInfo {summary.Count}");
                IsRunning = false;
                IsFirstLoadComplete = true;
            }
        }

        public static void Stop()
        {
            if (_timer == null)
                return;
            _timer.Stop();
            _timer.Dispose();
        }
    }
}