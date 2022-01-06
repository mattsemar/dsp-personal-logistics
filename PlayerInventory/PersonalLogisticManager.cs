﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using PersonalLogistics.Logistics;
using PersonalLogistics.Model;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.Scripts;
using PersonalLogistics.SerDe;
using PersonalLogistics.Util;
using static PersonalLogistics.Util.Log;
using static PersonalLogistics.Util.Constant;

namespace PersonalLogistics.PlayerInventory
{
    /// <summary>Manages tasks for incoming and outgoing items </summary>
    public class PersonalLogisticManager : InstanceSerializer<PersonalLogisticManager>
    {
        private static readonly int VERSION = 1;

        // private static PersonalLogisticManager _instance;
        private readonly List<PlayerInventoryAction> _inventoryActions = new();
        private readonly HashSet<int> _itemIdsRequested = new();
        private Player _player;
        private readonly PlogPlayerId _playerId;
        private readonly List<ItemRequest> _requests = new();


        public PersonalLogisticManager(Player player, PlogPlayerId plogPlayerId)
        {
            _player = player;
            _playerId = plogPlayerId;
        }


        [CanBeNull]
        public ItemRequest GetRequest(int itemId)
        {
            return _requests.Find(r => r.ItemId == itemId);
        }

        public List<ItemRequest> GetRequests() => _requests;

        public int CancelInboundRequests()
        {
            var shippingManager = GetPlayer().shippingManager;
            if (shippingManager == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var itemRequest in _requests)
            {
                if (itemRequest.RequestType == RequestType.Load && itemRequest.State == RequestState.WaitingForShipping)
                {
                    // so here we've already really added the item to the buffer, despite what we show the user, so first we send all of this item back to network
                    var bufferedItemCount = shippingManager.GetActualBufferedItemCount(itemRequest.ItemId);
                    var removed = GetPlayer().shippingManager.RemoveFromBuffer(itemRequest.ItemId, bufferedItemCount);
                    itemRequest.ItemCount -= removed;
                    itemRequest.State = RequestState.Failed;
                    count++;
                }
            }

            return count;
        }

        public List<PlayerInventoryAction> GetInventoryActions(bool clear = true)
        {
            var result = new List<PlayerInventoryAction>(_inventoryActions);
            if (clear)
            {
                _inventoryActions.Clear();
            }

            return result;
        }

        private void ProcessTasks()
        {
            var requestsToRemove = new List<ItemRequest>();
            for (var i = 0; i < _requests.Count; i++)
            {
                var itemRequest = _requests[i];
                switch (itemRequest.RequestType)
                {
                    case RequestType.Load:
                    {
                        if (ProcessLoadTask(itemRequest))
                        {
                            requestsToRemove.Add(itemRequest);
                        }

                        break;
                    }
                    case RequestType.Store:
                        if (ProcessStoreTask(itemRequest))
                        {
                            requestsToRemove.Add(itemRequest);
                        }

                        break;
                }
            }

            foreach (var itemRequest in requestsToRemove)
            {
                _requests.Remove(itemRequest);
            }

            SyncRequestedItemIds();
        }

        private bool ProcessStoreTask(ItemRequest itemRequest)
        {
            switch (itemRequest.State)
            {
                case RequestState.Created:
                {
                    if (!LogisticsNetwork.HasItem(itemRequest.ItemId))
                    {
                        itemRequest.State = RequestState.Failed;
                        return true;
                    }

                    if (!GetPlayer().shippingManager.AddToBuffer(itemRequest.ItemId, itemRequest.ItemCount))
                    {
                        LogAndPopupMessage($"No room in personal logistics system for {itemRequest.ItemName}");
                        itemRequest.State = RequestState.Failed;
                        return false;
                    }

                    itemRequest.State = RequestState.ReadyForInventoryUpdate;
                    _inventoryActions.Add(new PlayerInventoryAction(itemRequest.ItemId, itemRequest.ItemCount, PlayerInventoryActionType.Remove, itemRequest));
                    break;
                }

                case RequestState.InventoryUpdated:
                case RequestState.Complete:
                    return true;

                case RequestState.Failed:
                {
                    if (TimeSpan.FromSeconds(TimeUtil.GetSecondsFromGameTicks(GameMain.gameTick - itemRequest.CreatedTick)).TotalMinutes > 1)
                    {
                        // maybe item can be stored now
                        itemRequest.State = RequestState.Created;
                    }

                    Warn($"Store task unable to be processed {itemRequest.ItemName}");
                }
                    return false;
            }

            return false;
        }

        private bool ProcessLoadTask(ItemRequest itemRequest)
        {
            if (itemRequest.ItemId == DEBUG_ITEM_ID)
            {
                Debug($"Processing load task {itemRequest}");
            }

            switch (itemRequest.State)
            {
                case RequestState.Created:
                {
                    var removedCount = itemRequest.fillBufferRequest ? 0 : GetPlayer().shippingManager.RemoveFromBuffer(itemRequest.ItemId, itemRequest.ItemCount);
                    if (removedCount > 0)
                    {
                        itemRequest.ComputedCompletionTick = GameMain.gameTick;
                        itemRequest.State = RequestState.ReadyForInventoryUpdate;
                        if (removedCount < itemRequest.ItemCount)
                        {
                            // update task to reflect amount that we actually took from buffer, remaining items will be need to be gotten on the next pass 
                            itemRequest.ItemCount = removedCount;
                        }

                        itemRequest.bufferDebited = true;
                        return false;
                    }

                    if (!LogisticsNetwork.HasItem(itemRequest.ItemId))
                    {
                        if (itemRequest.ItemId == DEBUG_ITEM_ID)
                        {
                            Debug($"No stations with {ItemUtil.GetItemName(itemRequest.ItemId)} found, marking request as failed");
                        }

                        itemRequest.State = RequestState.Failed;
                        return false;
                    }

                    if (GetPlayer().shippingManager.AddRequest(_player.uPosition, _player.position, itemRequest))
                    {
                        itemRequest.State = RequestState.WaitingForShipping;
                    }
                    else
                    {
                        itemRequest.State = RequestState.Failed;
                    }

                    return false;
                }
                case RequestState.WaitingForShipping:
                {
                    if (GetPlayer().shippingManager.ItemForTaskArrived(itemRequest.guid))
                    {
                        itemRequest.State = itemRequest.fillBufferRequest ? RequestState.Complete : RequestState.ReadyForInventoryUpdate;
                    }

                    break;
                }
                case RequestState.ReadyForInventoryUpdate:
                {
                    var action = new PlayerInventoryAction(itemRequest.ItemId, itemRequest.ItemCount, PlayerInventoryActionType.Add, itemRequest);
                    _inventoryActions.Add(action);
                    if (itemRequest.ItemId == DEBUG_ITEM_ID)
                    {
                        Debug($"Added player inventory action to be done on the main thread {action}");
                    }

                    return false;
                }
                case RequestState.InventoryUpdated:
                case RequestState.Complete:
                case RequestState.Failed:
                    return true;
            }

            return false;
        }

        private void SyncRequestedItemIds()
        {
            _itemIdsRequested.Clear();
            _requests.ForEach(r => _itemIdsRequested.Add(r.ItemId));
        }

        public bool HasTaskForItem(int itemId)
        {
            if (itemId == DEBUG_ITEM_ID)
            {
                Debug($"checking if task exists for item {itemId} result: {_itemIdsRequested.Contains(itemId)}");
            }

            return _itemIdsRequested.Contains(itemId);
        }

        private void AddTask(ItemRequest itemRequest)
        {
            if (_itemIdsRequested.Contains(itemRequest.ItemId))
            {
                // see if we can update the existing request before creating a new one
                var itemRequests = _requests.FindAll(r =>
                    r.ItemId == itemRequest.ItemId && r.State == RequestState.Created &&
                    r.RequestType == itemRequest.RequestType);
                if (itemRequests.Count > 1)
                {
                    Warn($"more than one task in created state found for item {itemRequest.ItemId}");
                }

                if (itemRequests.Count > 0)
                {
                    // just update to the new value instead of adding to it
                    itemRequests[0].ItemCount = itemRequest.ItemCount;
                    return;
                }

                var oppositeTypeRequests =
                    _requests.FindAll(r => r.ItemId == itemRequest.ItemId && r.State == RequestState.Created);
                if (oppositeTypeRequests.Count > 0)
                {
                    oppositeTypeRequests[0].RequestType = itemRequest.RequestType;
                    oppositeTypeRequests[0].ItemCount = itemRequest.ItemCount;
                    for (var i = 1; i < oppositeTypeRequests.Count; i++)
                    {
                        var oppositeTypeRequest = oppositeTypeRequests[i];
                        _requests.Remove(oppositeTypeRequest);
                    }

                    return;
                }
            }

            _itemIdsRequested.Add(itemRequest.ItemId);
            _requests.Add(itemRequest);
        }

        public void SyncInventory()
        {
            if (PluginConfig.IsPaused())
            {
                return;
            }

            var inventoryManager = GetPlayer().inventoryManager;

            var itemRequests = inventoryManager.GetItemRequests();
            foreach (var request in itemRequests
                         .Where(request => !HasTaskForItem(request.ItemId)))
            {
                AddTask(request);
            }

            GridItem itemToRecycle = RecycleWindow.GetItemToRecycle();
            if (itemToRecycle != null)
            {
                var itemRequest = new ItemRequest
                {
                    ItemCount = itemToRecycle.Count, ItemId = itemToRecycle.ItemId, RequestType = RequestType.Store, ItemName = ItemUtil.GetItemName(itemToRecycle.ItemId),
                    FromRecycleArea = true, RecycleAreaIndex = itemToRecycle.Index
                };
                AddTask(itemRequest);
            }

            ProcessTasks();
        }

        public void FillBuffer()
        {
            if (PluginConfig.IsPaused())
            {
                return;
            }

            var itemRequests = GetPlayer().inventoryManager.GetFillBufferRequests();
            foreach (var request in itemRequests)
            {
                AddTask(request);
            }

            ProcessTasks();
        }

        public void Export(BinaryWriter binaryWriter)
        {
            binaryWriter.Write(VERSION);
            binaryWriter.Write(_requests.FindAll(r => !r.FromRecycleArea).Count);
            foreach (var request in _requests)
            {
                if (request.FromRecycleArea)
                    continue;
                request.Export(binaryWriter);
            }

            binaryWriter.Write(_inventoryActions.FindAll(ia => !ia.Request.FromRecycleArea).Count);
            foreach (var playerInventoryAction in _inventoryActions)
            {
                if (playerInventoryAction.Request.FromRecycleArea)
                    continue;
                playerInventoryAction.Export(binaryWriter);
            }
        }

        public void Import(BinaryReader r)
        {
            Debug($"reading PLM data");
            try
            {
                var ver = r.ReadInt32();
                if (ver != VERSION)
                {
                    Debug($"PLM version {VERSION} does not match save file version {ver}");
                }

                _inventoryActions.Clear();
                _requests.Clear();
                _itemIdsRequested.Clear();

                _player = GameMain.mainPlayer;
                var requestCount = r.ReadInt32();
                for (var i = 0; i < requestCount; i++)
                {
                    var itemRequest = ItemRequest.Import(r);
                    _itemIdsRequested.Add(itemRequest.ItemId);
                    _requests.Add(itemRequest);
                }

                var actionCount = r.ReadInt32();
                for (var i = 0; i < actionCount; i++)
                {
                    var playerInventoryAction = PlayerInventoryAction.Import(r);
                    // swap out request instance from the PLM instance so the refs are the same
                    var itemRequest = _requests.Find(req => req.guid == playerInventoryAction.Request?.guid);
                    if (itemRequest != null)
                    {
                        playerInventoryAction.Request = itemRequest;
                    }
                    else
                    {
                        Warn($"failed to player inventory actual item req with actual from PLM. {playerInventoryAction.Request}");
                    }

                    _inventoryActions.Add(playerInventoryAction);
                }

                Debug($"PLM read in {requestCount} requests and {actionCount} actions");
            }
            catch (Exception e)
            {
                Warn($"failed to read PLM data: {e.Message}");
                _player = GameMain.mainPlayer;
            }
        }

        public override void ExportData(BinaryWriter w)
        {
            Export(w);
        }

        public override PlogPlayerId GetPlayerId() => _playerId;
    }
}