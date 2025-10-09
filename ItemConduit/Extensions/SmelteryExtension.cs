using ItemConduit.Interfaces;
using Jotunn;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ItemConduit.Extensions
{
	public class SmelteryExtension : BaseExtension, IContainerInterface
	{
		public bool isConnected { get; set; } = false;
		private Smelter smelter;
		public Dictionary<string, int> oreProcessedList;

		protected override void Awake()
		{
			base.Awake();
			smelter = GetComponent<Smelter>();
		}
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

			if (desiredAmount > acceptableAmount) return desiredAmount;

			return (int)acceptableAmount;
		}

		public string ItemType(ItemDrop.ItemData item)
		{
			string prefabName = item.m_dropPrefab.name;
			if (smelter.IsItemAllowed(prefabName)) return "Ore";

			string fuelName = smelter.m_fuelItem.m_itemData.m_dropPrefab.name;
			if (prefabName == fuelName) return "Fuel";

			return "Invalid";
		}

		public bool CanAddItem(ItemDrop.ItemData item)
		{
			string prefabName = item.m_dropPrefab.name;
			if (smelter.IsItemAllowed(prefabName)) return true;

			string fuelName = smelter.m_fuelItem.m_itemData.m_dropPrefab.name;
			if (prefabName == fuelName) return true;

			return false;
		}

		public bool CanRemoveItem(ItemDrop.ItemData item)
		{
			if (!isConnected) return false;
			using (List<Smelter.ItemConversion>.Enumerator enumerator = smelter.m_conversion.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					if (enumerator.Current.m_to.gameObject.name == item.m_dropPrefab.name)
					{
						return true;
					}
				}
			}
			return false;
		}

		public bool AddItem(ItemDrop.ItemData item, int amount = 0)
		{
			if (amount <= 0 || item.m_stack <= 0) return false;

			if (amount > 0) { 
				item.m_stack = amount;
			}

			string type = ItemType(item);
			if (type == null || type == "Invalid") return false;

			if (type == "Ore")
			{
				for (int i = 0; i < item.m_stack; i++)
				{
					smelter.QueueOre(item.m_dropPrefab.name);
				}
			}
			
			if (type == "Fuel")
			{
				float fuel = smelter.GetFuel();
				smelter.SetFuel(fuel + item.m_stack);
			}

			return true;
		}

		public bool RemoveItem(ItemDrop.ItemData item, int amount = 0)
		{
			if (amount <= 0 && item.m_stack <= 0) return false;
			if (item== null) return false;

			if (amount > 0) item.m_stack = amount;

			int removeAmount = CalculateRemoveCapacity(item, item.m_stack);
			if (removeAmount > 0) {
				oreProcessedList[item.m_dropPrefab.name] -= removeAmount;
				return true;
			}
			
			return false;
		}

		private int CalculateRemoveCapacity(ItemDrop.ItemData sourceItem, int desiredAmount)
		{
			if (desiredAmount <= 0 && sourceItem.m_stack <= 0) return 0;
			if (sourceItem == null) return 0;
			if (!oreProcessedList.ContainsKey(sourceItem.m_dropPrefab.name)) return 0;

			if (desiredAmount > 0)
			{
				sourceItem.m_stack = desiredAmount;
			}

			if(oreProcessedList.TryGetValue(sourceItem.m_dropPrefab.name, out int itemAmount))
			{
				if (itemAmount <= desiredAmount) return itemAmount;
				if (itemAmount > desiredAmount) return desiredAmount;
			}
			return 0;
		}
		public Inventory GetInventory()
		{
			Inventory inventory = new Inventory("smelterInventory", Jotunn.Managers.GUIManager.Instance.GetSprite("woodpanel_playerinventory"), oreProcessedList.Count(), 1);
			foreach (var item in oreProcessedList)
			{
				Smelter.ItemConversion conversion =  smelter.GetItemConversion(item.Key);
				ItemDrop itemDrop = conversion.m_to;
				ItemDrop.ItemData itemData = itemDrop.m_itemData;
				itemData.m_stack = item.Value;

				inventory.AddItem(itemData);

			}

			return inventory;
		}

		public string GetName()
		{
			return smelter.m_name;
		}

		public UnityEngine.Vector3 GetTransformPosition()
		{
			return smelter.transform.position;
		}
	}
}
