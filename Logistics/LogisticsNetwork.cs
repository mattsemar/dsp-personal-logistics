using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;
using PersonalLogistics.Model;
using PersonalLogistics.PlayerInventory;
using PersonalLogistics.Shipping;
using PersonalLogistics.StationStorage;
using PersonalLogistics.Util;
using static PersonalLogistics.Log;
using Object = System.Object;

namespace PersonalLogistics.Logistics
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
        public VectorLF3 lastLocation;
    }

    public class StationInfo
    {
        public string PlanetName;
        public PlanetInfo PlanetInfo;
        public StationType StationType;
        public List<StationProductInfo> Products = new List<StationProductInfo>();
        public readonly List<StationProductInfo> RemoteImports = new List<StationProductInfo>();
        public readonly List<StationProductInfo> RemoteExports = new List<StationProductInfo>();
        public readonly List<StationProductInfo> LocalExports = new List<StationProductInfo>();
        public readonly List<StationProductInfo> LocalImports = new List<StationProductInfo>();
        public int stationId;
        public HashSet<int> ItemTypes = new HashSet<int>();

        private static Dictionary<int, Dictionary<int, StationInfo>> pool =
            new Dictionary<int, Dictionary<int, StationInfo>>();

        public HashSet<int> RequestedItems = new HashSet<int>();
        public HashSet<int> SuppliedItems = new HashSet<int>();

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

                var itemName = ItemUtil.GetItemName(store.itemId);
                var productInfo = new StationProductInfo
                {
                    ItemId = store.itemId,
                    ItemName = itemName,
                    ItemCount = store.count,
                    MaxCount = store.max
                };
                stationInfo.ItemTypes.Add(store.itemId);
                stationInfo.Products.Add(productInfo);
                bool isSupply = false;
                if (store.remoteLogic == ELogisticStorage.Demand)
                {
                    stationInfo.RemoteImports.Add(productInfo);
                }

                if (store.remoteLogic == ELogisticStorage.Supply)
                {
                    isSupply = true;
                    stationInfo.RemoteExports.Add(new StationProductInfo
                    {
                        ItemCount = store.count,
                        ItemName = itemName,
                        MaxCount = store.max
                    });
                }

                if (store.localLogic == ELogisticStorage.Supply)
                {
                    if (stationInfo.StationType == StationType.PLS)
                        isSupply = true;
                    stationInfo.LocalExports.Add(productInfo);
                }

                if (store.localLogic == ELogisticStorage.Demand)
                {
                    stationInfo.LocalImports.Add(productInfo);
                }

                if (isSupply)
                {
                    stationInfo.SuppliedItems.Add(productInfo.ItemId);
                }
                else
                {
                    stationInfo.RequestedItems.Add(productInfo.ItemId);
                }
            }

            stationInfo.stationId = station.id;
            stationInfo.PlanetInfo = new PlanetInfo
                { lastLocation = planet.uPosition, Name = planet.displayName, PlanetId = planet.id };
            return stationInfo;
        }


        public bool HasItem(int itemId)
        {
            return ItemTypes.Contains(itemId);
        }
    }

    public class ByItemSummary
    {
        public int Suppliers;
        public int Requesters;
        public int TotalStorage;
        public int AvailableItems;
        public int SuppliedItems;
        public HashSet<int> PlanetIds = new HashSet<int>();
    }

    public static class LogisticsNetwork
    {
        public static readonly List<StationInfo> stations = new List<StationInfo>();
        public static readonly Dictionary<int, List<StationInfo>> byPlanet = new Dictionary<int, List<StationInfo>>();
        public static readonly Dictionary<int, int> byItem = new Dictionary<int, int>();
        public static readonly Dictionary<int, ByItemSummary> byItemSummary = new Dictionary<int, ByItemSummary>();
        public static bool IsInitted = false;
        public static bool IsRunning = false;
        public static bool IsFirstLoadComplete;
        private static Timer _timer;

        public static void Start()
        {
            _timer = new Timer(5_000);
            _timer.Elapsed += DoPeriodicTask;
            _timer.AutoReset = true;
            _timer.Enabled = true;
            IsInitted = true;
        }

        private static void DoPeriodicTask(Object source, ElapsedEventArgs e)
        {
            try
            {
                if (PluginConfig.inventoryManagementPaused.Value)
                    return;
                CollectStationInfos(source, e);
            }
            catch (Exception exc)
            {
                Warn($"exception in periodic task {exc.Message}\n{exc.StackTrace}");
            }
        }

        private static void CollectStationInfos(Object source, ElapsedEventArgs e)
        {
            if (IsRunning)
            {
                logger.LogWarning($"Collect already running");
                return;
            }

            IsRunning = true;
            stations.Clear();
            byItem.Clear();
            byItemSummary.Clear();
            try
            {
                foreach (StarData star in GameMain.universeSimulator.galaxyData.stars)
                {
                    foreach (PlanetData planet in star.planets)
                    {
                        if (planet.factory != null && planet.factory.factorySystem != null &&
                            planet.factory.transport != null &&
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
                                    if (!byItem.ContainsKey(productInfo.ItemId))
                                    {
                                        byItem[productInfo.ItemId] = productInfo.ItemCount;
                                    }
                                    else
                                    {
                                        byItem[productInfo.ItemId] += productInfo.ItemCount;
                                    }


                                    if (byItemSummary.TryGetValue(productInfo.ItemId, out ByItemSummary summary))
                                    {
                                        summary.AvailableItems += productInfo.ItemCount;
                                        summary.PlanetIds.Add(stationInfo.PlanetInfo.PlanetId);

                                        summary.TotalStorage += productInfo.MaxCount;
                                        if (stationInfo.SuppliedItems.Contains(productInfo.ItemId))
                                        {
                                            summary.Suppliers++;
                                            summary.SuppliedItems += productInfo.ItemCount;
                                        }
                                        else
                                        {
                                            summary.Requesters++;
                                        }
                                    }
                                    else
                                    {
                                        byItemSummary[productInfo.ItemId] = new ByItemSummary
                                        {
                                            AvailableItems = productInfo.ItemCount,
                                            Requesters = stationInfo.RequestedItems.Contains(productInfo.ItemId) ? 1 : 0,
                                            Suppliers = stationInfo.SuppliedItems.Contains(productInfo.ItemId) ? 1 : 0,
                                            TotalStorage = stationInfo.StationType == StationType.ILS ? 10000 : 5000, // TODO fix this to get real  value
                                            SuppliedItems = stationInfo.SuppliedItems.Contains(productInfo.ItemId) ? productInfo.ItemCount : 0,
                                        };
                                        byItemSummary[productInfo.ItemId].PlanetIds.Add(stationInfo.PlanetInfo.PlanetId);
                                    }
                                }
                            }

                            byPlanet[planet.id] = byPlanetList;
                        }
                    }
                }

                IsInitted = true;
            }
            catch (Exception err)
            {
                logger.LogWarning($"Collection task failed {err}");
            }
            finally
            {
                IsRunning = false;
                IsFirstLoadComplete = true;
            }
        }

        public static void Stop()
        {
            IsInitted = false;
            IsFirstLoadComplete = false;
            if (_timer == null)
                return;
            _timer.Stop();
            _timer.Dispose();
        }

        public static bool HasItem(int itemId)
        {
            return byItem.ContainsKey(itemId);
        }

        public static int ItemAvailableCount(int itemId)
        {
            return byItem[itemId];
        }

        public static (double distance, int itemsRemoved, StationInfo stationInfo) RemoveItem(VectorLF3 playerUPosition, int itemId, int itemCount)
        {
            var stationsWithItem = stations.FindAll(s => s.HasItem(itemId));
            stationsWithItem.Sort((s1, s2) =>
            {
                var s1Distance = s1.PlanetInfo.lastLocation.Distance(playerUPosition);
                var s2Distance = s2.PlanetInfo.lastLocation.Distance(playerUPosition);
                return s1Distance.CompareTo(s2Distance);
            });
            var removedItemCount = 0;
            double distance = -1.0d;
            StationInfo stationPayingCost = null;
            while (removedItemCount < itemCount && stationsWithItem.Count > 0)
            {
                var stationInfo = stationsWithItem[0];
                stationsWithItem.RemoveAt(0);
                var removedCount =
                    StationStorageManager.RemoveFromStation(stationInfo, itemId, itemCount - removedItemCount);

                if (removedCount > 0)
                    Debug($"Removed {removedCount} of {ItemUtil.GetItemName(itemId)} from station on {stationInfo.PlanetName} for player inventory at {DateTime.Now}");
                removedItemCount += removedCount;
                if (removedCount > 0 && distance < 0)
                {
                    distance = playerUPosition.Distance(stationInfo.PlanetInfo.lastLocation);
                    stationPayingCost = stationInfo;
                }
            }

            return (distance, removedItemCount, stationPayingCost);
        }

        public static int AddItem(VectorLF3 playerUPosition, int itemId, int itemCount)
        {
            var stationsWithItem = stations.FindAll(s => s.HasItem(itemId));
            stationsWithItem.Sort((s1, s2) =>
            {
                var s1Distance = s1.PlanetInfo.lastLocation.Distance(playerUPosition);
                var s2Distance = s2.PlanetInfo.lastLocation.Distance(playerUPosition);
                return s1Distance.CompareTo(s2Distance);
            });
            var addedItemCount = 0;
            while (addedItemCount < itemCount && stationsWithItem.Count > 0)
            {
                var stationInfo = stationsWithItem[0];
                stationsWithItem.RemoveAt(0);
                var stationAddedAmount = itemCount - addedItemCount;
                var addedCount = StationStorageManager.AddToStation(stationInfo, itemId, stationAddedAmount);
                addedItemCount += addedCount;
                Debug(
                    $"Added {addedCount} of {ItemUtil.GetItemName(itemId)} to station {stationInfo.stationId} {stationInfo.ItemTypes} on {stationInfo.PlanetName}");
            }

            if (addedItemCount < itemCount)
            {
                Warn(
                    $"Added less than requested amount of {ItemUtil.GetItemName(itemId)} to stations. Added amount: {addedItemCount}, requested: {itemCount}");
            }

            return addedItemCount;
        }

        public static string ItemSummary(int itemId)
        {
            if (!byItem.ContainsKey(itemId) || !byItemSummary.ContainsKey(itemId))
            {
                if (IsInitted && IsFirstLoadComplete)
                    return "Not available in logistics network";
                return "Personal logistics still loading...";
            }

            var stringBuilder = new StringBuilder();
            stringBuilder.Append($"Supplied: {byItemSummary[itemId].SuppliedItems}\r\n");
            stringBuilder.Append($"Supply: {byItemSummary[itemId].Suppliers}, demand: {byItemSummary[itemId].Requesters}\r\n");
            stringBuilder.Append($"Total items: {byItemSummary[itemId].AvailableItems}\r\n");
            if (PersonalLogisticManager.Instance != null && PersonalLogisticManager.Instance.HasTaskForItem(itemId))
            {
                var itemRequest = PersonalLogisticManager.Instance.GetRequest(itemId);
                if (itemRequest != null && itemRequest.RequestType == RequestType.Load)
                {
                    stringBuilder.Append($"{itemRequest.ItemCount} requested\r\n");
                }
            }

            var bufferedAmount = ShippingManager.GetBufferedItemCount(itemId);

            stringBuilder.Append($"{bufferedAmount} in buffer\r\n");

            return stringBuilder.ToString();
        }

        public static string ShortItemSummary(int itemId)
        {
            if (!byItem.ContainsKey(itemId) || !byItemSummary.ContainsKey(itemId))
            {
                return $"Not available in logistics network";
            }

            var stringBuilder = new StringBuilder($"Total items: {byItem[itemId]}\r\n");
            stringBuilder.Append($"Supplied: {byItemSummary[itemId].SuppliedItems}");
            return stringBuilder.ToString();
        }
    }
}