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

namespace ItemConduit.Extensions
{
	public class FirePlaceExtention : BaseExtension<Fireplace>, IContainerInterface
	{
		private int lastFuel = 0;

		#region Unity Life Cycle

		protected void Update()
		{
			if (!IsConnected) return;
			float currentFuel = component.m_nview.GetZDO().GetFloat(ZDOVars.s_fuel, 0f);
			if ((int)currentFuel - lastFuel != 0)
			{
				ItemDrop itemDropPrefab = component.m_fuelItem.gameObject.GetComponent<ItemDrop>();
				ItemDrop.ItemData itemData = itemDropPrefab.m_itemData.Clone();
				itemData.m_dropPrefab = itemDropPrefab.gameObject;
				itemData.m_stack = Mathf.FloorToInt(currentFuel);
				m_container.m_inventory.RemoveAll();
				m_container.m_inventory.AddItem(itemData);
				lastFuel = (int)currentFuel;
			}
		}

		#endregion

		#region Connection Manament

		public override void OnNodeDisconnected(BaseNode node)
		{
			List<ItemDrop.ItemData> itemDatas = m_container.m_inventory.GetAllItems();
			if (!IsConnected && itemDatas.Count > 0)
			{
				foreach (ItemDrop.ItemData itemData in itemDatas) 
				{
					bool isFuel = false;

					if (itemData.m_dropPrefab != null && component.m_fuelItem != null)
					{
						isFuel = itemData.m_dropPrefab.name == component.m_fuelItem.gameObject.name;
					}

					if (isFuel) 
					{
						if (component == null || m_container == null) return;

						component.m_nview?.GetZDO()?.Set(ZDOVars.s_fuel, itemData.m_stack);
					}
					else
					{
						if (component == null || m_container == null) return;
						ItemDrop.DropItem(itemData, 0, component.transform.position, component.transform.rotation);
					}
				}

				m_container.m_inventory.RemoveAll();
			}

			base.OnNodeDisconnected(node);
		}
		#endregion

		#region IContainerInterface Implementation
		public int CalculateAcceptCapacity(ItemDrop.ItemData sourceItem, int desiredAmount)
		{
			if (sourceItem == null || component == null) return 0;

			int actualAmount = desiredAmount > 0 ? desiredAmount : sourceItem.m_stack;
			if (actualAmount <= 0) return 0;

			float currentFuel = component.m_nview.GetZDO().GetFloat(ZDOVars.s_fuel, 0f);
			float maxFuel = component.m_maxFuel;

			int addableAmount = (int)(maxFuel - currentFuel);
			if (addableAmount < 0) addableAmount = 0;
			return addableAmount;

		}
		public bool CanAddItem(ItemDrop.ItemData item)
		{
			if (item == null || component) return false;

			return item.m_dropPrefab.name == component.m_fuelItem.m_itemData.m_dropPrefab.name;

		}
		public bool CanRemoveItem(ItemDrop.ItemData item)
		{
			return true;
		}
		public bool AddItem(ItemDrop.ItemData item, int amount = 0)
		{
			if (item == null || component == null) return false;
			if(!CanAddItem(item)) return false;
			if (component.m_infiniteFuel) return false;

			int actualAmount = amount > 0 ? amount : item.m_stack;
			if (actualAmount <= 0) return false;

			int addableAmount = CalculateAcceptCapacity(item, actualAmount);
			if (actualAmount <= 0) return false;

			component.m_nview.GetZDO().Set(ZDOVars.s_fuel + addableAmount, 0f);
			component.m_fuelAddedEffects.Create(base.transform.position, base.transform.rotation, null, 1f, -1);
			component.UpdateState();
			return true;
		}
		public bool RemoveItem(ItemDrop.ItemData item, int amount = 1)
		{
			if (item == null || m_container == null) return false;
			int actualAmount = amount > 0 ? amount : item.m_stack;
			if(actualAmount <= 0) return false;

			if(m_container.m_inventory.RemoveItem(item, amount))
			{
				SaveInventoryToZDO();
				return true;
			}

			return false;
		}
		public bool AddToInventory(ItemDrop.ItemData item, int amount = 1)
		{
			if (item == null || m_container == null) return false;
			int actualAmount = amount > 0 ? amount : item.m_stack;
			if (actualAmount <= 0) return false;
			if (!m_container.m_inventory.CanAddItem(item)) return false;

			var cloneItem = item.Clone();
			cloneItem.m_stack = actualAmount;

			if (m_container.m_inventory.AddItem(cloneItem))
			{
				SaveInventoryToZDO();
				return true;
			}

			return false;

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
