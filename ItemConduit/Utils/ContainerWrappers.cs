using UnityEngine;
using System.Collections.Generic;
using ItemConduit.Core;
using Logger = Jotunn.Logger;

namespace ItemConduit.Utils
{
	/// <summary>
	/// Simple utility class for container operations
	/// Works with standard containers only - special containers would need reflection
	/// </summary>
	public static class ContainerUtils
	{
		/// <summary>
		/// Check if a game object has a valid container
		/// </summary>
		public static bool HasContainer(GameObject obj)
		{
			if (obj == null) return false;

			// Check for standard Container component
			return obj.GetComponent<Container>() != null;
		}

		/// <summary>
		/// Get container from a game object
		/// </summary>
		public static Container GetContainer(GameObject obj)
		{
			if (obj == null) return null;

			// Try to get Container component
			Container container = obj.GetComponent<Container>();
			if (container != null) return container;

			// Try parent
			container = obj.GetComponentInParent<Container>();
			if (container != null) return container;

			// Try children
			container = obj.GetComponentInChildren<Container>();
			return container;
		}

		/// <summary>
		/// Get inventory from a container
		/// </summary>
		public static Inventory GetInventory(GameObject obj)
		{
			Container container = GetContainer(obj);
			if (container != null)
			{
				return container.GetInventory();
			}
			return null;
		}

		/// <summary>
		/// Get display name for a container
		/// </summary>
		public static string GetContainerName(GameObject obj)
		{
			if (obj == null) return "Unknown";

			Container container = GetContainer(obj);
			if (container != null && !string.IsNullOrEmpty(container.m_name))
			{
				return container.m_name;
			}

			// Check for special containers (just for naming)
			Smelter smelter = obj.GetComponent<Smelter>();
			if (smelter != null && !string.IsNullOrEmpty(smelter.m_name))
				return smelter.m_name;

			Beehive beehive = obj.GetComponent<Beehive>();
			if (beehive != null && !string.IsNullOrEmpty(beehive.m_name))
				return beehive.m_name;

			Fermenter fermenter = obj.GetComponent<Fermenter>();
			if (fermenter != null && !string.IsNullOrEmpty(fermenter.m_name))
				return fermenter.m_name;

			CookingStation cookingStation = obj.GetComponent<CookingStation>();
			if (cookingStation != null && !string.IsNullOrEmpty(cookingStation.m_name))
				return cookingStation.m_name;

			// Default to game object name
			return obj.name.Replace("(Clone)", "").Replace("_", " ");
		}

		/// <summary>
		/// Try to add an item to a container
		/// </summary>
		public static bool TryAddItem(GameObject obj, ItemDrop.ItemData item)
		{
			if (obj == null || item == null) return false;

			Inventory inventory = GetInventory(obj);
			if (inventory != null && inventory.CanAddItem(item))
			{
				return inventory.AddItem(item);
			}

			return false;
		}

		/// <summary>
		/// Try to remove an item from a container
		/// </summary>
		public static bool TryRemoveItem(GameObject obj, ItemDrop.ItemData item, int amount = 1)
		{
			if (obj == null || item == null) return false;

			Inventory inventory = GetInventory(obj);
			if (inventory != null)
			{
				inventory.RemoveItem(item, amount);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Get all items in a container
		/// </summary>
		public static List<ItemDrop.ItemData> GetAllItems(GameObject obj)
		{
			Inventory inventory = GetInventory(obj);
			if (inventory != null)
			{
				return inventory.GetAllItems();
			}
			return new List<ItemDrop.ItemData>();
		}

		/// <summary>
		/// Check if container has space for an item
		/// </summary>
		public static bool HasSpace(GameObject obj, ItemDrop.ItemData item)
		{
			if (obj == null || item == null) return false;

			Inventory inventory = GetInventory(obj);
			if (inventory != null)
			{
				return inventory.CanAddItem(item);
			}

			return false;
		}

		/// <summary>
		/// Get empty slots in container
		/// </summary>
		public static int GetEmptySlots(GameObject obj)
		{
			Inventory inventory = GetInventory(obj);
			if (inventory != null)
			{
				return inventory.GetEmptySlots();
			}
			return 0;
		}

		/// <summary>
		/// Find nearest container to a position
		/// </summary>
		public static Container FindNearestContainer(Vector3 position, float maxDistance)
		{
			Collider[] colliders = Physics.OverlapSphere(position, maxDistance);

			Container nearestContainer = null;
			float nearestDistance = float.MaxValue;

			foreach (Collider col in colliders)
			{
				Container container = GetContainer(col.gameObject);
				if (container != null)
				{
					float distance = Vector3.Distance(position, container.transform.position);
					if (distance < nearestDistance)
					{
						nearestDistance = distance;
						nearestContainer = container;
					}
				}
			}

			return nearestContainer;
		}

		/// <summary>
		/// Find all containers in range
		/// </summary>
		public static List<Container> FindContainersInRange(Vector3 position, float range)
		{
			List<Container> containers = new List<Container>();
			Collider[] colliders = Physics.OverlapSphere(position, range);

			foreach (Collider col in colliders)
			{
				Container container = GetContainer(col.gameObject);
				if (container != null && !containers.Contains(container))
				{
					containers.Add(container);
				}
			}

			return containers;
		}

		/// <summary>
		/// Check if object is a special container (that we can't directly access)
		/// </summary>
		public static bool IsSpecialContainer(GameObject obj)
		{
			if (obj == null) return false;

			return obj.GetComponent<Smelter>() != null ||
				   obj.GetComponent<Beehive>() != null ||
				   obj.GetComponent<Fermenter>() != null ||
				   obj.GetComponent<CookingStation>() != null ||
				   obj.GetComponent<Windmill>() != null;
		}

		/// <summary>
		/// Get special container type name
		/// </summary>
		public static string GetSpecialContainerType(GameObject obj)
		{
			if (obj == null) return "None";

			if (obj.GetComponent<Smelter>() != null) return "Smelter";
			if (obj.GetComponent<Beehive>() != null) return "Beehive";
			if (obj.GetComponent<Fermenter>() != null) return "Fermenter";
			if (obj.GetComponent<CookingStation>() != null) return "CookingStation";
			if (obj.GetComponent<Windmill>() != null) return "Windmill";

			return "None";
		}

		/// <summary>
		/// Debug helper to log container info
		/// </summary>
		public static void LogContainerInfo(GameObject obj)
		{
			if (!ItemConduitMod.ShowDebugInfo.Value) return;

			string name = GetContainerName(obj);

			if (HasContainer(obj))
			{
				Inventory inv = GetInventory(obj);
				if (inv != null)
				{
					int items = inv.GetAllItems().Count;
					int empty = inv.GetEmptySlots();
					Logger.LogInfo($"[ItemConduit] Container '{name}': {items} items, {empty} empty slots");
				}
				else
				{
					Logger.LogWarning($"[ItemConduit] Container '{name}': No inventory");
				}
			}
			else if (IsSpecialContainer(obj))
			{
				string type = GetSpecialContainerType(obj);
				Logger.LogWarning($"[ItemConduit] Special Container '{name}': Type = {type} (not directly accessible)");
			}
			else
			{
				Logger.LogWarning($"[ItemConduit] Object '{name}': Not a container");
			}
		}
	}
}