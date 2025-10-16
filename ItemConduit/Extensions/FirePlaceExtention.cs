using ItemConduit.Config;
using ItemConduit.Interfaces;
using ItemConduit.Nodes;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static ItemDrop;
using Logger = Jotunn.Logger;

namespace ItemConduit.Extensions
{
	public class FirePlaceExtention : BaseExtension<Fireplace>, IContainerInterface
	{
		private bool isInitialized = false;
		private bool isSyncing = false;


		#region Unity Life Cycle

		protected override void Awake()
		{
			m_width = 4;
			base.Awake();
		}

		protected override void Start()
		{
			base.Start();
			EnsureContainerInitialized();
		}

		#endregion

		#region Inventory Callbacks - Automatic Sync

		protected override void OnItemAdded(ItemDrop.ItemData item)
		{
			base.OnItemAdded(item);  // Saves to ZDO

			// Prevent circular sync
			if (isSyncing) return;

			// Only sync fuel items
			if (!IsFuelItem(item)) return;

			// Add fuel to fireplace's internal counter
			float currentFuel = component.m_nview.GetZDO().GetFloat(ZDOVars.s_fuel, 0f);
			float newFuel = currentFuel + item.m_stack;

			component.m_nview.GetZDO().Set(ZDOVars.s_fuel, newFuel);
			component.UpdateState();

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo($"[FirePlaceExtention] Added {item.m_stack} fuel. Total: {newFuel}");
			}
		}

		protected override void OnItemRemoved(ItemDrop.ItemData item, int amount)
		{
			base.OnItemRemoved(item, amount);  // Saves to ZDO

			// Prevent circular sync
			if (isSyncing) return;


			// Only sync fuel items
			if (!IsFuelItem(item)) return;

			// Remove fuel from fireplace's internal counter
			float currentFuel = component.m_nview.GetZDO().GetFloat(ZDOVars.s_fuel, 0f);
			float newFuel = Mathf.Max(0f, currentFuel - amount);

			component.m_nview.GetZDO().Set(ZDOVars.s_fuel, newFuel);
			component.UpdateState();

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo($"[FirePlaceExtention] Removed {amount} fuel. Total: {newFuel}");
			}
		}

		#endregion

		#region Fireplace to Inventory Sync

		/// <summary>
		/// Sync fireplace fuel consumption back to inventory
		/// Called when fireplace burns fuel internally
		/// </summary>
		public void SyncFireplaceToInventory()
		{
			if (component == null || m_container == null) return;

			isSyncing = true;  // Prevent OnItemRemoved from triggering

			float currentFuel = component.m_nview.GetZDO().GetFloat(ZDOVars.s_fuel, 0f);
			int fuelToShow = Mathf.FloorToInt(currentFuel);

			// Clear all fuel items
			var allItems = m_container.m_inventory.GetAllItems();
			var fuelItems = allItems.Where(item => IsFuelItem(item)).ToList();

			foreach (var item in fuelItems)
			{
				m_container.m_inventory.RemoveItem(item);
			}

			// Add current fuel amount
			if (fuelToShow > 0)
			{
				ItemDrop.ItemData itemData = component.m_fuelItem.m_itemData.Clone();
				itemData.m_dropPrefab = component.m_fuelItem.gameObject;
				itemData.m_stack = fuelToShow;
				m_container.m_inventory.AddItem(itemData);
			}

			SaveInventoryToZDO();

			isSyncing = false;
		}

		#endregion

		#region Helper Methods

		private bool IsFuelItem(ItemDrop.ItemData item)
		{
			if (item == null || component == null || component.m_fuelItem == null)
				return false;

			return item.m_dropPrefab?.name == component.m_fuelItem.gameObject?.name;
		}

		private bool EnsureContainerInitialized()
		{
			if (isInitialized && m_container != null) return true;

			if (component == null)
			{
				component = GetComponent<Fireplace>();
				if (component == null) return false;
			}

			if (m_container == null)
			{
				zNetView = component.GetComponent<ZNetView>();
				if (zNetView == null || !zNetView.IsValid()) return false;

				SetupContainer(m_width, m_height);

				if (m_container != null)
				{
					LoadInventoryFromZDO();

					// Initial sync
					float currentFuel = component.m_nview.GetZDO().GetFloat(ZDOVars.s_fuel, 0f);
					SyncFireplaceToInventory();

					isInitialized = true;
				}
			}

			return m_container != null;
		}

		#endregion

		#region Connection Management

		public override void OnNodeDisconnected(BaseNode node)
		{
			base.OnNodeDisconnected(node);

			// When ALL nodes disconnect, only drop non-fuel items
			if (!IsConnected && m_container != null)
			{
				List<ItemDrop.ItemData> itemDatas = m_container.m_inventory.GetAllItems();

				if (itemDatas.Count > 0)
				{
					List<ItemDrop.ItemData> itemsToRemove = new List<ItemDrop.ItemData>();

					// Find non-fuel items
					foreach (ItemDrop.ItemData itemData in itemDatas)
					{
						if (!IsFuelItem(itemData))
						{
							itemsToRemove.Add(itemData);
						}
					}

					// Drop and remove non-fuel items
					foreach (var item in itemsToRemove)
					{
						if (component == null || m_container == null) return;

						ItemDrop.DropItem(item, item.m_stack, component.transform.position + Vector3.up, component.transform.rotation);
						m_container.m_inventory.RemoveItem(item);
					}

					if (itemsToRemove.Count > 0)
					{
						SaveInventoryToZDO();
					}
				}
			}
		}

		#endregion

		#region IContainerInterface Implementation
		public int CalculateAcceptCapacity(ItemDrop.ItemData sourceItem, int desiredAmount)
		{
			if (sourceItem == null || component == null) return 0;

			int actualAmount = desiredAmount > 0 ? desiredAmount : sourceItem.m_stack;
			if (actualAmount <= 0) return 0;

			var containerExt = m_container.GetComponent<StandardContainerExtension>();
			if (containerExt == null) return 0;

			int addableAmount = containerExt.CalculateAcceptCapacity(sourceItem, actualAmount);
			if (addableAmount <= 0) addableAmount = 0;

			return addableAmount;

		}
		public bool CanAddItem(ItemDrop.ItemData item)
		{
			if (item == null || component == null) return false;

			return IsFuelItem(item);

		}
		public bool CanRemoveItem(ItemDrop.ItemData item)
		{
			if (item == null || component == null) return false;
			return IsFuelItem(item);
		}
		public bool AddItem(ItemDrop.ItemData item, int amount = 0)
		{
			if (item == null || component == null) return false;
			if(!CanAddItem(item)) return false;
			if (component.m_infiniteFuel) return false;

			int actualAmount = amount > 0 ? amount : item.m_stack;
			if (actualAmount <= 0) return false;

			int addableAmount = CalculateAcceptCapacity(item, actualAmount);
			if (addableAmount <= 0) return false;

			ItemDrop.ItemData itemToAdd = item.Clone();
			itemToAdd.m_stack = addableAmount;

			return m_container.m_inventory.AddItem(itemToAdd);
		}
		public bool RemoveItem(ItemDrop.ItemData item, int amount = 0)
		{
			if (item == null || component == null) return false;
			if (!IsFuelItem(item)) return false;

			int actualAmount = amount > 0 ? amount : item.m_stack;

			// Just remove from inventory - OnItemRemoved will handle sync automatically!
			return m_container.m_inventory.RemoveItem(item, actualAmount);
		}
		public Inventory GetInventory()
		{
			return m_container?.m_inventory;
		}
		public string GetName()
		{
			return component?.m_name ?? "Fireplace";
		}

		public UnityEngine.Vector3 GetTransformPosition()
		{
			return component?.transform.position ?? transform.position;
		}

		#endregion

	}
}
