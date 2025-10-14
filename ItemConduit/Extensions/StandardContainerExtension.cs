using ItemConduit.Interfaces;
using System.Linq;
using UnityEngine;

namespace ItemConduit.Extensions
{
	/// <summary>
	/// Extension for standard Container objects with node notification
	/// </summary>
	public class StandardContainerExtension : BaseExtension<Container>, IContainerInterface
	{
		private Container container;

		protected override void Awake()
		{
			container = GetComponent<Container>();
		}

		#region IContainerInterface Implementation

		public int CalculateAcceptCapacity(ItemDrop.ItemData sourceItem, int desiredAmount)
		{
			if (container == null || sourceItem == null || desiredAmount <= 0)
				return 0;

			var destInventory = container.m_inventory;
			if (destInventory == null) return 0;

			int totalCanAccept = 0;
			int maxStackSize = sourceItem.m_shared.m_maxStackSize;

			// Check existing stacks that can accept more items
			var existingStacks = destInventory.GetAllItems()
				.Where(item => item.m_shared.m_name == sourceItem.m_shared.m_name &&
							  item.m_quality == sourceItem.m_quality &&
							  item.m_variant == sourceItem.m_variant &&
							  item.m_stack < maxStackSize)
				.OrderBy(item => item.m_gridPos.y * destInventory.GetWidth() + item.m_gridPos.x)
				.ToList();

			foreach (var existingStack in existingStacks)
			{
				int spaceInStack = maxStackSize - existingStack.m_stack;
				int canAddToStack = Mathf.Min(spaceInStack, desiredAmount - totalCanAccept);
				totalCanAccept += canAddToStack;

				if (totalCanAccept >= desiredAmount)
					return desiredAmount;
			}

			// Check empty slots
			int emptySlots = destInventory.GetEmptySlots();
			if (emptySlots > 0)
			{
				int itemsPerSlot = maxStackSize;
				int canAddToEmpty = Mathf.Min(emptySlots * itemsPerSlot, desiredAmount - totalCanAccept);
				totalCanAccept += canAddToEmpty;
			}

			return Mathf.Min(desiredAmount, totalCanAccept);
		}

		public bool CanAddItem(ItemDrop.ItemData item)
		{
			return container.m_inventory?.CanAddItem(item) ?? false;
		}

		public bool CanRemoveItem(ItemDrop.ItemData item)
		{
			return true;
		}

		public bool AddItem(ItemDrop.ItemData item, int amount = 0)
		{
			if (container.m_inventory == null) return false;
			if (amount <= 0 && item.m_stack <= 0) return false;

			if (amount > 0)
			{
				item.m_stack = amount;
			}

			return container.m_inventory.AddItem(item);
		}

		public bool RemoveItem(ItemDrop.ItemData item, int amount = 0)
		{
			if (container.m_inventory == null) return false;
			if (amount <= 0 && item.m_stack <= 0) return false;

			return container.m_inventory.RemoveItem(item, amount);
		}

		public Inventory GetInventory()
		{
			return container.m_inventory;
		}

		public string GetName()
		{
			return container?.m_name ?? "Container";
		}

		public Vector3 GetTransformPosition()
		{
			return transform.position;
		}

		#endregion
	}
}