using ItemConduit.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ItemConduit.Extensions
{
	public class StandardContainerExtension : BaseExtension, IContainerInterface
	{
		private Container container;
		private Inventory inventory;
		protected override void Awake()
		{
			base.Awake();
			container = GetComponent<Container>();
			inventory = container.GetInventory();
		}
		public int CalculateAcceptCapacity(ItemDrop.ItemData sourceItem, int desiredAmount)
		{
			if (container == null || sourceItem == null || desiredAmount <= 0) 
				return 0;

			var destInventory = inventory;
			if (destInventory == null) return 0;

			int totalCanAccept = 0;
			int maxStackSize = sourceItem.m_shared.m_maxStackSize;

			var existingStacks = destInventory.GetAllItems()
				.Where(item => item.m_shared.m_name == sourceItem.m_shared.m_name &&
							  item.m_quality == sourceItem.m_quality &&
							  item.m_variant == sourceItem.m_variant &&
							  item.m_stack < maxStackSize)
				.OrderBy(item => item.m_gridPos.y * destInventory.GetWidth() + item.m_gridPos.x) // Order by position
				.ToList();

			foreach (var existingStack in existingStacks)
			{
				int spaceInStack = maxStackSize - existingStack.m_stack;
				int canAddToStack = Mathf.Min(spaceInStack, desiredAmount - totalCanAccept);
				totalCanAccept += canAddToStack;

				if (totalCanAccept >= desiredAmount)
					return desiredAmount;
			}

			int emptySlots = destInventory.GetEmptySlots();
			if(emptySlots > 0)
			{
				int itemsPerSlot = maxStackSize;
				int canAddToEmpty = Mathf.Min(emptySlots * itemsPerSlot, desiredAmount - totalCanAccept);
				totalCanAccept += canAddToEmpty;
			}

			return Mathf.Min(desiredAmount, totalCanAccept);
		}

		public bool CanAddItem(ItemDrop.ItemData item)
		{
			return inventory.CanAddItem(item);
		}

		public bool CanRemoveItem(ItemDrop.ItemData item)
		{
			return true;
		}

		public bool AddItem(ItemDrop.ItemData item, int amount = 0)
		{
			if (amount <= 0 && item.m_stack <= 0) return false;
			if (amount > 0)
			{
				item.m_stack = amount;
			}
			return inventory.AddItem(item);
		}

		public bool RemoveItem(ItemDrop.ItemData item, int amount = 0) 
		{
			if (amount <= 0 && item.m_stack <= 0) return false;
			if (amount > 0) { 
				item.m_stack = amount;
			}
			return inventory.RemoveItem(item);
		}

		public Inventory GetInventory()
		{
			
			return inventory;
		}

		public string GetName()
		{
			return container.m_name;
		}

		public UnityEngine.Vector3 GetTransformPosition()
		{
			return container.transform.position;
		}
	}
}
