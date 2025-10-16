using HarmonyLib;
using ItemConduit.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ItemConduit.Patches
{
	public static class FirePlacePatches
	{
		[HarmonyPatch(typeof(Fireplace), "Awake")]
		public static class Fireplace_Awake_Patch
		{
			private static void Postfix(Fireplace __instance)
			{
				if (__instance.GetComponent<FirePlaceExtention>() == null)
				{
					__instance.gameObject.AddComponent<FirePlaceExtention>();
				}
			}
		}

		[HarmonyPatch(typeof(Fireplace), "UpdateFireplace")]
		public static class Fireplace_UpdateFireplace_Patch
		{
			private static float lastSyncTime = 0f;
			private const float SYNC_INTERVAL = 1f; // Sync every 1 second to avoid spam

			private static void Postfix(Fireplace __instance)
			{
				var extension = __instance.GetComponent<FirePlaceExtention>();
				if (extension == null) return;

				// Throttle sync to avoid excessive updates
				float currentTime = Time.time;
				if (currentTime - lastSyncTime < SYNC_INTERVAL) return;
				lastSyncTime = currentTime;

				// Sync the internal fuel state to inventory after burning
				extension.SyncFireplaceToInventory();
			}
		}

		[HarmonyPatch(typeof(Fireplace), "GetHoverText")]
		public static class Fireplace_GetHoverText_Patch
		{
			private static void Postfix(Fireplace __instance, ref string __result)
			{
				var extension = __instance.GetComponent<FirePlaceExtention>();
				if (extension == null || !extension.IsConnected) return;

				// Add Shift+E hint when connected
				__result += Localization.instance.Localize("\n[<color=yellow><b>Shift + $KEY_Use</b></color>] Open Fuel Inventory");
			}
		}

		[HarmonyPatch(typeof(Fireplace), "UseItem")]
		public static class Fireplace_UseItem_Patch
		{
			private static bool Prefix(Fireplace __instance, Humanoid user, ItemDrop.ItemData item, ref bool __result)
			{
				var extension = __instance.GetComponent<FirePlaceExtention>();
				if (extension == null) return true;

				// Only validate when connected and trying to add fuel
				if (item == null || __instance.m_fuelItem == null) return true;
				if (item.m_dropPrefab?.name != __instance.m_fuelItem.gameObject.name) return true;

				// Check if we can add more fuel
				if (extension.m_container != null && extension.m_container.m_inventory != null)
				{
					// Check if inventory is full (no empty slots)
					int emptySlots = extension.m_container.m_inventory.GetEmptySlots();

					if (emptySlots <= 0)
					{
						// Check if there are non-fuel items taking up space
						var allItems = extension.m_container.m_inventory.GetAllItems();
						var nonFuelItems = allItems.Where(i =>
							i.m_dropPrefab?.name != __instance.m_fuelItem.gameObject.name).ToList();

						if (nonFuelItems.Count > 0)
						{
							// Show message - inventory is full and has non-fuel items
							user.Message(MessageHud.MessageType.Center, "Fuel inventory full. Remove non-fuel items to make space");
							__result = true; // Return true to indicate we handled it (prevents item consumption)
							return false; // Skip original method
						}
					}
				}

				// Allow normal processing
				return true;
			}
		}


			[HarmonyPatch(typeof(Fireplace), "Interact")]
		public static class Fireplace_Interact_Patch
		{
			private static bool Prefix(Fireplace __instance, Humanoid user, bool hold, ref bool __result)
			{
				var extension = __instance.GetComponent<FirePlaceExtention>();
				if (extension == null) return true;

				// Check if Shift is being held
				bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

				if (shiftHeld && !hold && extension.IsConnected)
				{
					// Open the container inventory when Shift+E is pressed
					InventoryGui.instance.Show(extension.m_container, 1);
					__result = true;
					return false; // Prevent normal fireplace interaction
				}

				// Check if we can add more fuel
				if (!hold && extension.m_container != null && extension.m_container.m_inventory != null)
				{
					// Check if inventory is full (no empty slots)
					int emptySlots = extension.m_container.m_inventory.GetEmptySlots();

					if (emptySlots <= 0)
					{
						// Check if there are non-fuel items taking up space
						var allItems = extension.m_container.m_inventory.GetAllItems();
						var nonFuelItems = allItems.Where(i =>
							i.m_dropPrefab?.name != __instance.m_fuelItem?.gameObject?.name).ToList();

						if (nonFuelItems.Count > 0)
						{
							// Show message - inventory is full and has non-fuel items
							user.Message(MessageHud.MessageType.Center, "Fuel inventory full. Remove non-fuel items to make space");
							__result = false;
							return false; // Skip original method
						}
					}
				}

				// Allow normal fireplace interaction
				return true;
			}
		}
	}
}
