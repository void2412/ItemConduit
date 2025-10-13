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
	public class SapCollectorExtention : BaseExtension, IContainerInterface
	{
		private SapCollector sapCollector;
		public Container m_container;


		#region Unity Life Cycle
		protected override void Awake()
		{
			base.Awake();

			sapCollector = GetComponentInParent<SapCollector>();
			if (sapCollector == null)
			{
				sapCollector = GetComponent<SapCollector>();
				if (sapCollector == null)
				{
					sapCollector = GetComponentInChildren<SapCollector>();
				}
				else
				{
					Logger.LogError($"[ItemConduit] SapCollectorExtention could not find SapCollector component!");
					return;
				}
			}

			ZNetView znetView = sapCollector.GetComponent<ZNetView>();
			if (znetView == null || !znetView.IsValid())
			{
				if (DebugConfig.showDebug.Value)
				{
					Logger.LogInfo($"[ItemConduit] Skipping container creation - invalid ZNetView");
				}
				return;
			}

			SetupContainer();
			LoadInventoryFromZDO();
		}

		protected override void OnDestroy()
		{
			SaveInventoryToZDO();
			base.OnDestroy();
		}

		private void Update()
		{
			if (sapCollector == null) return;
			if (!IsConnected) return;

			int currentSap = sapCollector.GetLevel();
			if (currentSap > 0)
			{
				sapCollector.m_nview.GetZDO().Set(ZDOVars.s_level, 0, false);
				ItemDrop.ItemData itemData = sapCollector.m_spawnItem.m_itemData.Clone();
				itemData.m_dropPrefab = sapCollector.m_spawnItem.gameObject;
				itemData.m_stack = currentSap;
				m_container.m_inventory.AddItem(itemData);
				SaveInventoryToZDO();
			}

		}

		#endregion

		#region Container
		private void SetupContainer()
		{
			m_container = sapCollector.gameObject.AddComponent<Container>();
			m_container.m_width = 1;
			m_container.m_height = 1;
			m_container.m_inventory = new Inventory("Sap Collector Output", null, 1, 1);
			m_container.name = "Sap Collector Output";
		}

		public void SaveInventoryToZDO()
		{
			if (sapCollector == null || m_container == null) return;

			ZNetView znetView = sapCollector.GetComponent<ZNetView>();
			if (znetView == null || !znetView.IsValid()) return;

			ZDO zdo = znetView.GetZDO();
			if (zdo == null) return;

			// Save inventory as a ZPackage
			ZPackage pkg = new ZPackage();
			m_container.m_inventory.Save(pkg);
			zdo.Set("ItemConduit_SapCollectorInventory", pkg.GetBase64());
		}

		public void LoadInventoryFromZDO()
		{
			if (sapCollector == null || m_container == null) return;

			ZNetView znetView = sapCollector.GetComponent<ZNetView>();
			if (znetView == null || !znetView.IsValid()) return;

			ZDO zdo = znetView.GetZDO();
			if (zdo == null) return;

			string data = zdo.GetString("ItemConduit_SapCollectorInventory", "");
			if (!string.IsNullOrEmpty(data))
			{
				ZPackage pkg = new ZPackage(data);
				m_container.m_inventory.Load(pkg);
			}
		}

		#endregion

		#region Connection Management

		public override void OnNodeDisconnected(BaseNode node)
		{
			base.OnNodeDisconnected(node);
			List<ItemDrop.ItemData> itemDatas = m_container.m_inventory.GetAllItems();
			if(!IsConnected && itemDatas.Count > 0)
			{
				foreach (ItemDrop.ItemData itemData in itemDatas) 
				{
					bool isSap = false;
					if(itemData.m_dropPrefab != null && sapCollector.m_spawnItem != null)
					{
						isSap = itemData.m_dropPrefab.name == sapCollector.m_spawnItem.gameObject.name;
					}

					if (isSap)
					{
						if (sapCollector == null || m_container == null) return;

						sapCollector?.m_nview?.GetZDO()?.Set(ZDOVars.s_level, itemData.m_stack, false);
					}
					else
					{
						if (sapCollector == null || m_container == null) return;
						ItemDrop.DropItem(itemData, 0, sapCollector.m_spawnPoint.position, sapCollector.m_spawnPoint.rotation);
					}
				}

				m_container.m_inventory.RemoveAll();
				SaveInventoryToZDO();
			}
		}

		#endregion

		#region IContainerInterface Implementation

		public int CalculateAcceptCapacity(ItemDrop.ItemData sourceItem, int desiredAmount) { return 0; }
		public bool CanAddItem(ItemDrop.ItemData item) { return false; }

		public bool CanRemoveItem(ItemDrop.ItemData item)
		{
			if (item?.m_dropPrefab != null && sapCollector?.m_spawnItem != null)
			{
				return item.m_dropPrefab.name == sapCollector.m_spawnItem.gameObject.name;
			}

			// Fallback to checking shared name
			if (item?.m_shared != null && sapCollector?.m_spawnItem?.m_itemData?.m_shared != null)
			{
				return item.m_shared.m_name == sapCollector.m_spawnItem.m_itemData.m_shared.m_name;
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

		public string GetName() { return sapCollector?.m_name ?? "Sap Collector"; }

		public Vector3 GetTransformPosition() { return sapCollector?.transform.position ?? transform.position; }

		#endregion

	}
}
