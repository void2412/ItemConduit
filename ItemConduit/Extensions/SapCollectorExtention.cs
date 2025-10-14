using ItemConduit.Config;
using ItemConduit.Interfaces;
using ItemConduit.Nodes;
using Jotunn.Configs;
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
	public class SapCollectorExtention : BaseExtension<SapCollector>, IContainerInterface
	{
		
		#region Unity Life Cycle

		private void Update()
		{
			if (component == null) return;
			if (!IsConnected) return;

			int currentSap = component.GetLevel();
			if (currentSap > 0)
			{
				component.m_nview.GetZDO().Set(ZDOVars.s_level, 0, false);
				ItemDrop.ItemData itemData = component.m_spawnItem.m_itemData.Clone();
				itemData.m_dropPrefab = component.m_spawnItem.gameObject;
				itemData.m_stack = currentSap;
				m_container.m_inventory.AddItem(itemData);
				SaveInventoryToZDO();
			}

		}

		#endregion

		#region Connection Management

		public override void OnNodeDisconnected(BaseNode node)
		{
			
			List<ItemDrop.ItemData> itemDatas = m_container.m_inventory.GetAllItems();
			if(!IsConnected && itemDatas.Count > 0)
			{
				foreach (ItemDrop.ItemData itemData in itemDatas) 
				{
					bool isSap = false;
					if(itemData.m_dropPrefab != null && component.m_spawnItem != null)
					{
						isSap = itemData.m_dropPrefab.name == component.m_spawnItem.gameObject.name;
					}

					if (isSap)
					{
						if (component == null || m_container == null) return;

						component?.m_nview?.GetZDO()?.Set(ZDOVars.s_level, itemData.m_stack, false);
					}
					else
					{
						if (component == null || m_container == null) return;
						ItemDrop.DropItem(itemData, 0, component.m_spawnPoint.position, component.m_spawnPoint.rotation);
					}
				}

				m_container.m_inventory.RemoveAll();

				base.OnNodeDisconnected(node);
			}
		}

		#endregion

		#region IContainerInterface Implementation

		public int CalculateAcceptCapacity(ItemDrop.ItemData sourceItem, int desiredAmount) { return 0; }
		public bool CanAddItem(ItemDrop.ItemData item) { return false; }

		public bool CanRemoveItem(ItemDrop.ItemData item)
		{
			if (item?.m_dropPrefab != null && component?.m_spawnItem != null)
			{
				return item.m_dropPrefab.name == component.m_spawnItem.gameObject.name;
			}

			// Fallback to checking shared name
			if (item?.m_shared != null && component?.m_spawnItem?.m_itemData?.m_shared != null)
			{
				return item.m_shared.m_name == component.m_spawnItem.m_itemData.m_shared.m_name;
			}

			return false;
		}

		public bool AddItem(ItemDrop.ItemData item, int amount = 0) { return false; }

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

		public Inventory GetInventory() { return m_container.m_inventory; }

		public string GetName() { return component?.m_name ?? "Sap Collector"; }

		public Vector3 GetTransformPosition() { return component?.transform.position ?? transform.position; }

		#endregion

	}
}
