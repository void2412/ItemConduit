using ItemConduit.Config;
using ItemConduit.Interfaces;
using ItemConduit.Nodes;
using Jotunn.Configs;
using PlayFab.EconomyModels;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using static ItemDrop;
using Logger = Jotunn.Logger;

namespace ItemConduit.Extensions
{
	public class CookingStationExtension : BaseExtension<CookingStation>, IContainerInterface
	{
		public HashSet<int> freeSlotList = new HashSet<int>();
		#region Unity Life Cycle
		protected override void Awake()
		{
			m_width = 3;
			m_height = 2;
			base.Awake();
			GetFreeSlotList();
		}

		#endregion

		#region Connection Management
		public override void OnNodeConnected(BaseNode node)
		{
			base.OnNodeConnected(node);
			GetFreeSlotList();
			
			if (component != null && IsConnected)
			{
				component.UpdateCooking();
			}
		}
		public override void OnNodeDisconnected(BaseNode node)
		{
			
			List<ItemDrop.ItemData> itemDatas = m_container.m_inventory.GetAllItems();
			if (!IsConnected && itemDatas.Count > 0)
			{
				foreach(var item in itemDatas)
				{
					ItemDrop.DropItem(item, 0, component.m_slots[0].position, component.m_slots[0].rotation);
				}
				m_container.m_inventory.RemoveAll();
			}
			base.OnNodeDisconnected(node);
		}
		#endregion

		#region IContainerInterface Implementation

		public void GetFreeSlotList()
		{
			if (m_container == null) return;

			for (int i = 0; i < component.m_slots.Length; i++)
			{
				CookingStation.Status status;
				string itemName;
				float cookedTime;
				component.GetSlot(i, out itemName, out cookedTime, out status);
				if(string.IsNullOrEmpty(itemName))
				{
					freeSlotList.Add(i);
				}
			}
		}

		public int CalculateAcceptCapacity(ItemDrop.ItemData item, int amount)
		{
			if (item == null) return 0;
			if (freeSlotList.Count == 0) return 0;

			// Determine the actual amount to check
			int checkAmount = (amount > 0) ? amount : item.m_stack;

			if (checkAmount <= 0) return 0;

			// Return the minimum of requested amount and available slots
			int acceptableAmount = Math.Min(checkAmount, freeSlotList.Count);

			return acceptableAmount;
		}

		public bool CanAddItem(ItemDrop.ItemData item)
		{
			if (item == null) return false;
			if (!component.IsItemAllowed(item)) return false;

			return true;
		}

		public bool CanRemoveItem(ItemDrop.ItemData item) 
		{
			if (item == null) return false;
			if (!IsItemAllowedRemove(item)) return false;

			return true;
		}

		private bool IsItemAllowedRemove(ItemDrop.ItemData item)
		{
			if (item == null) return false;
			if(component == null) return false;

			string itemName = item.m_dropPrefab.name;

			string burntItemName = component?.m_overCookedItem?.m_itemData?.m_dropPrefab?.name;
			if (burntItemName != null && itemName == burntItemName)
			{
				return true;
			}

			using (List<CookingStation.ItemConversion>.Enumerator enumerator = component.m_conversion.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					if (enumerator.Current.m_to.gameObject.name == itemName)
					{
						return true;
					}
				}
			}

			return false;
		}


		public bool AddItem(ItemDrop.ItemData item, int amount = 0)
		{
			if (!CanAddItem(item)) return false;

			// Determine how many to add
			int amountToAdd = (amount > 0) ? amount : item.m_stack;

			int addableAmount = CalculateAcceptCapacity(item, amountToAdd);
			if (addableAmount <= 0) return false;

			// Create a list of slots to use (don't modify collection during iteration)
			List<int> slotsToUse = freeSlotList.Take(addableAmount).ToList();

			// Add items to cooking slots
			foreach (var slotNumber in slotsToUse)
			{
				component.SetSlot(slotNumber, item.m_dropPrefab.name, 0f, CookingStation.Status.NotDone);
			}

			// Remove used slots from freeSlotList (after iteration)
			foreach (var slotNumber in slotsToUse)
			{
				freeSlotList.Remove(slotNumber);
			}

			// DO NOT modify item.m_stack - NetworkManager handles this!
			return true;
		}

		public bool AddToInventory(ItemData item, int amount = 0)
		{
			if (item == null) return false;
			if (amount <= 0 && item.m_stack <= 0) return false;
			if (!m_container.m_inventory.CanAddItem(item)) return false;

			if (amount > 0 && item.m_stack != amount)
			{
				item.m_stack = amount;
			}

			if (m_container.m_inventory.AddItem(item))
			{
				SaveInventoryToZDO();
				return true;
			}

			return false;
		}

		public bool RemoveItem(ItemDrop.ItemData item, int amount = 0) 
		{
			if (item == null) return false;
			if (amount <= 0 && item.m_stack <= 0) return false;

			if (m_container.m_inventory.RemoveItem(item, amount))
			{
				SaveInventoryToZDO();
				return true;
			}

			return false;
		}

		public Inventory GetInventory() { return m_container?.m_inventory; }

		public string GetName() { return component?.m_name ?? "Cooking Station"; }

		public Vector3 GetTransformPosition() { return component?.transform.position ?? transform.position; }


		#endregion
	}
}
