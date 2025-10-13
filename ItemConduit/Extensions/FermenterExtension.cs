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
	public class FermenterExtension : BaseExtension, IContainerInterface
	{
		private Fermenter fermenter;
		public Container m_container;

		#region Unity Life Cycle

		protected override void Awake()
		{
			base.Awake();
			fermenter = GetComponentInParent<Fermenter>();
			if ( fermenter == null)
			{
				fermenter = GetComponent<Fermenter>();
				if ( fermenter == null)
				{
					fermenter = GetComponentInChildren<Fermenter>();
				}
				else
				{
					Logger.LogError($"ItemConduit] FermenterExtension could not find Fermenter component!");
					return;
				}
			}

			ZNetView zNetView = fermenter.GetComponent<ZNetView>();
			if(zNetView == null || !zNetView.IsValid())
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

		#endregion

		#region Container

		private void SetupContainer()
		{
			m_container = fermenter.gameObject.AddComponent<Container>();
			m_container.m_width = 3;
			m_container.m_height = 2;
			m_container.m_inventory = new Inventory("Fermenter Output", null, 3, 2);
			m_container.name = "Fermenter Output";
		}

		public void SaveInventoryToZDO()
		{
			if (fermenter == null || m_container == null) return;

			ZNetView znetView = fermenter.GetComponent<ZNetView>();
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
			if (fermenter == null || m_container == null) return;

			ZNetView znetView = fermenter.GetComponent<ZNetView>();
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
					ItemDrop.DropItem(item, 0, fermenter.m_outputPoint.position, fermenter.m_outputPoint.rotation);
				}
				m_container.m_inventory.RemoveAll();
				SaveInventoryToZDO();
			}

		}

		#endregion

		#region IContainerInterface Implementation

		public int CalculateAcceptCapacity(ItemDrop.ItemData item, int amount)
		{
			if (!CanAddItem(item)) return 0;

			return 1;
		}

		public bool CanAddItem(ItemDrop.ItemData item)
		{
			if (item == null || fermenter == null || m_container == null) return false;

			if (fermenter.GetStatus() == Fermenter.Status.Fermenting) return false;

			return fermenter.IsItemAllowed(item);
		}

		public bool CanRemoveItem(ItemDrop.ItemData item)
		{
			List<Fermenter.ItemConversion> itemConversions = fermenter.m_conversion;
			foreach (var itemConversion in itemConversions)
			{
				if(itemConversion.m_to.m_itemData.m_dropPrefab.name == item.m_dropPrefab.name)
				{
					return true;
				}
			}
			return false;
		}

		public bool AddItem(ItemDrop.ItemData item, int amount = 0)
		{
			if (item == null) return false;
			if(!CanAddItem(item)) return false;

			int addableAmount = CalculateAcceptCapacity(item, amount);
			if (addableAmount <= 0) return false;

			fermenter.m_nview.InvokeRPC("RPC_AddItem", new object[] { item.m_dropPrefab.name });
			return true;
		}

		public bool AddToInventory(ItemDrop.ItemData item, int amount = 0)
		{
			if (item == null) return false;
			if(amount <= 0 && item.m_stack <= 0) return false;
			if(!m_container.m_inventory.CanAddItem(item)) return false;

			if (amount > 0 )
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
			if(item == null) return false;
			if (amount <= 0 && item.m_stack <= 0) return false;

			if(m_container.m_inventory.RemoveItem(item, amount))
			{
				SaveInventoryToZDO();
				return true;
			}

			return false;
		}

		public Inventory GetInventory() { return m_container?.m_inventory; }

		public string GetName()
		{
			return fermenter?.m_name ?? "Fermenter";
		}

		public Vector3 GetTransformPosition()
		{
			return fermenter?.transform.position ?? transform.position;
		}

		#endregion
	}
}
