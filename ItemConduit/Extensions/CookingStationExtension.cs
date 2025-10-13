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
	public class CookingStationExtension : BaseExtension, IContainerInterface
	{
		private CookingStation cookingStation;
		public Container m_container;

		#region Unity Life Cycle
		protected override void Awake()
		{
			base.Awake();

			cookingStation = GetComponentInParent<CookingStation>();
			if (cookingStation == null)
			{
				cookingStation = GetComponent<CookingStation>();
				if (cookingStation == null) cookingStation = GetComponentInChildren<CookingStation>();
			}

			if (cookingStation == null)
			{
				Logger.LogError($"[ItemConduit] SmelteryExtension could not find Cooking Station component!");
				return;
			}

			ZNetView znetView = cookingStation.GetComponent<ZNetView>();
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
			m_container = cookingStation.gameObject.AddComponent<Container>();
			m_container.m_width = 1;
			m_container.m_height = 1;
			m_container.m_inventory = new Inventory("Cooking Station Output", null, 1, 1);
			m_container.name = "Cooking Station Output";
		}

		private void SaveInventoryToZDO()
		{
			if (cookingStation == null || m_container == null) return;

			ZNetView znetView = cookingStation.GetComponent<ZNetView>();
			if (znetView == null || !znetView.IsValid()) return;

			ZDO zdo = znetView.GetZDO();
			if (zdo == null) return;

			// Save inventory as a ZPackage
			ZPackage pkg = new ZPackage();
			m_container.m_inventory.Save(pkg);
			zdo.Set("ItemConduit_Inventory", pkg.GetBase64());
		}

		private void LoadInventoryFromZDO()
		{
			if (cookingStation == null || m_container == null) return;

			ZNetView znetView = cookingStation.GetComponent<ZNetView>();
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
				foreach(var item in itemDatas)
				{
					ItemDrop.DropItem(item, 0, cookingStation.m_slots[0].position, cookingStation.m_slots[0].rotation);
				}
				m_container.m_inventory.RemoveAll();
				SaveInventoryToZDO();
			}
		}
		#endregion

		#region IContainerInterface Implementation

		public int CalculateAcceptCapacity(ItemDrop.ItemData item, int amount)
		{
			return 0;
		}

		public bool CanAddItem(ItemDrop.ItemData item)
		{
			return false;
		}

		public bool CanRemoveItem(ItemDrop.ItemData item) 
		{
			return false;
		}

		public bool AddItem(ItemDrop.ItemData item, int amount = 0)
		{
			return false;
		}

		public bool AddToInventory(ItemData item, int amount = 0)
		{
			return false;
		}

		public bool RemoveItem(ItemDrop.ItemData item, int amount = 0) 
		{
			return false;		
		}

		public Inventory GetInventory() { return m_container?.m_inventory; }

		public string GetName() { return cookingStation?.m_name ?? "Cooking Station"; }

		public Vector3 GetTransformPosition() { return cookingStation?.transform.position ?? transform.position; }


		#endregion
	}
}
