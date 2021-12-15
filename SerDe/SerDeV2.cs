using System;
using System.IO;
using PersonalLogistics.Model;
using PersonalLogistics.PlayerInventory;
using PersonalLogistics.Scripts;
using PersonalLogistics.Shipping;
using PersonalLogistics.Util;

namespace PersonalLogistics.SerDe
{
    public class SerDeV2 : ISerDe
    {
        public void Import(BinaryReader r)
        {
            try
            {
                PersonalLogisticManager.Import(r);
            }
            catch (Exception e)
            {
                PersonalLogisticManager.InitOnLoad();
            }

            try
            {
                ShippingManager.Import(r);
            }
            catch (Exception e)
            {
                ShippingManager.InitOnLoad();
            }

            try
            {
                DesiredInventoryState.Import(r);
            }
            catch (Exception e)
            {
                var desiredInventoryState = DesiredInventoryState.instance;
                Log.Debug($"loaded desired inv state from config property. {desiredInventoryState.BannedItems.Count}\t{desiredInventoryState.DesiredItems.Count}");
            }

            try
            {
                RecycleWindow.Import(r);
            }
            catch (Exception e)
            {
                RecycleWindow.InitOnLoad();
            }
        }

        public void Export(BinaryWriter w)
        {
            w.Write(2);
            PersonalLogisticManager.Export(w);
            ShippingManager.Export(w);
            DesiredInventoryState.Export(w);
            RecycleWindow.Export(w);
        }
    }
}