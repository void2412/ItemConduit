using ItemConduit.Interfaces;
using ItemConduit.Nodes;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace ItemConduit.Extensions
{
	/// <summary>
	/// Extension for Smelter objects with node notification
	/// </summary>
	public class SmelteryExtension : BaseExtension, IContainerInterface
	{
		private Smelter smelter;
		public Dictionary<string, int> oreProcessedList = new Dictionary<string, int>();

		protected override void Awake()
		{
			base.Awake();
			smelter = GetComponent<Smelter>();
		}

		public override void OnNodeDisconnected(BaseNode node)
		{
			base.OnNodeDisconnected(node);

			// If no more nodes are connected and we have processed ore, spawn them
			if (!IsConnected && oreProcessedList.Count > 0)
			{
				foreach (var item in oreProcessedList.ToList())
				{
					smelter.Spawn(item.Key, item.Value);
					oreProcessedList.Remove(item.Key);
				}
			}
		}

		#region IContainerInterface Implementation

		public int CalculateAcceptCapacity(ItemDrop.ItemData sourceItem, int desiredAmount)
		{
			if (sourceItem == null || desiredAmount <= 0) return 0;
			if (!CanAddItem(sourceItem)) return 0;

			string type = ItemType(sourceItem);
			if (type == null || type == "Invalid") return 0;

			float acceptableAmount = 0;
			if (type == "Fuel")
			{
				var maxFuel = smelter.m_maxFuel;
				var currentFuel = smelter.GetFuel();
				acceptableAmount = maxFuel - currentFuel;
			}

			if (type == "Ore")
			{
				var maxOre = smelter.m_maxOre;
				var currentOreSize = smelter.GetQueueSize();
				acceptableAmount = maxOre - currentOreSize;
			}

			if (acceptableAmount <= 0) return 0;

			return Mathf.Min(desiredAmount, (int)acceptableAmount);
		}

		public bool CanAddItem(ItemDrop.ItemData item)
		{
			if (smelter == null) return false;
			string type = ItemType(item);
			return type == "Fuel" || type == "Ore";
		}

		public bool CanRemoveItem(ItemDrop.ItemData item)
		{
			// Smelters typically don't allow direct item removal
			return false;
		}

		public bool AddItem(ItemDrop.ItemData item, int amount = 0)
		{
			

			return false;
		}

		public bool RemoveItem(ItemDrop.ItemData item, int amount = 0)
		{
			// Smelters don't support direct item removal
			return false;
		}

		public Inventory GetInventory()
		{
			// Smelters don't have a traditional inventory
			return null;
		}

		public string GetName()
		{
			return smelter?.m_name ?? "Smelter";
		}

		public Vector3 GetTransformPosition()
		{
			return transform.position;
		}

		private string ItemType(ItemDrop.ItemData item)
		{
			if (item == null) return "Invalid";

			// Check if it's fuel
			if (smelter.m_fuelItem.m_itemData.m_dropPrefab.name == item.m_dropPrefab.name)
			{
				return "Fuel";
			}

			// Check if it's a valid ore for conversion
			foreach (var conversion in smelter.m_conversion)
			{
				if (conversion.m_from.m_itemData.m_dropPrefab.name == item.m_dropPrefab.name)
				{
					return "Ore";
				}
			}

			return "Invalid";
		}

		#endregion
	}
}