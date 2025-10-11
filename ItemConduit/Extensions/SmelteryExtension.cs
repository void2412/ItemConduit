using ItemConduit.Interfaces;
using ItemConduit.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace ItemConduit.Extensions
{
	/// <summary>
	/// Extension for Smelter objects with node notification
	/// </summary>
	public class SmelteryExtension : BaseExtension, IContainerInterface
	{
		private Smelter smelter;
		public Inventory m_inventory = new Inventory("smelterOutput", Jotunn.Managers.GUIManager.Instance.GetSprite("woodpanel_playerinventory"),1,1);
		public uint m_lastRevision;
		public string m_lastDataString;
		public bool m_loading;

		protected override void Awake()
		{
			base.Awake();
			smelter = GetComponentInParent<Smelter>();
			ZDO zdo = zNetView.GetZDO();
			if (zdo == null) return;

			m_inventory = new Inventory(smelter.name + "Output", Jotunn.Managers.GUIManager.Instance.GetSprite("woodpanel_playerinventory"), 1, 1);
			m_inventory.m_onChanged = (Action)Delegate.Combine(m_inventory.m_onChanged, new Action(OnSmelterChange));

			base.InvokeRepeating("CheckForChanges", 0f, 1f);
		}

		public void Save()
		{
			ZPackage zpackage = new ZPackage();
			m_inventory.Save(zpackage);
			string @base = zpackage.GetBase64();
			zNetView.GetZDO().Set(ZDOVars.s_items, @base);
			m_lastRevision = zNetView.GetZDO().DataRevision;
			m_lastDataString = @base;
		}

		public bool Load()
		{
			if (zNetView.GetZDO().DataRevision == m_lastRevision)
			{
				return false;
			}

			m_lastRevision = zNetView.GetZDO().DataRevision;
			string @string = zNetView.GetZDO().GetString(ZDOVars.s_items, "");

			if(@string == m_lastDataString)
			{
				m_lastDataString = @string;
				return true;
			}

			if (string.IsNullOrEmpty(@string))
			{
				m_lastDataString = @string;
				return true;
			}

			ZPackage zpackage = new ZPackage(@string);
			m_loading = true;
			m_inventory.Load(zpackage);
			m_loading = false;
			m_lastDataString = @string;
			return true;

		}

		public void OnSmelterChange()
		{
			if (m_loading)
			{
				return;
			}
			if (!zNetView.IsOwner())
			{
				return;
			}
			this.Save();
		}

		public void CheckForChanges()
		{
			if (zNetView.IsValid()) return;

			if (!Load()) return;


		}

		public override void OnNodeDisconnected(BaseNode node)
		{
			base.OnNodeDisconnected(node);

			// If no more nodes are connected and we have processed ore, spawn them
			if (!IsConnected && m_inventory.GetAllItems().Count > 0)
			{
				foreach (var item in m_inventory.GetAllItems())
				{
					smelter.Spawn(item.m_dropPrefab.name, item.m_stack);
					m_inventory.RemoveItem(item);
				}
			}
		}

		#region IContainerInterface Implementation

		public int CalculateAcceptCapacity(ItemDrop.ItemData sourceItem, int desiredAmount)
		{
			if (sourceItem == null || desiredAmount <= 0) return 0;
			if (!CanAddItem(sourceItem)) return 0;

			string type = ItemType(sourceItem);

			float acceptableAmount = 0;
			if (type == "Fuel")
			{
				var maxFuel = smelter.m_maxFuel;
				var currentFuel = smelter.GetFuel();
				acceptableAmount = maxFuel - currentFuel;
			}

			if (type == "Ore")
			{
				var maxOre = smelter.m_maxOre;
				var currentOreSize = smelter.GetQueueSize();
				acceptableAmount = maxOre - currentOreSize;
			}

			if (acceptableAmount <= 0) return 0;

			return Mathf.Min(desiredAmount, (int)acceptableAmount);
		}

		public bool CanAddItem(ItemDrop.ItemData item)
		{
			if (smelter == null) return false;
			string type = ItemType(item);
			return type == "Fuel" || type == "Ore";
		}

		public bool CanRemoveItem(ItemDrop.ItemData item)
		{
			List<Smelter.ItemConversion> itemConversions = smelter.m_conversion;
			foreach (var itemConversion in itemConversions) {
				if (itemConversion.m_to.m_itemData.m_dropPrefab.name == item.m_dropPrefab.name)
				{
					return true;
				}
			}
			
			return false;
		}

		public bool AddItem(ItemDrop.ItemData item, int amount = 0)
		{
			if (!CanAddItem(item)) return false;

			int addableAmount = CalculateAcceptCapacity(item, amount);
			if (addableAmount <= 0) return false;


			string type = ItemType(item);

			if (type == "Fuel")
			{
				AddFuel(addableAmount);
			}

			if (type == "Ore")
			{
				AddOre(item.m_dropPrefab.name,addableAmount);
			}

			return true;
		}

		public void AddFuel(int amount)
		{
			for (int i = 0; i < amount; i++) 
			{
				float currentFuel = smelter.GetFuel();
				smelter.SetFuel(currentFuel + 1);
			}
		}

		public void AddOre(string name,int amount)
		{
			if(!smelter.IsItemAllowed(name)) return;

			for (int i = 0;i < amount; i++)
			{
				smelter.QueueOre(name);
			}
		}

		public bool RemoveItem(ItemDrop.ItemData item, int amount = 0)
		{
			if (item == null) return false;
			if (amount <= 0 && item.m_stack <= 0) return false;
			if (amount > 0)
			{
				item.m_stack = amount;
			}

			if (m_inventory.RemoveItem(item)) return true;

			return false;
		}

		public Inventory GetInventory()
		{
			return m_inventory;
		}

		public bool AddToInventory(ItemDrop.ItemData item, int amount = 0)
		{
			if (item == null) return false;
			if (amount <= 0 && item.m_stack <=0) return false;
			if (!m_inventory.CanAddItem(item)) return false;

			if (amount > 0) {
				item.m_stack = amount;
			}

			if (m_inventory.AddItem(item)) return true;

			return false;
		}

		public string GetName()
		{
			return smelter?.m_name ?? "Smelter";
		}

		public Vector3 GetTransformPosition()
		{
			return transform.position;
		}

		private string ItemType(ItemDrop.ItemData item)
		{
			if (item == null) return "Invalid";

			// Check if it's fuel
			if (smelter.m_fuelItem.m_itemData.m_dropPrefab.name == item.m_dropPrefab.name)
			{
				return "Fuel";
			}

			// Check if it's a valid ore for conversion
			foreach (var conversion in smelter.m_conversion)
			{
				if (conversion.m_from.m_itemData.m_dropPrefab.name == item.m_dropPrefab.name)
				{
					return "Ore";
				}
			}

			return "Invalid";
		}

		#endregion
	}
}