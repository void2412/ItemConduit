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
	public class FermenterExtension : BaseExtension<Fermenter>, IContainerInterface
	{

		#region Unity Life Cycle

		protected override void Awake()
		{
			m_height = 2;
			m_width = 3;
			base.Awake();
		}

		protected void Update()
		{
			if (IsConnected && component.GetStatus() == Fermenter.Status.Ready)
			{
				Fermenter.ItemConversion itemConversion = component.GetItemConversion(component.GetContent());
				ItemDrop itemDropPrefab = itemConversion.m_to;
				ItemDrop.ItemData itemData = itemDropPrefab.m_itemData.Clone();
				itemData.m_dropPrefab = itemDropPrefab.gameObject;
				itemData.m_stack = itemConversion.m_producedItems;

				if (AddToInventory(itemData))
				{
					component.m_nview.GetZDO().Set(ZDOVars.s_content, "");
					component.m_nview.GetZDO().Set(ZDOVars.s_startTime, 0, false);
				}
			}
			
		}

		#endregion

		#region Connection Management

		public override void OnNodeDisconnected(BaseNode node)
		{
			
			List<ItemDrop.ItemData> itemDatas = m_container.m_inventory.GetAllItems();
			if (!IsConnected && itemDatas.Count > 0)
			{
				foreach(var item in itemDatas)
				{
					ItemDrop.DropItem(item, 0, component.m_outputPoint.position, component.m_outputPoint.rotation);
				}
				m_container.m_inventory.RemoveAll();
			}
			base.OnNodeDisconnected(node);
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
			if (item == null || component == null || m_container == null) return false;

			if (component.GetStatus() == Fermenter.Status.Fermenting) return false;

			return component.IsItemAllowed(item);
		}

		public bool CanRemoveItem(ItemDrop.ItemData item)
		{
			List<Fermenter.ItemConversion> itemConversions = component.m_conversion;
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

			component.m_nview.InvokeRPC("RPC_AddItem", new object[] { item.m_dropPrefab.name });
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
			return component?.m_name ?? "Fermenter";
		}

		public Vector3 GetTransformPosition()
		{
			return component?.transform.position ?? transform.position;
		}

		#endregion
	}
}
