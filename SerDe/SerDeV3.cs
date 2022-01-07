using System.Collections.Generic;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.Scripts;
using PersonalLogistics.Util;

namespace PersonalLogistics.SerDe
{
    /// <summary>Try and isolate failures from affecting other parts by building a map of offsets similar to how the plugin does </summary>
    public class SerDeV3 : TocBasedSerDe
    {
        public override List<InstanceSerializer> GetSections()
        {
            var result = new List<InstanceSerializer>
            {
                PlogPlayerRegistry.LocalPlayer().personalLogisticManager,
                PlogPlayerRegistry.LocalPlayer().shippingManager,
                PlogPlayerRegistry.LocalPlayer().inventoryManager,
            };
            if (PlogPlayerRegistry.LocalPlayer().recycleWindowPersistence != null)
            {
                result.Add(PlogPlayerRegistry.LocalPlayer().recycleWindowPersistence);
            }
            else
            {
                Log.Debug($"Recycle window persistence not initted");
                result.Add(new RecycleWindowPersistence(PlogPlayerRegistry.LocalPlayer().playerId));
            }

            return result;
            // { typeof(PersonalLogisticManager), PlogPlayerRegistry.LocalPlayer().personalLogisticManager.ExportData },
            //         { typeof(ShippingManager), PlogPlayerRegistry.LocalPlayer().shippingManager.ExportData },
            //         { typeof(DesiredInventoryState), PlogPlayerRegistry.LocalPlayer().inventoryManager.ExportData },
            //         { typeof(RecycleWindow), RecycleWindow.Export }           
        }

        protected override int GetVersion() => 3;
    }
}