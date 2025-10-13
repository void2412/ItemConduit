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
					Logger.LogError($"[ItemConduit] SmelteryExtension could not find Smelter component!");
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

		private void SaveInventoryToZDO()
		{
			if (beehive == null || m_container == null) return;

			ZNetView znetView = beehive.GetComponent<ZNetView>();
			if (znetView == null || !znetView.IsValid()) return;

			ZDO zdo = znetView.GetZDO();
			if (zdo == null) return;

			// Save inventory as a ZPackage
			ZPackage pkg = new ZPackage();
			m_container.m_inventory.Save(pkg);
			zdo.Set("ItemConduit_SmelterInventory", pkg.GetBase64());
		}

		private void LoadInventoryFromZDO()
		{
			if (beehive == null || m_container == null) return;

			ZNetView znetView = beehive.GetComponent<ZNetView>();
			if (znetView == null || !znetView.IsValid()) return;

			ZDO zdo = znetView.GetZDO();
			if (zdo == null) return;

			string data = zdo.GetString("ItemConduit_SmelterInventory", "");
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
				foreach(ItemDrop.ItemData itemData in itemDatas)
				{
					if (itemData.m_dropPrefab.name == beehive.m_honeyItem.m_itemData.m_dropPrefab.name)
					{
						beehive.m_nview.GetZDO().Set(ZDOVars.s_level, itemData.m_stack, false);
					}
				}	
			}

		}

		#endregion

		#region IContainerInterface Implementation

		public int CalculateAcceptCapacity(ItemDrop.ItemData sourceItem, int desiredAmount) { return 0; }
		public bool CanAddItem(ItemDrop.ItemData item) { return false; }
		public bool CanRemoveItem(ItemDrop.ItemData item) 
		{
			if (item.m_dropPrefab.name == beehive.m_honeyItem.m_itemData.m_dropPrefab.name) return true;

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
