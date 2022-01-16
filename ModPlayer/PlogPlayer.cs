using System;
using System.IO;
using System.Text;
using PersonalLogistics.PlayerInventory;
using PersonalLogistics.Scripts;
using PersonalLogistics.Shipping;
using PersonalLogistics.Util;
using UnityEngine;

namespace PersonalLogistics.ModPlayer
{
    public class PlogPlayerPosition
    {
        public Vector3 planetPosition;
        public VectorLF3 clusterPosition;
    }

    public abstract class PlogPlayer
    {
        public readonly PlogPlayerId playerId;
        public PersonalLogisticManager personalLogisticManager;
        public ShippingManager shippingManager;
        public InventoryManager inventoryManager;
        public RecycleWindowPersistence recycleWindowPersistence;
        public PlayerStateContainerPersistence playerStateContainerPersistence;

        public readonly bool isRemote;

        public PlogPlayer(PlogPlayerId playerId, bool isRemote, Player player = null)
        {
            this.playerId = playerId;
            personalLogisticManager = new PersonalLogisticManager(player, playerId);
            shippingManager = new ShippingManager(playerId);
            inventoryManager = new InventoryManager(this);

            this.isRemote = isRemote;
        }

        public abstract PlogPlayerPosition GetPosition();

        public abstract int PackageSize();
        public abstract int PlanetId();

        public override string ToString()
        {
            return $"PLOGPlayer: {playerId}, isRemote: {isRemote}";
        }

        public abstract float QueryEnergy(long valueEnergyCost);

        public abstract void UseEnergy(float energyToUse, int type);

        public abstract void NotifyLeavePlanet();

        public string SummarizeState()
        {
            var sb = new StringBuilder($"Player: ${playerId}\r\n");
            sb.AppendLine(personalLogisticManager == null ? "null PLM" : $"{personalLogisticManager.SummarizeState()}");
            sb.AppendLine(shippingManager != null ? $"{shippingManager.SummarizeState()}" : "null shipping manager");
            sb.AppendLine(inventoryManager != null ? $"{inventoryManager.SummarizeState()}" : "null inv mgr");
            sb.AppendLine(playerStateContainerPersistence != null ? $"{playerStateContainerPersistence.SummarizeState()}" : "null psc");
            sb.AppendLine(recycleWindowPersistence == null ? "null RW" : recycleWindowPersistence.SummarizeState());
            return sb.ToString();
        }
    }

    public class PlogPlayerId : IComparable<PlogPlayerId>, IEquatable<PlogPlayerId>
    {
        private static readonly int _version = 1;
        public readonly int gameSeed;
        public readonly Guid assignedId;

        public PlogPlayerId(int gameSeed, Guid assignedId)
        {
            this.gameSeed = gameSeed;
            this.assignedId = assignedId;
        }

        public int CompareTo(PlogPlayerId other)
        {
            if (gameSeed != other.gameSeed)
            {
                return gameSeed.CompareTo(other.gameSeed);
            }

            return assignedId.CompareTo(other.assignedId);
        }

        public override bool Equals(object obj)
        {
            if (obj is PlogPlayerId playerId)
                return playerId == this;
            return false;
        }

        public override int GetHashCode() => ToString().GetHashCode();

        public bool Equals(PlogPlayerId other)
        {
            return CompareTo(other) == 0;
        }

        public override string ToString()
        {
            return $"{assignedId}_{gameSeed}";
        }

        public static PlogPlayerId ComputeLocalPlayerId()
        {
            return new PlogPlayerId(GameUtil.GetSeedInt(), PluginConfig.GetAssignedUserId());
        }

        public static bool operator ==(PlogPlayerId p1, PlogPlayerId p2)
        {
            if (p1 is null)
                return p2 is null;
            return p1.Equals(p2);
        }

        public static bool operator !=(PlogPlayerId p1, PlogPlayerId p2)
        {
            return !(p1 == p2);
        }

        public static PlogPlayerId FromString(string playerId)
        {
            var strings = playerId.Split('_');
            var assignedIdStr = strings[0];
            var guid = Guid.Parse(assignedIdStr);
            return new PlogPlayerId(int.Parse(strings[1]), guid);
        }

        public static void Export(PlogPlayerId player, BinaryWriter w)
        {
            w.Write(_version);
            w.Write(player.gameSeed);
            w.Write(player.assignedId.ToByteArray());
        }

        public static PlogPlayerId Import(BinaryReader reader)
        {
            var ver = reader.ReadInt32();
            if (ver != _version)
            {
                Log.Debug($"reading in a version={ver} playerId from code at version={_version}");
            }

            var seed = reader.ReadInt32();
            byte[] guidBytes = new byte[16];
            reader.Read(guidBytes, 0, guidBytes.Length);
            return new PlogPlayerId(seed, new Guid(guidBytes));
        }
    }
}