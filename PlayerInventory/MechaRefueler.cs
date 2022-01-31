using System.Collections.Generic;
using PersonalLogistics.Model;
using PersonalLogistics.Util;

namespace PersonalLogistics.PlayerInventory
{
    public class MechaRefueler
    {
        private readonly InventoryManager _inventoryManager;
        private readonly StorageComponent _inventory;
        private readonly StorageComponent _mechaReactorStorage;

        public MechaRefueler(InventoryManager inventoryManager, Player player)
        {
            _inventoryManager = inventoryManager;
            _inventory = player?.package;
            _mechaReactorStorage = player?.mecha?.reactorStorage;
        }

        public void Refuel()
        {
            if (_inventory == null || _mechaReactorStorage == null)
            {
                Log.Warn($"can't refuel mecha inventory or reactor is null {_inventory == null}");
                return;
            }

            if (_mechaReactorStorage.isFull)
                return;

            // simplest case is that there is already fuel in each slot but it's not stacked fully.
            TopOffFuelSlots();
            _inventory.NotifyStorageChange();
            _mechaReactorStorage.NotifyStorageChange();
            if (_mechaReactorStorage.isFull)
                return;
            FillEmptyFuelSlots();
            _inventory.NotifyStorageChange();
            _mechaReactorStorage.NotifyStorageChange();
        }

        private void FillEmptyFuelSlots()
        {
            List<ItemProto> desiredFuelItems = BuildFuelItemPriorityList();
            // on to the more complicated flow, decide what to add based on set of ridiculous rules
            foreach (var fuelItem in desiredFuelItems)
            {
                for (int i = 0; i < _mechaReactorStorage.grids.Length; ++i)
                {
                    // if there is already something in the slot it should've been handle by the topoff part
                    if (_mechaReactorStorage.grids[i].itemId != 0)
                        continue;
                    if (_inventoryManager.GetInventoryCount(fuelItem.ID).ItemCount < 1)
                        continue;
                    var mostStackedGridId = 0;
                    var toppedOff = false;
                    var countdown = 5;
                    do
                    {
                        mostStackedGridId = _inventoryManager.GetGridIndexWithMostPoints(fuelItem.ID);
                        if (mostStackedGridId < 0)
                        {
                            break;
                        }

                        var inventoryGrid = _inventory.grids[mostStackedGridId];
                        var addedCount = _mechaReactorStorage.AddItemStacked(inventoryGrid.itemId, inventoryGrid.count, inventoryGrid.inc, out int remainInc);

                        _inventory.grids[mostStackedGridId].count = inventoryGrid.count - addedCount;
                        _inventory.grids[mostStackedGridId].inc = remainInc;
                        if (_inventory.grids[mostStackedGridId].count == 0)
                        {
                            _inventory.grids[mostStackedGridId].itemId = _inventory.grids[mostStackedGridId].filter;
                            _inventory.grids[mostStackedGridId].inc = 0;
                            if (_inventory.grids[mostStackedGridId].filter == 0)
                                _inventory.grids[mostStackedGridId].stackSize = 0;
                            _inventory.NotifyStorageChange();
                        }
                        if (_mechaReactorStorage.grids[i].count >= _mechaReactorStorage.grids[i].stackSize)
                        {
                            toppedOff = true;
                        }
                    } while (!toppedOff && --countdown > 0);
                }

                if (_mechaReactorStorage.isFull)
                    break;
            }
        }

        private void TopOffFuelSlots()
        {
            for (int i = 0; i < _mechaReactorStorage.grids.Length; ++i)
            {
                if (_mechaReactorStorage.grids[i].itemId == 0)
                    continue;
                if (_mechaReactorStorage.grids[i].count == _mechaReactorStorage.grids[i].stackSize)
                {
                    // already full
                    continue;
                }

                var itemId = _mechaReactorStorage.grids[i].itemId;

                var mostStackedGridId = 0;
                var toppedOff = false;
                int ctr = 5;
                do
                {
                    mostStackedGridId = _inventoryManager.GetGridIndexWithMostPoints(itemId);
                    if (mostStackedGridId < 0)
                    {
                        break;
                    }

                    var inventoryGrid = _inventory.grids[mostStackedGridId];
                    var invItemStack = ItemStack.FromCountAndPoints(inventoryGrid.count, inventoryGrid.inc);
                    var itemsNeededForSlot = _mechaReactorStorage.grids[i].stackSize - _mechaReactorStorage.grids[i].count;
                    var removedStack = invItemStack.Remove(itemsNeededForSlot);
                    Log.Info($"updating reactor index {i}, incrementing with {removedStack}");
                    _mechaReactorStorage.grids[i].count += removedStack.ItemCount;
                    _mechaReactorStorage.grids[i].inc += removedStack.ProliferatorPoints;
                    _inventory.grids[mostStackedGridId].count = invItemStack.ItemCount;
                    _inventory.grids[mostStackedGridId].inc = invItemStack.ProliferatorPoints;
                    if (_inventory.grids[mostStackedGridId].count == 0)
                    {
                        _inventory.grids[mostStackedGridId].itemId = _inventory.grids[mostStackedGridId].filter;
                        _inventory.grids[mostStackedGridId].inc = 0;
                        if (_inventory.grids[mostStackedGridId].filter == 0)
                            _inventory.grids[mostStackedGridId].stackSize = 0;
                    }

                    if (_mechaReactorStorage.grids[i].count >= _mechaReactorStorage.grids[i].stackSize)
                    {
                        toppedOff = true;
                    }
                } while (!toppedOff && ctr-- > 0);

                if (ctr <= 0)
                {
                    Log.Warn($"Hit bailout on countdown in TopOffFuelSlots");
                }
            }
        }

        private List<ItemProto> BuildFuelItemPriorityList()
        {
            var currentFuelIds = GetMechaFuelStorageItems();
            var fuelItems = ItemUtil.GetFuelItemProtos();
            fuelItems.Sort((i1, i2) =>
            {
                if (i1.ID == i2.ID) // should not actually happen
                {
                    return 0;
                }

                var priority1 = GameMain.mainPlayer.mecha.reactorItemId == i1.ID ? -1000 : 0;
                var priority2 = GameMain.mainPlayer.mecha.reactorItemId == i2.ID ? -1000 : 0;

                if (currentFuelIds.ContainsKey(i1.ID))
                {
                    priority1 -= 1_000_000 - currentFuelIds[i1.ID];
                }

                if (currentFuelIds.ContainsKey(i2.ID))
                {
                    priority2 -= 1_000_000 - currentFuelIds[i2.ID];
                }

                if (InventoryManager.GetMinRequestAmount(i1.ID) > 0)
                {
                    priority1 -= 100;
                }

                if (InventoryManager.GetMinRequestAmount(i2.ID) > 0)
                {
                    priority2 -= 100;
                }

                if (priority1 != priority2)
                {
                    return priority1.CompareTo(priority2);
                }

                return i2.HeatValue.CompareTo(i1.HeatValue);
            });
            return fuelItems;
        }

        private Dictionary<int, int> GetMechaFuelStorageItems()
        {
            var result = new Dictionary<int, int>();
            foreach (var grid in _mechaReactorStorage.grids)
            {
                if (grid.itemId > 0)
                {
                    if (!result.ContainsKey(grid.itemId))
                    {
                        result[grid.itemId] = grid.count;
                    }
                    else
                    {
                        result[grid.itemId] += grid.count;
                    }
                }
            }

            return result;
        }
    }
}