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

		#region Helper Methods

		private bool IsFuelItem(ItemDrop.ItemData item)
		{
			if (item == null || component == null || component.m_fuelItem == null)
				return false;

			return item.m_dropPrefab?.name == component.m_fuelItem.gameObject?.name;
		}


		#endregion


		#region IContainerInterface Implementation
		public int CalculateAcceptCapacity(ItemDrop.ItemData sourceItem, int desiredAmount)
		{
			if (sourceItem == null || component == null) return 0;

			int actualAmount = desiredAmount > 0 ? desiredAmount : sourceItem.m_stack;
			if (actualAmount <= 0) return 0;

			float currentFuel = component.m_nview.GetZDO().GetFloat(ZDOVars.s_fuel, 0f);
			int addableAmount = (int)(component.m_maxFuel - currentFuel);

			return Math.Min(addableAmount, desiredAmount);

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

			float num = component.m_nview.GetZDO().GetFloat(ZDOVars.s_fuel, 0f);
			num = Mathf.Clamp(num + addableAmount, 0f, component.m_maxFuel);
			component.m_nview.GetZDO().Set(ZDOVars.s_fuel, num);
			component.m_fuelAddedEffects.Create(base.transform.position, base.transform.rotation, null, 1f, -1);
			component.UpdateState();
			return true;
		}
		public bool RemoveItem(ItemDrop.ItemData item, int amount = 0)
		{
			return false;
		}
		public Inventory GetInventory()
		{
			return m_container.m_inventory;
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
