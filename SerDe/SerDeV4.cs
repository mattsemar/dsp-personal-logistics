using System.Collections.Generic;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.Scripts;
using PersonalLogistics.Util;

namespace PersonalLogistics.SerDe
{
    /// <summary>Try and isolate failures from affecting other parts by building a map of offsets similar to how the plugin does </summary>
    public class SerDeV4 : TocBasedSerDe
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

            if (PlogPlayerRegistry.LocalPlayer().playerStateContainerPersistence == null)
            {
                Log.Debug($"Player states are null, long live player states");
                result.Add(new PlayerStateContainerPersistence(PlogPlayerRegistry.LocalPlayer().playerId));
            }
            else
            {
                result.Add(PlogPlayerRegistry.LocalPlayer().playerStateContainerPersistence);
            }

            return result;
        }

        protected override int GetVersion() => 4;
    }
}