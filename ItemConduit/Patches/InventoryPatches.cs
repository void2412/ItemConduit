// ItemConduit/Patches/InventoryPatches.cs
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ItemConduit.Patches
{
	public static class InventoryPatches
	{
		// Separate dictionaries for add and remove callbacks
		private static Dictionary<Inventory, Action<ItemDrop.ItemData>> onAddCallbacks = new Dictionary<Inventory, Action<ItemDrop.ItemData>>();
		private static Dictionary<Inventory, Action<ItemDrop.ItemData, int>> onRemoveCallbacks = new Dictionary<Inventory, Action<ItemDrop.ItemData, int>>();

		/// <summary>
		/// Register callbacks for when items are added/removed from a specific inventory
		/// </summary>
		public static void RegisterInventory(Inventory inventory, Action<ItemDrop.ItemData> onAdd, Action<ItemDrop.ItemData, int> onRemove)
		{
			if (inventory == null) return;

			if (onAdd != null)
			{
				onAddCallbacks[inventory] = onAdd;
			}

			if (onRemove != null)
			{
				onRemoveCallbacks[inventory] = onRemove;
			}
		}

		/// <summary>
		/// Register only add callback
		/// </summary>
		public static void RegisterInventoryAdd(Inventory inventory, Action<ItemDrop.ItemData> onAdd)
		{
			if (inventory != null && onAdd != null)
			{
				onAddCallbacks[inventory] = onAdd;
			}
		}

		/// <summary>
		/// Register only remove callback
		/// </summary>
		public static void RegisterInventoryRemove(Inventory inventory, Action<ItemDrop.ItemData, int> onRemove)
		{
			if (inventory != null && onRemove != null)
			{
				onRemoveCallbacks[inventory] = onRemove;
			}
		}

		/// <summary>
		/// Unregister callbacks for an inventory
		/// </summary>
		public static void UnregisterInventory(Inventory inventory)
		{
			if (inventory != null)
			{
				onAddCallbacks.Remove(inventory);
				onRemoveCallbacks.Remove(inventory);
			}
		}

		#region AddItem Patches
		/// <summary>
		/// Patch AddItem to trigger add callback
		/// </summary>
		[HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new Type[] { typeof(ItemDrop.ItemData) })]
		public static class Inventory_AddItem_Patch
		{
			private static void Postfix(Inventory __instance, ItemDrop.ItemData item, bool __result)
			{
				if (__result && onAddCallbacks.TryGetValue(__instance, out Action<ItemDrop.ItemData> callback))
				{
					callback?.Invoke(item);
				}
			}
		}

		[HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new Type[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int) })]
		public static class Inventory_AddItem_Position_Patch
		{
			private static void Postfix(Inventory __instance, ItemDrop.ItemData item, bool __result)
			{
				if (__result && onAddCallbacks.TryGetValue(__instance, out Action<ItemDrop.ItemData> callback))
				{
					callback?.Invoke(item);
				}
			}
		}

		[HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new Type[] { typeof(ItemDrop.ItemData), typeof(Vector2i) })]
		public static class Inventory_AddItem_Vector_Patch
		{
			private static void Postfix(Inventory __instance, ItemDrop.ItemData item, Vector2i pos, bool __result)
			{
				if (__result && onAddCallbacks.TryGetValue(__instance, out Action<ItemDrop.ItemData> callback))
				{
					callback?.Invoke(item);
				}
			}
		}

		#endregion


		#region RemoveItem Patches

		/// <summary>
		/// Patch RemoveItem(ItemDrop.ItemData item, int amount) - MAIN removal method
		/// Used for: drag-and-drop, node extraction, crafting consumption
		/// </summary>
		[HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), new Type[] { typeof(ItemDrop.ItemData), typeof(int) })]
		public static class Inventory_RemoveItem_Amount_Patch
		{
			private static void Postfix(Inventory __instance, ItemDrop.ItemData item, int amount, bool __result)
			{
				if (__result && onRemoveCallbacks.TryGetValue(__instance, out Action<ItemDrop.ItemData, int> callback))
				{
					callback?.Invoke(item, amount);
				}
			}
		}

		/// <summary>
		/// Patch RemoveItem(ItemDrop.ItemData item) - Remove entire stack
		/// Used when removing the whole item without specifying amount
		/// </summary>
		[HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), new Type[] { typeof(ItemDrop.ItemData) })]
		public static class Inventory_RemoveItem_NoAmount_Patch
		{
			private static void Postfix(Inventory __instance, ItemDrop.ItemData item, bool __result)
			{
				if (__result && onRemoveCallbacks.TryGetValue(__instance, out Action<ItemDrop.ItemData, int> callback))
				{
					// When no amount specified, the entire stack is removed
					callback?.Invoke(item, item.m_stack);
				}
			}
		}


		/// <summary>
		/// Patch RemoveItem(string name, int amount, int quality) - Remove by name and quality
		/// Used for: removing specific quality items (e.g., quality 3 sword)
		/// </summary>
		[HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), new Type[] { typeof(string), typeof(int), typeof(int), typeof(bool) })]
		public static class Inventory_RemoveItem_ByNameQuality_Patch
		{
			private static List<ItemDrop.ItemData> capturedItems = new List<ItemDrop.ItemData>();

			private static bool Prefix(Inventory __instance, string name,int amount, int itemQuality, bool worldLevelBased)
			{
				foreach (ItemDrop.ItemData itemData in __instance.m_inventory)
				{
					if (itemData.m_shared.m_name == name && (itemQuality < 0 || itemData.m_quality == itemQuality) && (!worldLevelBased || itemData.m_worldLevel >= Game.m_worldLevel))
					{
						int num = Mathf.Min(itemData.m_stack, amount);
						var cloneItemData = itemData.Clone();
						itemData.m_stack = num;
						capturedItems.Add(cloneItemData);
						itemData.m_stack -= num;
						amount -= num;
						if (amount <= 0)
						{
							break;
						}
					}
				}
				__instance.m_inventory.RemoveAll((ItemDrop.ItemData x) => x.m_stack <= 0);
				__instance.Changed();
				return false;
			}

			private static void Postfix(Inventory __instance, string name, int amount, int itemQuality, bool worldLevelBased)
			{
				if (capturedItems != null && capturedItems.Count > 0 && onRemoveCallbacks.TryGetValue(__instance, out Action<ItemDrop.ItemData, int> callback))
				{
					foreach (ItemDrop.ItemData item in capturedItems) {
						callback?.Invoke(item, item.m_stack);
					}
				}
			}
		}

		/// <summary>
		/// Patch RemoveOneItem(ItemDrop.ItemData item) - Remove single item from stack
		/// Used internally by Valheim for various operations
		/// </summary>
		[HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveOneItem))]
		public static class Inventory_RemoveOneItem_Patch
		{
			private static void Postfix(Inventory __instance, ItemDrop.ItemData item, bool __result)
			{
				if (__result && onRemoveCallbacks.TryGetValue(__instance, out Action<ItemDrop.ItemData, int> callback))
				{
					callback?.Invoke(item, 1); // Always removes 1 item
				}
			}
		}

		[HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveAll))]
		public static class Inventory_RemoveAll_Patch
		{
			private static List<ItemDrop.ItemData> deletedInventory = null;
			private static void Prefix(Inventory __instance)
			{
				deletedInventory = new List<ItemDrop.ItemData>(__instance.m_inventory);
			} 
			private static void Postfix(Inventory __instance)
			{
				if(deletedInventory!= null && onRemoveCallbacks.TryGetValue(__instance, out Action<ItemDrop.ItemData, int> callback))
				{
					foreach (var item in deletedInventory)
					{
						callback?.Invoke(item, item.m_stack);
					}
				}
			}
		}

		[HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), new Type[] {typeof(int)})]
		public static class Inventory_RemoveAt_Patch
		{
			private static ItemDrop.ItemData deletedItem = null;
			private static void Prefix(Inventory __instance, int index)
			{
				deletedItem = __instance.m_inventory.ElementAt<ItemDrop.ItemData>(index);
			}

			private static void Postfix(Inventory __instance, int index, bool __result)
			{
				if(__result && onRemoveCallbacks.TryGetValue(__instance, out Action<ItemDrop.ItemData, int> callback))
				{
					callback?.Invoke(deletedItem, deletedItem.m_stack);
				}
			}
		}

		#endregion
	}
}