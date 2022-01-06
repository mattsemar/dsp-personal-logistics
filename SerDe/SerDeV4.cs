using System;
using System.Collections.Generic;
using System.IO;
using PersonalLogistics.Model;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.PlayerInventory;
using PersonalLogistics.Scripts;
using PersonalLogistics.Shipping;
using PersonalLogistics.Util;

namespace PersonalLogistics.SerDe
{
    /// <summary>Try and isolate failures from affecting other parts by building a map of offsets similar to how the plugin does </summary>
    public class SerDeV4 : TocBasedSerDe
    {
        private static readonly List<Type> _parts = new()
        {
            typeof(PersonalLogisticManager),
            typeof(ShippingManager),
            typeof(DesiredInventoryState),
            typeof(RecycleWindow),
            typeof(PlayerStateContainer)
        };

        private static readonly Dictionary<Type, string> _typeNames = new()
        {
            { typeof(PersonalLogisticManager), "PLM" },
            { typeof(ShippingManager), "SM" },
            { typeof(DesiredInventoryState), "DINV" },
            { typeof(RecycleWindow), "RW" },
            { typeof(PlayerStateContainer), "PSC" }
        };

        protected override List<Type> GetParts()
        {
            return _parts;
        }

        protected override Dictionary<Type, string> GetTypeNames() => _typeNames;

        protected override Dictionary<Type, Action<BinaryWriter>> GetExportActions()
        {
            return new()
            {
                { typeof(PersonalLogisticManager), PlogPlayerRegistry.LocalPlayer().personalLogisticManager.ExportData },
                { typeof(ShippingManager), PlogPlayerRegistry.LocalPlayer().shippingManager.ExportData },
                { typeof(DesiredInventoryState), PlogPlayerRegistry.LocalPlayer().inventoryManager.ExportData },
                { typeof(RecycleWindow), RecycleWindow.Export },
                { typeof(PlayerStateContainer), PlayerStateContainer.Export }
            };
        }

        protected override Dictionary<Type, Action<BinaryReader>> GetImportActions()
        {
            return new()
            {
                { typeof(PersonalLogisticManager), PlogPlayerRegistry.LocalPlayer().personalLogisticManager.Import },
                { typeof(ShippingManager), PlogPlayerRegistry.LocalPlayer().shippingManager.Import },
                { typeof(DesiredInventoryState), PlogPlayerRegistry.LocalPlayer().inventoryManager.desiredInventoryState.ImportData },
                { typeof(RecycleWindow), RecycleWindow.Import }, 
                { typeof(PlayerStateContainer), PlayerStateContainer.Import }
            };
        }

        // used when imports fail
        protected override Dictionary<Type, Action> GetInitActions()
        {
            return new()
            {
                { typeof(PersonalLogisticManager), () => Log.Debug("no init for PLM") },
                { typeof(ShippingManager), () => Log.Debug("no init for ShipMgr") },
                { typeof(DesiredInventoryState), DesiredInventoryState.InitOnLoad },
                { typeof(RecycleWindow), RecycleWindow.InitOnLoad },
                { typeof(PlayerStateContainer), PlayerStateContainer.Clear }
            };
        }

        protected override int getVersion() => 4;
    }
}