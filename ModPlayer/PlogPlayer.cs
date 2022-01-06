using System;
using PersonalLogistics.PlayerInventory;
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
        protected readonly Player _localPlayer;
        public PersonalLogisticManager personalLogisticManager;
        public ShippingManager shippingManager;
        public InventoryManager inventoryManager;
        public readonly bool isRemote;

        public PlogPlayer(PlogPlayerId playerId, bool isRemote, Player player = null)
        {
            this.playerId = playerId;
            _localPlayer = player;
            personalLogisticManager = new PersonalLogisticManager(player, playerId);
            shippingManager = new ShippingManager(this);
            inventoryManager = new InventoryManager(this);
            this.isRemote = isRemote;
        }

        public abstract PlogPlayerPosition GetPosition();

        public abstract int PackageSize();

        public override string ToString()
        {
            return $"PLOGPlayer: {playerId}, isRemote: {isRemote}";
        }

        public abstract float QueryEnergy(long valueEnergyCost);

        public abstract void UseEnergy(float energyToUse, int type);
    }

    public class PlogPlayerId : IComparable<PlogPlayerId>, IEquatable<PlogPlayerId>
    {
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
            return new PlogPlayerId(GameUtil.GetSeedInt(), CryptoUtils.GetCurrentUserAndGameIdentifierGuid());
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
    }
}