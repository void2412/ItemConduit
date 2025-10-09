using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ItemConduit.Interfaces
{
	
    /// <summary>
    /// Interface for interacting with different container types in Valheim
    /// </summary>
    public interface IContainerInterface
	{
		int CalculateAcceptCapacity(ItemDrop.ItemData sourceItem, int desiredAmount);
		bool CanAddItem(ItemDrop.ItemData item);
		bool CanRemoveItem(ItemDrop.ItemData item);
		bool AddItem(ItemDrop.ItemData item, int amount = 0);
		bool RemoveItem(ItemDrop.ItemData item, int amount = 1);
		Inventory GetInventory();
		string GetName();

		UnityEngine.Vector3 GetTransformPosition();
	}

}

