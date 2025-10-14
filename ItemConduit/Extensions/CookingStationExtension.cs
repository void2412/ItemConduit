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
		public List<int> freeSlotList = new List<int>();
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
			GetFreeSlotList();
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
		public override void OnNodeConnected(BaseNode node)
		{
			GetFreeSlotList();
			base.OnNodeConnected(node);
		}
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

		public void GetFreeSlotList()
		{
			if (m_container == null) return;

			for (int i = 0; i < cookingStation.m_slots.Length; i++)
			{
				CookingStation.Status status;
				string itemName;
				float cookedTime;
				cookingStation.GetSlot(i, out itemName, out cookedTime, out status);
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
			if(amount <= 0 && item.m_stack <= 0) return 0;

			if(amount >0) item.m_stack = amount;

			int acceptableAmount = 0;

			acceptableAmount = Math.Min(item.m_stack, freeSlotList.Count);

			return acceptableAmount;
		}

		public bool CanAddItem(ItemDrop.ItemData item)
		{
			if (item == null) return false;
			if (!cookingStation.IsItemAllowed(item)) return false;

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
			if(cookingStation == null) return false;

			string itemName = item.m_dropPrefab.name;

			string burntItemName = cookingStation?.m_overCookedItem?.m_itemData?.m_dropPrefab?.name;
			if (burntItemName != null && itemName == burntItemName)
			{
				return true;
			}

			using (List<CookingStation.ItemConversion>.Enumerator enumerator = cookingStation.m_conversion.GetEnumerator())
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
			if(!CanAddItem(item)) return false;
			int addableAmount = CalculateAcceptCapacity(item, amount);
			if (addableAmount <= 0) return false;


			for (int i = 0; i < addableAmount; i++)
			{
				int slotNumber = freeSlotList[i];
				cookingStation.SetSlot(slotNumber, item.m_dropPrefab.name, 0f, CookingStation.Status.NotDone);
				freeSlotList.RemoveAt(i);
			}

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

			if (m_container.m_inventory.CanAddItem(item))
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

		public string GetName() { return cookingStation?.m_name ?? "Cooking Station"; }

		public Vector3 GetTransformPosition() { return cookingStation?.transform.position ?? transform.position; }


		#endregion
	}
}
