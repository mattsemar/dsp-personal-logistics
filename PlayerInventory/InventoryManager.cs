using System.Collections.Generic;
using PersonalLogistics.Model;
using PersonalLogistics.Shipping;
using PersonalLogistics.Util;
using static PersonalLogistics.Util.Constant;

namespace PersonalLogistics.PlayerInventory
{
    public class InventoryManager
    {
        private static InventoryManager _instance;
        public static InventoryManager instance => GetInstance();

        private Player _player;
        private DesiredInventoryState _desiredInventoryState;

        private InventoryManager(Player player)
        {
            _player = player;
            _desiredInventoryState = CrossSeedInventoryState.instance?.GetStateForSeed(GameUtil.GetSeed());
        }

        public DesiredInventoryState SaveInventoryAsDesiredState()
        {
            var inv = _player.package;
            var itemCounts = new Dictionary<int, int>();
            for (int index = 0; index < inv.size; ++index)
            {
                var itemId = inv.grids[index].itemId;
                if (itemId == 0)
                    continue;
                var count = inv.grids[index].count;
                if (itemCounts.TryGetValue(itemId, out _))
                {
                    itemCounts[itemId] += count;
                }
                else
                {
                    itemCounts[itemId] = count;
                }
            }

            _desiredInventoryState.ClearAll();
            foreach (var itemAndCount in itemCounts)
            {
                if (itemAndCount.Key == DEBUG_ITEM_ID)
                    Log.Debug($"Adding item {itemAndCount.Key} {ItemUtil.GetItemName(itemAndCount.Key)} count={itemAndCount.Value} to desired list");
                _desiredInventoryState.AddDesiredItem(itemAndCount.Key, itemAndCount.Value, itemAndCount.Value);
            }

            foreach (var item in LDB._items.dataArray)
            {
                if (!_desiredInventoryState.IsDesiredOrBanned(item.ID))
                {
                    Log.Debug($"Adding item {item.ID} {ItemUtil.GetItemName(item.ID)} to ban list");
                    _desiredInventoryState.AddBan(item.ID);
                }
            }

            CrossSeedInventoryState.instance.SetStateForSeed(GameUtil.GetSeed(), _desiredInventoryState);
            return _desiredInventoryState;
        }

        public (int minDesiredAmount, int maxDesiredAmount, bool allowBuffer) GetDesiredAmount(int itemId)
        {
            if (!_desiredInventoryState.IsDesiredOrBanned(itemId))
            {
                return (0, int.MaxValue, true);
            }

            if (_desiredInventoryState.BannedItems.Contains(itemId))
            {
                return (0, 0, true);
            }

            if (_desiredInventoryState.DesiredItems.TryGetValue(itemId, out DesiredItem desiredItem))
            {
                return (desiredItem.count, desiredItem.maxCount, desiredItem.allowBuffering);
            }

            Log.Warn($"Unexpected state for item {itemId}. Not in ban list or desired list");
            return (-1, -1, true);
        }

        public List<ItemRequest> GetItemRequests()
        {
            if (_player == null)
            {
                Log.Warn($"player is null");
                _player = GameMain.mainPlayer;
                return new List<ItemRequest>();
            }

            var inv = _player.package;
            if (inv?.grids == null)
            {
                Log.Warn($"player package is null == {inv == null} || grids is null {inv?.grids == null}");
                _player = GameMain.mainPlayer;
                return new List<ItemRequest>();
            }

            var itemCounts = new Dictionary<int, int>();
            for (int index = 0; index < inv.size; ++index)
            {
                var itemId = inv.grids[index].itemId;
                if (itemId < 1)
                {
                    continue;
                }

                var count = inv.grids[index].count;
                if (itemCounts.TryGetValue(itemId, out _))
                {
                    itemCounts[itemId] += count;
                }
                else
                {
                    itemCounts[itemId] = count;
                }
            }

            if (_player.inhandItemId > 0 && _player.inhandItemCount > 0)
            {
                itemCounts.TryGetValue(_player.inhandItemId, out var value);
                itemCounts[_player.inhandItemId] = value + _player.inhandItemCount;
            }

            var result = new List<ItemRequest>(itemCounts.Keys.Count);
            foreach (var item in ItemUtil.GetAllItems())
            {
                var curCount = itemCounts.ContainsKey(item.ID) ? itemCounts[item.ID] : 0;
                var (action, actionCount, skipBuffer) =
                    _desiredInventoryState.GetActionForItem(item.ID, curCount);
                if (DEBUG_ITEM_ID == item.ID)
                {
                    Log.Debug($"action for item {item.ID} {action} {actionCount}");
                }
                switch (action)
                {
                    case DesiredInventoryAction.None:
                        continue;
                    case DesiredInventoryAction.Add:
                        result.Add(new ItemRequest
                            { ItemCount = actionCount, ItemId = item.ID, RequestType = RequestType.Load, ItemName = item.Name.Translate(), SkipBuffer = skipBuffer });
                        break;
                    case DesiredInventoryAction.Remove:
                        result.Add(new ItemRequest
                            { ItemCount = actionCount, ItemId = item.ID, RequestType = RequestType.Store, ItemName = item.Name.Translate() });
                        break;
                }

                if (item.ID == DEBUG_ITEM_ID)
                    Log.Debug($"Added new ItemRequest {result[result.Count - 1]}");
            }

            return result;
        }

        public bool IsBanned(int itemId) => _desiredInventoryState.BannedItems.Contains(itemId);


        public static void Reset()
        {
            if (_instance == null)
                return;
            _instance._player = null;
            _instance._desiredInventoryState = null;
            _instance = null;
        }

        private static InventoryManager GetInstance()
        {
            if (_instance == null && GameMain.mainPlayer == null)
                return null;
            var result = _instance ?? (_instance = new InventoryManager(GameMain.mainPlayer));
            if (result._player == null)
            {
                result._player = GameMain.mainPlayer;
            }

            if (result._desiredInventoryState == null)
            {
                if (!CrossSeedInventoryState.IsInitialized)
                    return null;
                result._desiredInventoryState = CrossSeedInventoryState.instance?.GetStateForSeed(GameUtil.GetSeed());
            }

            if (result._desiredInventoryState == null)
            {
                result._desiredInventoryState = new DesiredInventoryState();
            }

            return result;
        }

        public void ProcessInventoryActions()
        {
            if (PersonalLogisticManager.Instance == null)
            {
                return;
            }

            var playerInventoryActions = PersonalLogisticManager.Instance.GetInventoryActions();
            foreach (var action in playerInventoryActions)
            {
                if (action.ItemId == DEBUG_ITEM_ID)
                    Log.Debug($"Performing inventory action {action}");
                if (action.ActionType == PlayerInventoryActionType.Add)
                {
                    var removedFromBuffer = action.Request.bufferDebited ? action.ItemCount : 0;
                    if (!action.Request.bufferDebited)
                    {
                        removedFromBuffer += ShippingManager.Instance.RemoveItemsFromBuffer(action.ItemId, action.ItemCount);
                    }
                    Log.Debug($"item request status is complete, remove from buffer {action.Request.ItemName}  {action.ItemCount}, actually removed {removedFromBuffer}");
                    var addItem = _player.package.AddItem(action.ItemId, removedFromBuffer);
                    if (action.ItemId == DEBUG_ITEM_ID)
                        Log.Debug($"successful={addItem} added {ItemUtil.GetItemName(action.ItemId)} count={action.ItemCount}");
                    if (addItem < removedFromBuffer)
                    {
                        // inventory would not hold amount that we took out of buffer, add some back
                        var returnToBuffer = removedFromBuffer - addItem;
                        Log.Debug($"Re-adding {returnToBuffer} of {action.Request.ItemName} back into buffer");
                        ShippingManager.AddToBuffer(action.ItemId, returnToBuffer);
                    }
                    action.Request.State = RequestState.Complete;
                    if (addItem > 0)
                        UIItemup.Up(action.ItemId, addItem);
                }
                else if (action.ActionType == PlayerInventoryActionType.Remove)
                {
                    int itmId = action.ItemId;
                    int itmCnt = action.ItemCount;
                    _player.package.TakeTailItems(ref itmId, ref itmCnt);
                    var success = itmCnt == action.ItemCount;
                    action.Request.State = RequestState.Complete;
                    if (itmId == DEBUG_ITEM_ID)
                        Log.Debug($"successful={success} added {ItemUtil.GetItemName(action.ItemId)} count={itmCnt} (requestedCnt={action.ItemCount})");
                }
                else
                {
                    if (action.ItemId == DEBUG_ITEM_ID)
                        Log.Warn($"Unhandled action type {action} {action.ActionType}");
                }

                if (PluginConfig.sortInventory.Value)
                {
                    _player.package.Sort();
                }
            }
        }

        public void BanItem(int itemID)
        {
            if (_desiredInventoryState.DesiredItems.ContainsKey(itemID))
                _desiredInventoryState.DesiredItems.Remove(itemID);
            _desiredInventoryState.AddBan(itemID);
            CrossSeedInventoryState.instance.SetStateForSeed(GameUtil.GetSeed(), _desiredInventoryState);
        }

        public void UnBanItem(int itemID)
        {
            _desiredInventoryState.BannedItems.Remove(itemID);
            CrossSeedInventoryState.instance.SetStateForSeed(GameUtil.GetSeed(), _desiredInventoryState);
        }

        public void SetDesiredAmount(int itemID, int newValue, int maxValue)
        {
            if (_desiredInventoryState.BannedItems.Contains(itemID))
            {
                _desiredInventoryState.BannedItems.Remove(itemID);
            }

            _desiredInventoryState.AddDesiredItem(itemID, newValue, maxValue);
        }

        public void SaveDesiredStateFromOther(string otherStateString)
        {
            var otherDesiredState = DesiredInventoryState.LoadStored(otherStateString);
            _desiredInventoryState.ClearAll();
            _desiredInventoryState.BannedItems = new HashSet<int>(otherDesiredState.BannedItems);
            _desiredInventoryState.DesiredItems = new Dictionary<int, DesiredItem>(otherDesiredState.DesiredItems);
            CrossSeedInventoryState.instance.SetStateForSeed(GameUtil.GetSeed(), _desiredInventoryState);
            CrossSeedInventoryState.Save();
        }

        public void Clear()
        {
            _desiredInventoryState.ClearAll();
            CrossSeedInventoryState.instance.SetStateForSeed(GameUtil.GetSeed(), _desiredInventoryState);
        }

        public bool RemoveItemImmediately(int itemId, int count)
        {
            int cnt = count;
            _player.package.TakeTailItems(ref itemId, ref cnt);
            if (PluginConfig.sortInventory.Value)
            {
                _player.package.Sort();
            }
            return cnt == count;
        }

        public int AddItemToInventory(int itemId, int itemCount)
        {
            var added = _player.package.AddItem(itemId, itemCount);

            if (added > 0)
                UIItemup.Up(itemId, added);
            if (added > 0 && PluginConfig.sortInventory.Value)
                _player.package.Sort();
            return added;
        }

        public void ToggleBuffering(int itemID)
        {
            if (_desiredInventoryState.DesiredItems.ContainsKey(itemID))
            {
                _desiredInventoryState.DesiredItems[itemID].allowBuffering = !_desiredInventoryState.DesiredItems[itemID].allowBuffering;
            }
            else
            {
                Log.Warn($"Item {itemID} not found. Buffering will not be toggled");
            }
        }
    }
}