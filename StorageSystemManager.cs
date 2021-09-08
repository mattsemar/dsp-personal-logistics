using System;
using UnityEngine;

namespace NetworkManager
{
    /// <summary>Helps finding items on local planet from player, storage boxes and logistics stations</summary>
    public class StorageSystemManager
    {
        private static StorageSystemManager _instance;
        private readonly PlanetFactory _factory;
        private readonly Player _player;

        private StorageSystemManager(PlanetFactory factory, Player player)
        {
            _factory = factory;
            _player = player;
        }

        private static StorageSystemManager GetInstance()
        {
            if (_instance == null)
            {
                _instance = new StorageSystemManager(GameMain.mainPlayer.factory, GameMain.mainPlayer);
                return _instance;
            }

            if (_instance._factory?.index == GameMain.mainPlayer.factory.index)
            {
                return _instance;
            }

            _instance = new StorageSystemManager(GameMain.mainPlayer.factory, GameMain.mainPlayer);
            return _instance;
        }

        private int CountLocalStationItems(int itemId)
        {
            var result = 0;
            for (int i = 1; i < _factory.transport.stationCursor; i++)
            {
                var station = _factory.transport.stationPool[i];
                if (station.id <= 0)
                    continue;
                foreach (var store in station.storage)
                {
                    if (store.itemId < 1)
                    {
                        continue;
                    }


                    if (store.itemId != itemId)
                        continue;
                    result += store.count;
                }
            }

            return result;
        }

        private int CountLocalStorageItems(int itemId)
        {
            var result = 0;
            if (_factory.factoryStorage?.storagePool == null)
            {
                return result;
            }

            for (int i = 0; i < _factory.factoryStorage.storageCursor; i++)
            {
                var storageComponent = _factory.factoryStorage.storagePool[i];
                if (storageComponent == null || storageComponent.id <= 0)
                    continue;
                result += storageComponent.GetItemCount(itemId);
            }

            return result;
        }

        public static (int itemsRemoved, bool successful) RemoveItems(int itemId, int count)
        {
            var itemsRemoved = GetInstance().DoRemove(itemId, count);
            return (itemsRemoved, itemsRemoved >= count);
        }

        private int DoRemove(int itemId, int count)
        {
            var removed = 0;
            var (removedFromLocation, successful) = RemoveFromPlayer(itemId, count);

            if (successful)
            {
                return removedFromLocation;
            }

            removed += removedFromLocation;

            (removedFromLocation, successful) = RemoveFromStorage(itemId, count - removed);

            if (successful)
                return count;
            removed += removedFromLocation;
            (removedFromLocation, successful) = RemoveFromStations(itemId, count - removed);
            return removed + removedFromLocation;
        }

        private (int, bool successful) RemoveFromStorage(int itemId, int count)
        {
            var removed = 0;
            for (int i = 0; i < _factory.factoryStorage.storageCursor; i++)
            {
                var storageComponent = _factory.factoryStorage.storagePool[i];
                if (storageComponent?.id != i)
                    continue;
                var remaining = count - removed;
                var itemIdRef = itemId;
                var remainRef = remaining;
                storageComponent.TakeTailItems(ref itemIdRef, ref remainRef);
                removed += remainRef;
                if (removed >= count)
                    return (removed, true);
            }

            return (removed, removed >= count);
        }

        private (int removed, bool successful) RemoveFromStations(int itemId, int count)
        {
            var removed = 0;
            for (int i = 0; i < _factory.transport.stationCursor; i++)
            {
                var station = _factory.transport.stationPool[i];
                if (station?.id != i)
                    continue;

                var itemRef = itemId;
                var countRef = count - removed;
                station.TakeItem(ref itemRef, ref countRef);
                if (countRef > 0)
                {
                    removed += countRef;
                    if (removed >= count)
                        return (removed, true);
                }
            }

            return (removed, removed >= count);
        }

        private (int, bool) RemoveFromPlayer(int itemId, int count)
        {
            var amountToRemove = count;
            var itemIdRef = itemId;
            GameMain.mainPlayer.package.TakeTailItems(ref itemIdRef, ref amountToRemove);
            return amountToRemove == count ? (amountToRemove, true) : (amountToRemove, false);
        }

        public static (string message, bool hasEnoughCapacity, int remainingToRemove) BuildRemovalMessage(int itemId, int count)
        {
            var result = GetInstance().DoBuildRemovalMessage(itemId, count);
            return (result.Item1.Trim(), Math.Max(0, result.Item2) <= 0, result.Item2);
        }

        private (string, int) DoBuildRemovalMessage(int itemId, int count)
        {
            if (_player?.package == null)
            {
                Log.logger.LogWarning($"player not set");
                return ("Failure while counting items", count);
            }
            var playerItemCount = _player.package.GetItemCount(itemId);
            var itemName = GetItemName(itemId);
            if (playerItemCount >= count)
                return ($"{count} of {itemName} will be removed from your inventory (leaving {playerItemCount - count})", 0);
            var message = "";
            var remainingToRemove = count;
            if (playerItemCount > 0)
            {
                message += $"{playerItemCount} of {itemName} will be removed from your inventory ({count - playerItemCount} needed after). ";
                remainingToRemove -= playerItemCount;
            }

            var localStorageItems = CountLocalStorageItems(itemId);
            if (localStorageItems > remainingToRemove)
            {
                message += $"\n{remainingToRemove} of {itemName} will be removed from storage ({localStorageItems} total). ";
                remainingToRemove -= localStorageItems;
                return (message, remainingToRemove);
            }

            if (localStorageItems > 0)
            {
                message += $"\nAll {localStorageItems} of {itemName} will be removed from storage.";
                remainingToRemove -= localStorageItems;
            }

            if (remainingToRemove <= 0)
                return (message, remainingToRemove);
            var stationItems = CountLocalStationItems(itemId);
            if (stationItems > remainingToRemove)
            {
                message += $"\n{remainingToRemove} of {itemName} will be removed from local logistics stations ({stationItems} total). ";
                remainingToRemove -= stationItems;
                return (message, remainingToRemove);
            }

            if (stationItems > 0)
            {
                message += $"\nAll {stationItems} of {itemName} will be removed from local logistics stations.";
                remainingToRemove -= stationItems;
            }

            return (message, remainingToRemove);
        }

        private static string GetItemName(int itemId)
        {
            return LDB._items.Select(itemId).Name.Translate();
        }
    }
}