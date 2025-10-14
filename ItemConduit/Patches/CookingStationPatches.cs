using HarmonyLib;
using ItemConduit.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ItemConduit.Patches
{
	public static class CookingStationPatches
	{
		[HarmonyPatch(typeof(CookingStation), "Awake")]
		public static class CookingStation_Awake_Patch
		{
			private static void Postfix(CookingStation __instance)
			{
				if (__instance.GetComponent<CookingStationExtension>() == null)
				{
					__instance.gameObject.AddComponent<CookingStationExtension>();
				}
			}
		}

		[HarmonyPatch(typeof(CookingStation), "UpdateCooking")]
		public static class CookingStation_UpdateCooking_Patch
		{
			private static void Postfix(CookingStation __instance)
			{
				var extension = __instance.GetComponent<CookingStationExtension>();
				if (extension == null) return;
				if (!extension.IsConnected) return;

				for (int i = 0; i < __instance.m_slots.Length; i++)
				{
					CookingStation.Status status;
					string itemName;
					float cookedTime;
					__instance.GetSlot(i, out itemName, out cookedTime, out status);

					if (string.IsNullOrEmpty(itemName)) continue;
					
					CookingStation.ItemConversion itemConversion = __instance.GetItemConversion(itemName);
					if(itemConversion == null) continue;

					if (status == CookingStation.Status.Done)
					{
						ItemDrop itemDropPrefab = itemConversion.m_to.gameObject.GetComponent<ItemDrop>();

						ItemDrop.ItemData itemData = itemDropPrefab.m_itemData.Clone();
						itemData.m_dropPrefab = itemDropPrefab.gameObject;
						itemData.m_stack = 1;
						
						if (extension.AddToInventory(itemData))
						{
							__instance.SetSlot(i, "", 0f, CookingStation.Status.NotDone);
							extension.freeSlotList.Add(i);
						}

					}

					if (status == CookingStation.Status.Burnt)
					{
						ItemDrop itemDropPrefab = __instance.m_overCookedItem.gameObject.GetComponent<ItemDrop>();

						ItemDrop.ItemData itemData = itemDropPrefab.m_itemData.Clone();
						itemData.m_dropPrefab = itemDropPrefab.gameObject;
						itemData.m_stack = 1;

						if(extension.AddToInventory(itemData))
						{
							__instance.SetSlot(i, "", 0f, CookingStation.Status.NotDone);
							extension.freeSlotList.Add(i);
						}
					}
				}
			}
		}
	}
}
