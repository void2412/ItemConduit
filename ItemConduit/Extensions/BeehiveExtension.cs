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
	public class BeehiveExtension : BaseExtension, IContainerInterface
	{
		private Beehive beehive;
		public Container m_container;


		#region Unity Life Cycle
		protected override void Awake()
		{
			base.Awake();
			beehive = GetComponentInParent<Beehive>();
			if (beehive == null)
			{
				beehive = GetComponent<Beehive>();
				if (beehive == null)
				{
					beehive = GetComponentInChildren<Beehive>();
				}
				else
				{
					Logger.LogError($"[ItemConduit] BeehiveExtension could not find Beehive component!");
					return;
				}
			}

			ZNetView znetView = beehive.GetComponent<ZNetView>();
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
			if (beehive == null) return;
			if (!IsConnected) return;

			int currentHoney = beehive.GetHoneyLevel();
			if (currentHoney > 0)
			{
				beehive.m_nview.GetZDO().Set(ZDOVars.s_level, 0, false);
				ItemDrop.ItemData itemData = beehive.m_honeyItem.m_itemData.Clone();
				itemData.m_dropPrefab = beehive.m_honeyItem.gameObject;
				itemData.m_stack = currentHoney;
				m_container.m_inventory.AddItem(itemData);
				SaveInventoryToZDO();
			}
		}

		#endregion

		#region Container

		private void SetupContainer()
		{
			m_container = beehive.gameObject.AddComponent<Container>();
			m_container.m_width = 1;
			m_container.m_height = 1;
			m_container.m_inventory = new Inventory("Beehive Output", null, 1, 1);
			m_container.name = "Beehive Output";
		}

		public void SaveInventoryToZDO()
		{
			if (beehive == null || m_container == null) return;

			ZNetView znetView = beehive.GetComponent<ZNetView>();
			if (znetView == null || !znetView.IsValid()) return;

			ZDO zdo = znetView.GetZDO();
			if (zdo == null) return;

			// Save inventory as a ZPackage
			ZPackage pkg = new ZPackage();
			m_container.m_inventory.Save(pkg);
			zdo.Set("ItemConduit_Inventory", pkg.GetBase64());
		}

		public void LoadInventoryFromZDO()
		{
			if (beehive == null || m_container == null) return;

			ZNetView znetView = beehive.GetComponent<ZNetView>();
			if (znetView == null || !znetView.IsValid()) return;

			ZDO zdo = znetView.GetZDO();
			if (zdo == null) return;

			string data = zdo.GetString("ItemConduit_Inventory", "");
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
			if (!IsConnected && itemDatas.Count > 0)
			{
				foreach (ItemDrop.ItemData itemData in itemDatas)
				{
					// More defensive check
					bool isHoney = false;
					if (itemData.m_dropPrefab != null && beehive.m_honeyItem != null)
					{
						isHoney = itemData.m_dropPrefab.name == beehive.m_honeyItem.gameObject.name;
					}
					else if (itemData.m_shared != null && beehive.m_honeyItem?.m_itemData?.m_shared != null)
					{
						isHoney = itemData.m_shared.m_name == beehive.m_honeyItem.m_itemData.m_shared.m_name;
					}

					if (isHoney)
					{
						if (beehive == null || m_container == null) return;
						
						beehive.m_nview?.GetZDO()?.Set(ZDOVars.s_level, itemData.m_stack, false);
					}
					else
					{
						if (beehive == null || m_container == null) return;
						ItemDrop.DropItem(itemData, 0, beehive.m_spawnPoint.position, beehive.m_spawnPoint.rotation);
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
			if (item?.m_dropPrefab != null && beehive?.m_honeyItem != null)
			{
				return item.m_dropPrefab.name == beehive.m_honeyItem.gameObject.name;
			}

			// Fallback to checking shared name
			if (item?.m_shared != null && beehive?.m_honeyItem?.m_itemData?.m_shared != null)
			{
				return item.m_shared.m_name == beehive.m_honeyItem.m_itemData.m_shared.m_name;
			}

			return false; 
		}
		public bool AddItem(ItemDrop.ItemData item, int amount = 0) { return false; }
		public bool RemoveItem(ItemDrop.ItemData item, int amount = 0)
		{
			if(item==null) return false;
			if(amount <=0 && item.m_stack <= 0) return false;

			if (m_container.m_inventory.RemoveItem(item, amount))
			{
				SaveInventoryToZDO();
				return true;
			}

			return false;
		}

		public Inventory GetInventory() { return m_container.m_inventory; }
		public string GetName() { return beehive?.m_name ?? "Beehive"; }

		public Vector3 GetTransformPosition() { return beehive?.transform.position ?? transform.position; }

		#endregion

	}
}
