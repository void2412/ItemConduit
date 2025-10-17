using ItemConduit.Config;
using ItemConduit.Interfaces;
using ItemConduit.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace ItemConduit.Extensions
{
	public class TurretExtension : BaseExtension<Turret>, IContainerInterface
	{

		#region Helper Methods

		private bool IsAmmoItem(ItemDrop.ItemData item)
		{
			if (item == null || component == null || component.m_defaultAmmo == null)
				return false;

			return component.IsItemAllowed(item.m_shared.m_name);
		}
		
		private bool IsEmpty()
		{
			return component.m_nview.GetZDO().GetInt(ZDOVars.s_ammo, 0) == 0;
		}

		private bool IsAmmoTypeLoaded(string ammoType)
		{
			return component.m_nview.GetZDO().GetString(ZDOVars.s_ammoType, "") == ammoType;
		}

		#endregion


		#region IContainerInterface Implementation

		public int CalculateAcceptCapacity(ItemDrop.ItemData sourceItem, int desiredAmount)
		{
			if (sourceItem == null || component == null) return 0;

			int actualAmount = desiredAmount > 0 ? desiredAmount : sourceItem.m_stack;
			if (actualAmount <= 0) return 0;

			int currentAmmo = component.m_nview.GetZDO().GetInt(ZDOVars.s_ammo, 0);
			int addableAmount = Mathf.FloorToInt(component.m_maxAmmo - currentAmmo);

			return Mathf.Min(addableAmount, actualAmount);
		}

		public bool CanAddItem(ItemDrop.ItemData item)
		{
			if (item == null || component == null) return false;
			if(!IsAmmoItem(item)) return false;
			if (!IsAmmoTypeLoaded(item.m_dropPrefab.name))
			{
				if (IsEmpty())
				{
					return true;
				}
			}


			return true;
		}

		public bool CanRemoveItem(ItemDrop.ItemData item)
		{
			if (item == null || component == null) return false;
			return IsAmmoItem(item);
		}

		public bool AddItem(ItemDrop.ItemData item, int amount = 0)
		{
			if (item == null || component == null) return false;
			if (!CanAddItem(item)) return false;

			int actualAmount = amount > 0 ? amount : item.m_stack;
			if (actualAmount <= 0) return false;

			int addableAmount = CalculateAcceptCapacity(item, actualAmount);
			if (addableAmount <= 0) return false;

			int currentAmmo = component.m_nview.GetZDO().GetInt(ZDOVars.s_ammo, 0);
			int newAmmo = Mathf.Clamp(currentAmmo + addableAmount, 0, component.m_maxAmmo);
			component.m_nview.GetZDO().Set(ZDOVars.s_ammo, newAmmo);

			// Trigger ammo added effects if available
			if (component.m_addAmmoEffect != null)
			{
				component.m_addAmmoEffect.Create(component.m_turretBody.transform.position, component.m_turretBody.transform.rotation, null, 1f, -1);
			}

			component.UpdateVisualBolt();
			return true;
		}

		public bool RemoveItem(ItemDrop.ItemData item, int amount = 0)
		{
			return false;
		}

		public Inventory GetInventory()
		{
			return m_container?.m_inventory;
		}

		public string GetName()
		{
			return component?.m_name ?? "Turret";
		}

		public Vector3 GetTransformPosition()
		{
			return component?.transform.position ?? transform.position;
		}

		#endregion

	}
}